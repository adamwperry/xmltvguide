#!/bin/bash

# make sure the log exists
touch /var/log/cron.log

# log time to confirm startup
echo "[entrypoint] Started at $(date -u)" >> /var/log/cron.log

# start cron and nginx
service cron start
service nginx start

# run app once at startup
dotnet /app/xmltvguide-generator.dll >> /var/log/cron.log 2>&1

# keep container alive and tail log
tail -f /var/log/cron.log
