# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore — sadece csproj'ları kopyala (layer cache optimizasyonu)
COPY ["src/MetarPulse.Api/MetarPulse.Api.csproj",             "src/MetarPulse.Api/"]
COPY ["src/MetarPulse.Core/MetarPulse.Core.csproj",           "src/MetarPulse.Core/"]
COPY ["src/MetarPulse.Abstractions/MetarPulse.Abstractions.csproj", "src/MetarPulse.Abstractions/"]
COPY ["src/MetarPulse.Infrastructure/MetarPulse.Infrastructure.csproj", "src/MetarPulse.Infrastructure/"]
RUN dotnet restore "src/MetarPulse.Api/MetarPulse.Api.csproj"

# Publish
COPY . .
RUN dotnet publish "src/MetarPulse.Api/MetarPulse.Api.csproj" \
    -c Release -o /app/publish --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

EXPOSE 5000
ENV ASPNETCORE_URLS=http://+:5000
ENTRYPOINT ["dotnet", "MetarPulse.Api.dll"]
