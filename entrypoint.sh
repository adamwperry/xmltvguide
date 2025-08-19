#!/bin/bash

# make sure the log exists
touch /var/log/cron.log

# log time to confirm startup
echo "[entrypoint] Started at $(date -u)" >> /var/log/cron.log

# if EPG_URL_FILES is set and file exists, read it
if [[ -n "$EPG_URL_FILES" && -f "$EPG_URL_FILES" ]]; then
    echo "[entrypoint] found $EPG_URL_FILES; exporting EPG_URL" >> /var/log/cron.log
    export EPG_URL=$(paste -sd, "$EPG_URL_FILES")
else
    echo "[entrypoint] $EPG_URL_FILES not found or not set; skipping EPG_URL export" >> /var/log/cron.log
fi

# show what we're using
echo "EPG_URL: $EPG_URL" >> /var/log/cron.log
echo "CHANNEL_MAP_PATH: $CHANNEL_MAP_PATH" >> /var/log/cron.log
echo "OUTPUT_PATH: $OUTPUT_PATH" >> /var/log/cron.log

# run the app if EPG_URL is present
if [[ -n "$EPG_URL" ]]; then
    dotnet /app/xmltvguide-generator.dll >> /var/log/cron.log 2>&1
else
    echo "[entrypoint] No URLs to pass. Skipping app startup." >> /var/log/cron.log
fi

# start cron and nginx
service cron start
service nginx start

# keep container alive and tail log
tail -f /var/log/cron.log
