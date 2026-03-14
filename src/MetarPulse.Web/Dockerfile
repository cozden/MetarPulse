# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["src/MetarPulse.Web/MetarPulse.Web.csproj",             "src/MetarPulse.Web/"]
COPY ["src/MetarPulse.Core/MetarPulse.Core.csproj",           "src/MetarPulse.Core/"]
COPY ["src/MetarPulse.Abstractions/MetarPulse.Abstractions.csproj", "src/MetarPulse.Abstractions/"]
RUN dotnet restore "src/MetarPulse.Web/MetarPulse.Web.csproj"

COPY . .
RUN dotnet publish "src/MetarPulse.Web/MetarPulse.Web.csproj" \
    -c Release -o /app/publish --no-restore

# Serve stage — nginx ile statik dosyaları sun
FROM nginx:alpine AS runtime
COPY --from=build /app/publish/wwwroot /usr/share/nginx/html
COPY src/MetarPulse.Web/nginx.conf /etc/nginx/conf.d/default.conf

EXPOSE 80
