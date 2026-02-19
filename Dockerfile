# Dockerfile for xmltvguide-generator

# Use .NET 8 SDK to build the app
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

COPY . .

RUN dotnet publish xmltvguide-generator.csproj -c Release -o /app/out

# Runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app

# Install cron for potential future use
RUN apt-get update && apt-get install -y cron curl && apt-get clean

# Copy published app
COPY --from=build /app/out .

# Copy wwwroot for static files
COPY src/wwwroot ./wwwroot

# copy config files
COPY ChannelMap.json /app/ChannelMap.json
COPY epg_urls.txt /app/epg_urls.txt

# Copy cron config and entry script
COPY crontab.txt /etc/cron.d/epg-cron
COPY cron-wrapper.sh /app/cron-wrapper.sh
COPY entrypoint.sh /entrypoint.sh

# Set permissions
RUN chmod 0644 /etc/cron.d/epg-cron && crontab /etc/cron.d/epg-cron
RUN chmod +x /entrypoint.sh
RUN chmod +x /app/cron-wrapper.sh

# Create output directory
RUN mkdir -p /app/output
RUN mkdir -p /app/logs

# Expose port for web hosting
EXPOSE 80

# Start nginx, cron, and the app
CMD ["/entrypoint.sh"]
