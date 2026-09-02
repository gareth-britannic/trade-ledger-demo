FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

COPY src/TradeLedger.Common/TradeLedger.Common.csproj src/TradeLedger.Common/
COPY src/TradeLedger.Domain/TradeLedger.Domain.csproj src/TradeLedger.Domain/
COPY src/TradeLedger.Application/TradeLedger.Application.csproj src/TradeLedger.Application/
COPY src/TradeLedger.Database/TradeLedger.Database.csproj src/TradeLedger.Database/
COPY src/TradeLedger.Api/TradeLedger.Api.csproj src/TradeLedger.Api/
RUN dotnet restore src/TradeLedger.Api/TradeLedger.Api.csproj

COPY src/ src/
RUN dotnet publish src/TradeLedger.Api/TradeLedger.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
RUN apt-get update \
    && apt-get install --yes --no-install-recommends libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*
COPY --from=build /app .

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID

ENTRYPOINT ["dotnet", "TradeLedger.Api.dll"]
