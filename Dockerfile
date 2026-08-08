# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /build
COPY CloudSealed-Predictive-ML-Core.slnx ./
COPY src/CloudSealed.ML.Engine/CloudSealed.ML.Engine.csproj src/CloudSealed.ML.Engine/
COPY src/CloudSealed.ML.API/CloudSealed.ML.API.csproj src/CloudSealed.ML.API/
COPY src/CloudSealed.ML.CLI/CloudSealed.ML.CLI.csproj src/CloudSealed.ML.CLI/
COPY tests/CloudSealed.ML.Tests/CloudSealed.ML.Tests.csproj tests/CloudSealed.ML.Tests/
RUN dotnet restore src/CloudSealed.ML.API/CloudSealed.ML.API.csproj

COPY src/ src/
RUN dotnet publish src/CloudSealed.ML.API/CloudSealed.ML.API.csproj \
    --no-restore -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0
LABEL org.opencontainers.image.source="https://github.com/cloudsealed/Predictive-ML-Core" \
      org.opencontainers.image.description="Architecture risk scoring service" \
      org.opencontainers.image.licenses="MIT"

# Run unprivileged: the service parses untrusted architecture inventory payloads.
RUN useradd --create-home --uid 10001 engine \
    && apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app/publish .
USER engine

EXPOSE 8092
ENV PORT=8092

HEALTHCHECK --interval=30s --timeout=3s --start-period=20s \
    CMD curl -f http://127.0.0.1:8092/health || exit 1

ENTRYPOINT ["dotnet", "CloudSealed.ML.API.dll"]
