using System.Text.Json;
using Amazon.Runtime;
using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Shouldly;
using TradeLedger.Application;
using TradeLedger.Application.Interfaces;
using TradeLedger.Application.Messaging;
using TradeLedger.Application.Records;
using TradeLedger.Common;
using TradeLedger.Database;
using TradeLedger.Domain;
using Xunit;

namespace TradeLedger.IntegrationTests.Processor;

public sealed class LocalStackOrderingTests
{
    private const string LocalStackEndpoint = "http://localhost:4566";
    private const string QueueName = "trade-ledger-fills.fifo";
    private const string ConnectionString =
        "Host=localhost;Port=55432;Database=trade_ledger;Username=trade_ledger;Password=trade_ledger";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [LocalStackFact]
    [Trait("Category", "External")]
    public async Task EventSourceMapping_ReplaysOutOfArrivalOrderAndRedeliveryIsIdempotent()
    {
        using var sqs = CreateSqsClient();
        var queueUrl = (await sqs.GetQueueUrlAsync(QueueName)).QueueUrl;
        await using var services = CreateServices(queueUrl);
        await MigrateAsync(services);

        var symbol = $"IT{Guid.NewGuid():N}"[..14].ToUpperInvariant();
        var baseTime = new DateTimeOffset(2026, 9, 1, 10, 0, 0, TimeSpan.Zero);
        var laterBuy = Fill.Create(Guid.NewGuid(), symbol, Side.Buy, 100m, 10m, baseTime.AddMinutes(2));
        var earlierBuy = Fill.Create(Guid.NewGuid(), symbol, Side.Buy, 100m, 20m, baseTime.AddMinutes(1));
        var sell = Fill.Create(Guid.NewGuid(), symbol, Side.Sell, 100m, 30m, baseTime.AddMinutes(3));

        // SQS receives the £10 buy first even though the £20 buy executed first.
        await AcceptAsync(services, laterBuy, "ordering-later-buy");
        await AcceptAsync(services, earlierBuy, "ordering-earlier-buy");
        await AcceptAsync(services, sell, "ordering-sell");

        var applied = await WaitForLedgerAsync(services, symbol, [laterBuy.Id, earlierBuy.Id, sell.Id]);
        applied.ProcessedAt.Values.ShouldAllBe(timestamp => timestamp.HasValue);
        applied.Lots.ShouldBe([
            new PersistedLot(laterBuy.Id, 100m, 10m, laterBuy.ExecutedAt)
        ]);
        applied.OpenQuantity.ShouldBe(100m);
        applied.RealisedPnl.ShouldBe(1000m);

        var originalProcessedAt = applied.ProcessedAt[sell.Id];
        await sqs.SendMessageAsync(new SendMessageRequest
        {
            QueueUrl = queueUrl,
            MessageBody = JsonSerializer.Serialize(new FillRequestMessage(
                sell.Id,
                sell.Symbol,
                sell.Side.ToString(),
                sell.Quantity,
                sell.Price,
                sell.ExecutedAt), JsonOptions),
            MessageGroupId = symbol,
            MessageDeduplicationId = $"redelivery-{Guid.NewGuid():N}",
            MessageAttributes = new Dictionary<string, MessageAttributeValue>
            {
                ["CorrelationId"] = new()
                {
                    DataType = "String",
                    StringValue = "ordering-redelivery"
                }
            }
        });
        await WaitForQueueToDrainAsync(sqs, queueUrl);

        var redelivered = await ReadLedgerAsync(services, symbol, [laterBuy.Id, earlierBuy.Id, sell.Id]);
        redelivered.Lots.ShouldBe(applied.Lots);
        redelivered.OpenQuantity.ShouldBe(applied.OpenQuantity);
        redelivered.RealisedPnl.ShouldBe(applied.RealisedPnl);
        redelivered.ProcessedAt[sell.Id].ShouldBe(originalProcessedAt);
    }

    private static async Task AcceptAsync(
        ServiceProvider services,
        Fill fill,
        string correlationId)
    {
        await using var scope = services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ICorrelationIdProvider>().CorrelationId = correlationId;
        await scope.ServiceProvider.GetRequiredService<IFillRequestService>().CreateAsync(
            new CreateFillCommand(
                fill.Id,
                fill.Symbol,
                fill.Side,
                fill.Quantity,
                fill.Price,
                fill.ExecutedAt),
            CancellationToken.None);
    }

    private static async Task<LedgerSnapshot> WaitForLedgerAsync(
        ServiceProvider services,
        string symbol,
        IReadOnlyCollection<Guid> fillIds)
    {
        var timeoutAt = DateTimeOffset.UtcNow.AddSeconds(90);
        LedgerSnapshot? latest = null;
        while (DateTimeOffset.UtcNow < timeoutAt)
        {
            latest = await ReadLedgerAsync(services, symbol, fillIds);
            if (latest.ProcessedAt.Count == fillIds.Count &&
                latest.ProcessedAt.Values.All(timestamp => timestamp is not null))
            {
                return latest;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(250));
        }

        throw new TimeoutException(
            $"LocalStack did not apply all fills within the bounded timeout. Last state: {latest}");
    }

    private static async Task WaitForQueueToDrainAsync(IAmazonSQS sqs, string queueUrl)
    {
        var timeoutAt = DateTimeOffset.UtcNow.AddSeconds(60);
        while (DateTimeOffset.UtcNow < timeoutAt)
        {
            var attributes = await sqs.GetQueueAttributesAsync(new GetQueueAttributesRequest
            {
                QueueUrl = queueUrl,
                AttributeNames = ["ApproximateNumberOfMessages", "ApproximateNumberOfMessagesNotVisible"]
            });
            var visible = int.Parse(attributes.Attributes["ApproximateNumberOfMessages"]);
            var inFlight = int.Parse(attributes.Attributes["ApproximateNumberOfMessagesNotVisible"]);
            if (visible == 0 && inFlight == 0)
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(250));
        }

        throw new TimeoutException("The redelivery did not leave the FIFO queue within the bounded timeout.");
    }

    private static async Task<LedgerSnapshot> ReadLedgerAsync(
        ServiceProvider services,
        string symbol,
        IReadOnlyCollection<Guid> fillIds)
    {
        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<TradeLedgerDbContext>();
        var fills = await context.Fills
            .AsNoTracking()
            .Where(fill => fillIds.Contains(fill.Id))
            .ToDictionaryAsync(fill => fill.Id, fill => fill.ProcessedAt);
        var lots = await context.Lots
            .AsNoTracking()
            .Where(lot => lot.Symbol == symbol)
            .OrderBy(lot => lot.OpenedAt)
            .ThenBy(lot => lot.Id)
            .Select(lot => new PersistedLot(lot.Id, lot.RemainingQuantity, lot.UnitCost, lot.OpenedAt))
            .ToListAsync();
        var position = await context.Positions
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Symbol == symbol);
        return new LedgerSnapshot(
            fills,
            lots,
            position is null ? null : lots.Sum(lot => lot.RemainingQuantity),
            position?.RealisedPnl);
    }

    private static async Task MigrateAsync(ServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<TradeLedgerDbContext>()
            .Database.MigrateAsync();
    }

    private static ServiceProvider CreateServices(string queueUrl)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:ConnectionString"] = ConnectionString,
                ["FillQueue:Url"] = queueUrl,
                ["AWS:ServiceUrl"] = LocalStackEndpoint,
                ["AWS:Region"] = "eu-west-2",
                ["AWS:AccessKey"] = "test",
                ["AWS:SecretKey"] = "test"
            })
            .Build();
        var services = new ServiceCollection();
        var environment = new LocalHostEnvironment();
        services.AddLogging();
        services.AddCommon();
        services.AddSqsClient(configuration, environment);
        services.AddApplication();
        services.AddDatabase(configuration);
        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true
        });
    }

    private static AmazonSQSClient CreateSqsClient() => new(
        new BasicAWSCredentials("test", "test"),
        new AmazonSQSConfig
        {
            ServiceURL = LocalStackEndpoint,
            AuthenticationRegion = "eu-west-2"
        });

    private sealed class LocalHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = nameof(LocalStackOrderingTests);
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed record PersistedLot(
        Guid Id,
        decimal RemainingQuantity,
        decimal UnitCost,
        DateTimeOffset OpenedAt);

    private sealed record LedgerSnapshot(
        IReadOnlyDictionary<Guid, DateTimeOffset?> ProcessedAt,
        IReadOnlyList<PersistedLot> Lots,
        decimal? OpenQuantity,
        decimal? RealisedPnl);
}

public sealed class LocalStackFactAttribute : FactAttribute
{
    public LocalStackFactAttribute()
    {
        if (!string.Equals(
                Environment.GetEnvironmentVariable("TRADE_LEDGER_RUN_LOCALSTACK_INTEGRATION"),
                "1",
                StringComparison.Ordinal))
        {
            Skip = "Run deploy/scripts/bootstrap-all.sh and set TRADE_LEDGER_RUN_LOCALSTACK_INTEGRATION=1.";
        }
    }
}
