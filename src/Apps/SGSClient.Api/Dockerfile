# Build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props ./
COPY src/Core/SGSClient.Core/SGSClient.Core.csproj src/Core/SGSClient.Core/
COPY src/Infrastructure/SGSClient.Infrastructure.Server/SGSClient.Infrastructure.Server.csproj src/Infrastructure/SGSClient.Infrastructure.Server/
COPY src/Apps/SGSClient.Api/SGSClient.Api.csproj src/Apps/SGSClient.Api/
RUN dotnet restore src/Apps/SGSClient.Api/SGSClient.Api.csproj
COPY src/Core/SGSClient.Core/ src/Core/SGSClient.Core/
COPY src/Infrastructure/SGSClient.Infrastructure.Server/ src/Infrastructure/SGSClient.Infrastructure.Server/
COPY src/Apps/SGSClient.Api/ src/Apps/SGSClient.Api/
RUN dotnet publish src/Apps/SGSClient.Api/SGSClient.Api.csproj -c Release -o /app/publish --no-restore

# Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/* \
    && mkdir -p /data/sgs/games
ENV ASPNETCORE_URLS=http://+:8080
ENV Games__StoragePath=/data/sgs/games
ENV Games__PublicBaseUrl=/api/games/packages
ENV HttpsRedirection__Enabled=false
EXPOSE 8080
COPY --from=build /app/publish .
VOLUME ["/data/sgs/games"]
HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 \
  CMD curl -fsS http://127.0.0.1:8080/health || exit 1
ENTRYPOINT ["dotnet", "SGSClient.Api.dll"]
