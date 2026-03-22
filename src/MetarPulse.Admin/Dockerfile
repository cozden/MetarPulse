# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0.103 AS build
WORKDIR /src

COPY ["src/MetarPulse.Admin/MetarPulse.Admin.csproj",             "src/MetarPulse.Admin/"]
COPY ["src/MetarPulse.Core/MetarPulse.Core.csproj",               "src/MetarPulse.Core/"]
COPY ["src/MetarPulse.Abstractions/MetarPulse.Abstractions.csproj", "src/MetarPulse.Abstractions/"]
COPY ["src/MetarPulse.Infrastructure/MetarPulse.Infrastructure.csproj", "src/MetarPulse.Infrastructure/"]
RUN dotnet restore "src/MetarPulse.Admin/MetarPulse.Admin.csproj"

COPY . .
RUN dotnet publish "src/MetarPulse.Admin/MetarPulse.Admin.csproj" \
    -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

EXPOSE 5001
ENV ASPNETCORE_URLS=http://+:5001
ENTRYPOINT ["dotnet", "MetarPulse.Admin.dll"]
