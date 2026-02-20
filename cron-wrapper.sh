#!/bin/bash

# Cron wrapper script that logs runs via API
LOG_URL="http://localhost/api/cronlogs/log"
TIMESTAMP=$(date -u +"%Y-%m-%dT%H:%M:%S.000Z")

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
    
    # Try to log to API (don't let this fail the script)
    curl -s -X POST "$LOG_URL" \
        -H "Content-Type: application/json" \
        -d "$json_payload" > /dev/null 2>&1 || true
}

# Set environment variables
export EPG_URL_FILES="/app/epg_urls.txt"
export CHANNEL_MAP_PATH="/app/ChannelMap.json"
export OUTPUT_PATH="/app/output/guide.xml"

echo "[$TIMESTAMP] Starting EPG generation cron job"

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