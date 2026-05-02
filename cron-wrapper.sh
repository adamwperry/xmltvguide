#!/bin/bash

# Cron wrapper script that logs runs via API
LOG_URL="http://localhost/api/cronlogs/log"
TIMESTAMP=$(date -u +"%Y-%m-%dT%H:%M:%S.000Z")

if [ -f /app/config/cron.env ]; then
    set -a
    # shellcheck disable=SC1091
    . /app/config/cron.env
    set +a
fi

# Function to log to API
log_to_api() {
    local success=$1
    local message=$2
    local error_message=$3
    
    # Escape quotes and newlines in the message
    message=$(echo "$message" | sed 's/"/\\"/g' | tr '\n' ' ')
    if [ -n "$error_message" ]; then
        error_message=$(echo "$error_message" | sed 's/"/\\"/g' | tr '\n' ' ')
        error_json="\"$error_message\""
    else
        error_json="null"
    fi
    
    # Create JSON payload
    local json_payload=$(cat <<EOF
{
    "message": "$message",
    "timestamp": "$TIMESTAMP",
    "success": $success,
    "errorMessage": $error_json
}
EOF
)
    
    local token_header=()
    if [ -n "$CRON_LOG_TOKEN" ]; then
        token_header=(-H "X-Cron-Log-Token: $CRON_LOG_TOKEN")
    fi

    # Try to log to API (don't let this fail the script)
    curl -s -X POST "$LOG_URL" \
        -H "Content-Type: application/json" \
        "${token_header[@]}" \
        -d "$json_payload" > /dev/null 2>&1 || true
}

# Set defaults when the runtime environment file is not available.
export EPG_URL_FILES="${EPG_URL_FILES:-/app/epg_urls.txt}"
export CHANNEL_MAP_PATH="${CHANNEL_MAP_PATH:-/app/ChannelMap.json}"
export SETTINGS_PATH="${SETTINGS_PATH:-/app/settings.json}"
export OUTPUT_PATH="${OUTPUT_PATH:-/app/output/guide.xml}"

case "${USE_CHANNEL_NAMES_INSTEAD_OF_NUMERIC_IDS:-}" in
    [Tt][Rr][Uu][Ee]|1|[Yy][Ee][Ss])
        export STRIP_CHANNEL_NUMBERS="${STRIP_CHANNEL_NUMBERS:-true}"
        ;;
esac

echo "[$TIMESTAMP] Starting EPG generation cron job"
echo "[$TIMESTAMP] EPG_URL_FILES=$EPG_URL_FILES"
echo "[$TIMESTAMP] CHANNEL_MAP_PATH=$CHANNEL_MAP_PATH"
echo "[$TIMESTAMP] SETTINGS_PATH=$SETTINGS_PATH"
echo "[$TIMESTAMP] OUTPUT_PATH=$OUTPUT_PATH"
echo "[$TIMESTAMP] USE_CHANNEL_NAMES_INSTEAD_OF_NUMERIC_IDS=${USE_CHANNEL_NAMES_INSTEAD_OF_NUMERIC_IDS:-}"
echo "[$TIMESTAMP] STRIP_CHANNEL_NUMBERS=${STRIP_CHANNEL_NUMBERS:-}"
echo "[$TIMESTAMP] SORT_CHANNELS_BY_ID=${SORT_CHANNELS_BY_ID:-}"

# Run the .NET application
if /usr/bin/dotnet /app/xmltvguide-generator.dll; then
    SUCCESS_MESSAGE="EPG generation completed successfully at $TIMESTAMP"
    echo "[$TIMESTAMP] $SUCCESS_MESSAGE"
    log_to_api true "$SUCCESS_MESSAGE" ""
else
    ERROR_MESSAGE="EPG generation failed at $TIMESTAMP"
    echo "[$TIMESTAMP] $ERROR_MESSAGE"
    log_to_api false "EPG generation failed" "$ERROR_MESSAGE"
fi
