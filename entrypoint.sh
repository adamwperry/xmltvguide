#!/bin/bash

# make sure the log exists
touch /var/log/cron.log

# log time to confirm startup
echo "[entrypoint] Started at $(date -u)" >> /var/log/cron.log

# Debug: log EPG_URL_FILES existence and contents
if [[ -n "$EPG_URL_FILES" ]]; then
    echo "[entrypoint] EPG_URL_FILES is set to: $EPG_URL_FILES" >> /var/log/cron.log
    if [[ -f "$EPG_URL_FILES" ]]; then
        echo "[entrypoint] $EPG_URL_FILES exists. Contents:" >> /var/log/cron.log
        cat "$EPG_URL_FILES" >> /var/log/cron.log
        # Try paste first, then fallback to tr if paste fails
        paste_output=$(paste -sd, "$EPG_URL_FILES")
        echo "[entrypoint] paste output: $paste_output" >> /var/log/cron.log
        if [[ -z "$paste_output" ]]; then
            tr_output=$(tr '\n' ',' < "$EPG_URL_FILES" | sed 's/,$//')
            echo "[entrypoint] tr output: $tr_output" >> /var/log/cron.log
            export EPG_URL="$tr_output"
        else
            export EPG_URL="$paste_output"
        fi
        echo "[entrypoint] EPG_URL set to: $EPG_URL" >> /var/log/cron.log
    else
        echo "[entrypoint] $EPG_URL_FILES does NOT exist." >> /var/log/cron.log
    fi
else
    echo "[entrypoint] EPG_URL_FILES not set." >> /var/log/cron.log
fi

echo "EPG_URL: $EPG_URL" >> /var/log/cron.log
echo "CHANNEL_MAP_PATH: $CHANNEL_MAP_PATH" >> /var/log/cron.log
echo "OUTPUT_PATH: $OUTPUT_PATH" >> /var/log/cron.log
echo "EPG_URL_FILES: $EPG_URL_FILES" >> /var/log/cron.log

ARGS=""
if [[ -n "$EPG_URL" ]]; then
    ARGS="$ARGS --url=\"$EPG_URL\""
fi
if [[ -n "$CHANNEL_MAP_PATH" ]]; then
    ARGS="$ARGS --channelmap=\"$CHANNEL_MAP_PATH\""
fi
if [[ -n "$OUTPUT_PATH" ]]; then
    ARGS="$ARGS --output=\"$OUTPUT_PATH\""
fi
if [[ -n "$EPG_URL_FILES" ]]; then
    ARGS="$ARGS --epgUrlFiles=\"$EPG_URL_FILES\""
fi
if [[ -n "$FAKE" ]]; then
    ARGS="$ARGS --fake"
fi

echo "[entrypoint] Final ARGS: $ARGS" >> /var/log/cron.log

# Updated: Run app if EPG_URL or EPG_URL_FILES is set
if [[ -n "$EPG_URL" || -n "$EPG_URL_FILES" ]]; then
    echo "[entrypoint] Running: dotnet /app/xmltvguide-generator.dll $ARGS" >> /var/log/cron.log
    eval dotnet /app/xmltvguide-generator.dll $ARGS >> /var/log/cron.log 2>&1
else
    echo "[entrypoint] No EPG_URL or EPG_URL_FILES to pass. Skipping app startup." >> /var/log/cron.log
fi

# start cron and nginx
service cron start
service nginx start
service cron status

# keep container alive and tail log
tail -f /var/log/cron.log
