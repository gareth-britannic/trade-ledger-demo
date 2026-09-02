using System.Text.Json;
using Amazon.Lambda.Core;
using Amazon.Lambda.SQSEvents;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shouldly;
using TradeLedger.Application.Messaging;
using TradeLedger.Common;
using TradeLedger.Processor.Handlers;
using Xunit;

namespace TradeLedger.UnitTests.Processor;

public sealed class SqsMessageHandlerTests
{
    [Fact]
    public async Task ValidRecord_MapsFillCorrelationAndCancellation_ThenClearsScopedState()
    {
        using var source = new CancellationTokenSource();
        var fillId = Guid.NewGuid();
        var harness = new Harness();
        var response = await harness.Handler.HandleAsync(
            Event(Message("one", "ACME", fillId, "correlation-123")),
            Context(),
            source.Token);

        response.BatchItemFailures.ShouldBeEmpty();
        harness.Calls.ShouldHaveSingleItem();
        harness.Calls[0].FillId.ShouldBe(fillId);
        harness.Calls[0].CorrelationId.ShouldBe("correlation-123");
        harness.Calls[0].CancellationToken.ShouldBe(source.Token);
        harness.Providers.ShouldHaveSingleItem().CorrelationId.ShouldBeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("bad\ncorrelation")]
    public async Task MissingOrInvalidCorrelation_IsReplacedSafely(string? correlationId)
    {
        var harness = new Harness();

        await harness.Handler.HandleAsync(
            Event(Message("one", "ACME", Guid.NewGuid(), correlationId)),
            Context(),
            CancellationToken.None);

        var generated = harness.Calls.ShouldHaveSingleItem().CorrelationId;
        generated.ShouldNotBeNullOrWhiteSpace();
        generated.Length.ShouldBeLessThanOrEqualTo(CorrelationIdFactory.MaximumLength);
        generated.Any(char.IsControl).ShouldBeFalse();
    }

    [Fact]
    public async Task MalformedPayload_IsAnIndividualFailureAndDoesNotInvokeService()
    {
        var harness = new Harness();
        var record = Message("bad", "ACME", Guid.NewGuid(), "correlation");
        record.Body = "{";

        var response = await harness.Handler.HandleAsync(
            Event(record), Context(), CancellationToken.None);

        response.BatchItemFailures.Select(failure => failure.ItemIdentifier).ShouldBe(["bad"]);
        harness.Calls.ShouldBeEmpty();
    }

    [Fact]
    public async Task ServiceFailure_BlocksLaterSameGroupButOtherGroupsContinue()
    {
        var failedId = Guid.NewGuid();
        var skippedId = Guid.NewGuid();
        var harness = new Harness(failedId);
        var response = await harness.Handler.HandleAsync(
            Event(
                Message("a-1", "ACME", Guid.NewGuid(), "one"),
                Message("a-2", "ACME", failedId, "two"),
                Message("b-1", "BETA", Guid.NewGuid(), "three"),
                Message("a-3", "ACME", skippedId, "four")),
            Context(),
            CancellationToken.None);

        response.BatchItemFailures.Select(failure => failure.ItemIdentifier)
            .ShouldBe(["a-2", "a-3"]);
        harness.Calls.Select(call => call.FillId).ShouldNotContain(skippedId);
        harness.Calls.Count.ShouldBe(3);
    }

    [Fact]
    public async Task EveryProcessedRecord_UsesAFreshDependencyInjectionScope()
    {
        var harness = new Harness();

        await harness.Handler.HandleAsync(
            Event(
                Message("one", "ACME", Guid.NewGuid(), "one"),
                Message("two", "BETA", Guid.NewGuid(), "two")),
            Context(),
            CancellationToken.None);

        harness.ServiceInstanceIds.Distinct().Count().ShouldBe(2);
        harness.Providers.Count.ShouldBe(2);
    }

    [Fact]
    public async Task PreCancelledInvocation_DoesNotInvokeService()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();
        var harness = new Harness();

        await Should.ThrowAsync<OperationCanceledException>(() => harness.Handler.HandleAsync(
            Event(Message("one", "ACME", Guid.NewGuid(), "one")),
            Context(),
            source.Token));

        harness.Calls.ShouldBeEmpty();
    }

    private static SQSEvent Event(params SQSEvent.SQSMessage[] records) => new() { Records = [..records] };

    private static SQSEvent.SQSMessage Message(
        string messageId,
        string symbol,
        Guid fillId,
        string? correlationId)
    {
        var message = new FillRequestMessage(fillId, symbol, "Buy", 10m, 12m, DateTimeOffset.UtcNow);
        var attributes = new Dictionary<string, SQSEvent.MessageAttribute>();
        if (correlationId is not null)
        {
            attributes[CorrelationIdMetadata.PropertyName] = new SQSEvent.MessageAttribute
            {
                DataType = "String",
                StringValue = correlationId
            };
        }

        return new SQSEvent.SQSMessage
        {
            MessageId = messageId,
            Body = JsonSerializer.Serialize(message, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
            Attributes = new Dictionary<string, string> { ["MessageGroupId"] = symbol },
            MessageAttributes = attributes
        };
    }

    private static ILambdaContext Context()
    {
        var context = new Mock<ILambdaContext>();
        context.SetupGet(instance => instance.AwsRequestId).Returns("lambda-request");
        context.SetupGet(instance => instance.RemainingTime).Returns(TimeSpan.FromMinutes(1));
        return context.Object;
    }

    private sealed class Harness
    {
        public Harness(Guid? failureId = null)
        {
            var services = new ServiceCollection();
            services.AddScoped<CorrelationIdProvider>(_ =>
            {
                var provider = new CorrelationIdProvider();
                Providers.Add(provider);
                return provider;
            });
            services.AddScoped<ICorrelationIdProvider>(provider =>
                provider.GetRequiredService<CorrelationIdProvider>());
            services.AddScoped<ISqsMessageHandler<FillRequestMessage>>(provider =>
            {
                var id = Guid.NewGuid();
                ServiceInstanceIds.Add(id);
                return new CapturingMessageHandler(
                    provider.GetRequiredService<ICorrelationIdProvider>(),
                    id,
                    Calls,
                    failureId);
            });
            ServiceProvider = services.BuildServiceProvider();
            Handler = new SqsMessageHandler<FillRequestMessage>(
                ServiceProvider.GetRequiredService<IServiceScopeFactory>(),
                NullLogger<SqsMessageHandler<FillRequestMessage>>.Instance);
        }

        public ServiceProvider ServiceProvider { get; }
        public SqsMessageHandler<FillRequestMessage> Handler { get; }
        public List<CorrelationIdProvider> Providers { get; } = [];
        public List<Guid> ServiceInstanceIds { get; } = [];
        public List<Call> Calls { get; } = [];
    }

    private sealed class CapturingMessageHandler(
        ICorrelationIdProvider correlationIdProvider,
        Guid instanceId,
        List<Call> calls,
        Guid? failureId) : ISqsMessageHandler<FillRequestMessage>
    {
        public Task ProcessAsync(
            FillRequestMessage message,
            string? messageGroupId,
            CancellationToken cancellationToken)
        {
            calls.Add(new Call(
                message.FillId,
                correlationIdProvider.CorrelationId,
                cancellationToken,
                instanceId));
            if (message.FillId == failureId)
            {
                throw new InvalidOperationException("Synthetic processor failure.");
            }

            return Task.CompletedTask;
        }
    }

    private sealed record Call(
        Guid FillId,
        string? CorrelationId,
        CancellationToken CancellationToken,
        Guid ServiceInstanceId);
}
