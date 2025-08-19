# Dockerfile for xmltvguide-generator

# Use .NET 6 SDK to build the app
FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /app

COPY . .

RUN dotnet publish xmltvguide-generator.sln -c Release -o /app/out

# Runtime image
FROM mcr.microsoft.com/dotnet/aspnet:6.0
WORKDIR /app

# Install nginx and cron
RUN apt-get update && apt-get install -y nginx cron && rm /etc/nginx/sites-enabled/default

# Copy published app
COPY --from=build /app/out .

# copy config files
COPY ChannelMap.json /app/ChannelMap.json
COPY epg_urls.txt /app/epg_urls.txt

# Copy cron config and entry script
COPY crontab.txt /etc/cron.d/epg-cron
COPY entrypoint.sh /entrypoint.sh

# Set permissions
RUN chmod 0644 /etc/cron.d/epg-cron && crontab /etc/cron.d/epg-cron
RUN chmod +x /entrypoint.sh

# Configure nginx
RUN echo 'server { listen 80; root /app/output; index guide.xml; location / { try_files $uri $uri/ =404; } }' > /etc/nginx/sites-available/xmltvguide \
    && ln -s /etc/nginx/sites-available/xmltvguide /etc/nginx/sites-enabled/default

# Expose port for web hosting
EXPOSE 80

# Start nginx, cron, and the app
CMD ["/entrypoint.sh"]
