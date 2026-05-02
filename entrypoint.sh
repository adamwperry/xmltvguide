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
echo "SETTINGS_PATH: $SETTINGS_PATH" >> /var/log/cron.log
echo "USE_CHANNEL_NAMES_INSTEAD_OF_NUMERIC_IDS: $USE_CHANNEL_NAMES_INSTEAD_OF_NUMERIC_IDS" >> /var/log/cron.log
echo "STRIP_CHANNEL_NUMBERS: $STRIP_CHANNEL_NUMBERS" >> /var/log/cron.log
echo "SORT_CHANNELS_BY_ID: $SORT_CHANNELS_BY_ID" >> /var/log/cron.log
echo "RUN_AS_WEB: $RUN_AS_WEB" >> /var/log/cron.log

case "${USE_CHANNEL_NAMES_INSTEAD_OF_NUMERIC_IDS,,}" in
    true|1|yes)
        if [[ -z "${STRIP_CHANNEL_NUMBERS:-}" ]]; then
            export STRIP_CHANNEL_NUMBERS=true
        fi
        ;;
esac

write_cron_env() {
    mkdir -p /app/config
    {
        echo "EPG_URL=${EPG_URL:-}"
        echo "EPG_URL_FILES=${EPG_URL_FILES:-/app/epg_urls.txt}"
        echo "CHANNEL_MAP_PATH=${CHANNEL_MAP_PATH:-/app/ChannelMap.json}"
        echo "SETTINGS_PATH=${SETTINGS_PATH:-/app/settings.json}"
        echo "OUTPUT_PATH=${OUTPUT_PATH:-/app/output/guide.xml}"
        echo "USE_CHANNEL_NAMES_INSTEAD_OF_NUMERIC_IDS=${USE_CHANNEL_NAMES_INSTEAD_OF_NUMERIC_IDS:-}"
        echo "STRIP_CHANNEL_NUMBERS=${STRIP_CHANNEL_NUMBERS:-}"
        echo "SORT_CHANNELS_BY_ID=${SORT_CHANNELS_BY_ID:-}"
        echo "CRON_LOG_TOKEN=${CRON_LOG_TOKEN:-}"
    } > /app/config/cron.env
    chmod 600 /app/config/cron.env
    echo "[entrypoint] Wrote cron environment to /app/config/cron.env" >> /var/log/cron.log
}

# Build command line arguments for headless mode
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
if [[ "$USE_CHANNEL_NAMES_INSTEAD_OF_NUMERIC_IDS" == "true" || "$STRIP_CHANNEL_NUMBERS" == "true" ]]; then
    ARGS="$ARGS --strip-channel-numbers"
fi
if [[ "$SORT_CHANNELS_BY_ID" == "false" ]]; then
    ARGS="$ARGS --preserve-channel-order"
fi
if [[ -n "$FAKE" ]]; then
    ARGS="$ARGS --fake"
fi

echo "[entrypoint] Final ARGS: $ARGS" >> /var/log/cron.log

# Check if running in web mode or headless mode
if [[ "$RUN_AS_WEB" == "true" ]]; then
    echo "[entrypoint] Running in WEB mode" >> /var/log/cron.log
    
    # Set environment for web hosting
    export ASPNETCORE_ENVIRONMENT=Production
    write_cron_env
    
    # Start cron daemon in the background
    echo "[entrypoint] Starting cron daemon..." >> /var/log/cron.log
    service cron start
    
    # Start the .NET application as web server
    echo "[entrypoint] Starting .NET web application..." >> /var/log/cron.log
    exec dotnet /app/xmltvguide-generator.dll
else
    echo "[entrypoint] Running in HEADLESS mode" >> /var/log/cron.log
    
    # Run one-time EPG generation with CLI arguments
    echo "[entrypoint] Executing: dotnet /app/xmltvguide-generator.dll $ARGS" >> /var/log/cron.log
    eval "dotnet /app/xmltvguide-generator.dll $ARGS"
    
    # Exit after completion (don't keep container running)
    echo "[entrypoint] Headless execution completed at $(date -u)" >> /var/log/cron.log
fi
