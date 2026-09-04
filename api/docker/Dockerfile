# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY Directory.Build.props ./
COPY Dpm.sln ./
COPY src/ src/
COPY tests/ tests/

RUN dotnet restore Dpm.sln
RUN dotnet publish src/Dpm.Api/Dpm.Api.csproj -c Release -o /app/publish --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

RUN adduser --disabled-password --gecos "" --uid 10001 dpm
USER 10001

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 \
    CMD ["dotnet", "--info"]

ENTRYPOINT ["dotnet", "Dpm.Api.dll"]
