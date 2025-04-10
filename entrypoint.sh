#!/bin/bash

# Start cron and nginx
service cron start
service nginx start

# Create and tail cron log in the background
touch /var/log/cron.log
tail -f /var/log/cron.log &

# Run the app once on container start
dotnet /app/xmltvguide-generator.dll

# Keep the container alive
tail -f /dev/null
