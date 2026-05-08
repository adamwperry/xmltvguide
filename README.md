# XMLTV Guide Generator

<img width="1468" height="798" alt="image" src="https://github.com/user-attachments/assets/b3889804-b683-4a56-930b-48e8dffdf98b" />

XMLTV Guide Generator builds an XMLTV-compatible `guide.xml` from one or more EPG source URLs and a channel map. It can run once in headless mode, or as a persistent web UI for managing sources, channel mappings, previews, rebuilds, health checks, cron runs, and config backups.

## Features

- Web dashboard for guide health, EPG source count, configured channel count, guide size, last update, and scheduled rebuild status.
- EPG source editor with reload, save, validation, source testing, and multi-source support.
- Channel mapping editor for `ChannelMap.json`.
- Channel mapping preview modal with combined/source-level aggregation, mapped/unmapped counts, source details, and preview search/filtering.
- TV guide preview modal for browsing generated guide data by channel or show.
- Manual rebuild with progress/status polling, cancellation, and rebuild history.
- Cron run history with clear action and schedule visibility.
- Config backup/restore for EPG URLs and channel map.
- Public health endpoint at `/health` and authenticated config/rebuild APIs.

## Web UI Quick Start

```bash
docker build -t xmltvguide-generator .

docker run --rm \
  -e RUN_AS_WEB=true \
  -e AUTH_USERNAME=admin \
  -e AUTH_PASSWORD=changeme \
  -e AUTH_EMAIL=admin@xmltvguide.local \
  -e EPG_URL_FILES=/app/epg_urls.txt \
  -e CHANNEL_MAP_PATH=/app/ChannelMap.json \
  -e OUTPUT_PATH=/app/output/guide.xml \
  -v $(pwd)/epg_urls.txt:/app/epg_urls.txt \
  -v $(pwd)/ChannelMap.json:/app/ChannelMap.json \
  -v $(pwd)/output:/app/output \
  -p 8585:80 \
  xmltvguide-generator
```

Then open:

- Web UI: `http://localhost:8585`
- XMLTV guide: `http://localhost:8585/guide.xml`
- Health check: `http://localhost:8585/health`

Cron is enabled in web mode and runs EPG updates every 20 minutes by default. Edit `crontab.txt` to change the schedule.

### Web UI Authentication

The web UI reads login credentials from environment variables first, then falls back to `appsettings.json`:

- `AUTH_USERNAME`
- `AUTH_PASSWORD`
- `AUTH_EMAIL`

For Docker Compose, `docker-compose.yml` uses shell or `.env` values when present and falls back to local defaults:

```yaml
AUTH_USERNAME=${AUTH_USERNAME:-admin}
AUTH_PASSWORD=${AUTH_PASSWORD:-changeme}
AUTH_EMAIL=${AUTH_EMAIL:-admin@xmltvguide.local}
```

To override without editing the compose file, create a `.env` file next to `docker-compose.yml`:

```env
AUTH_USERNAME=admin
AUTH_PASSWORD=replace-with-a-strong-password
AUTH_EMAIL=admin@example.com
```

## Configuration Files

### `epg_urls.txt`

One EPG source URL per line:

```txt
https://example.com/api/time={unixtime}
https://example.com/xmltv/guide?zip_code=30303
```

### `ChannelMap.json`

Maps provider/channel identifiers into display names Emby can match reliably:

```json
{
  "channels": [
    {
      "channel": {
        "name": "EXAMPLE NETWORK",
        "channelId": "21760"
      }
    }
  ]
}
```

The UI validation warns about duplicate `channelId` values, blank channel IDs, and blank names.

### `settings.json`

Persists UI and build settings:

```json
{
  "channel": {
    "useChannelNamesInsteadOfNumericIds": false,
    "sortChannelsByIdThenDisplayName": true
  }
}
```

Docker Compose can also set these values with environment variables:

```env
USE_CHANNEL_NAMES_INSTEAD_OF_NUMERIC_IDS=false
SORT_CHANNELS_BY_ID=true
```

### CORS

Most deployments do not need custom CORS settings because the web UI and API are served from the same origin. If you host a separate frontend that calls this API from another origin, set `CORS_ALLOWED_ORIGINS` as a comma-separated list:

```env
CORS_ALLOWED_ORIGINS=https://xmltv.example.com,http://192.168.1.50:8586
```

# Build & Deploy via Docker

## 1. Build Docker Image Locally

Make sure you're in the root of your project (where your `Dockerfile` is):

```bash
cd path/to/xmltvguide-generator
```

Then build the image:

```bash
docker build -t xmltvguide-generator .
```

---

## 2. Run Locally to Test

### 2a. Headless Mode

Runs once to generate the EPG XML file and then exits. Useful for testing or cron jobs.

**Single URL:**

```bash
docker run --rm \
  -e EPG_URL="https://example.com/api/time{unixtime}" \
  -e CHANNEL_MAP_PATH=/app/ChannelMap.json \
  -e OUTPUT_PATH=/app/output/guide.xml \
  -v $(pwd)/ChannelMap.json:/app/ChannelMap.json \
  -v $(pwd)/output:/app/output \
  xmltvguide-generator \
  --url="https://example.com/api/time{unixtime}" \
  --channelmap=/app/ChannelMap.json \
  --output=/app/output/guide.xml
```

**Multiple URLs from file:**

```bash
docker run --rm \
  -e CHANNEL_MAP_PATH=/app/ChannelMap.json \
  -e OUTPUT_PATH=/app/output/guide.xml \
  -v $(pwd)/epg_urls.txt:/app/epg_urls.txt \
  -v $(pwd)/ChannelMap.json:/app/ChannelMap.json \
  -v $(pwd)/output:/app/output \
  xmltvguide-generator \
  --epgUrlFiles=/app/epg_urls.txt \
  --channelmap=/app/ChannelMap.json \
  --output=/app/output/guide.xml
```

> After completion, the generated file will be in `./output/guide.xml`

### 2b. With Web UI

Runs a persistent web service with a management interface and scheduled EPG updates.

```bash
docker run --rm \
  -e RUN_AS_WEB=true \
  -e AUTH_USERNAME=admin \
  -e AUTH_PASSWORD=changeme \
  -e AUTH_EMAIL=admin@xmltvguide.local \
  -e EPG_URL_FILES=/app/epg_urls.txt \
  -e CHANNEL_MAP_PATH=/app/ChannelMap.json \
  -e OUTPUT_PATH=/app/output/guide.xml \
  -v $(pwd)/epg_urls.txt:/app/epg_urls.txt \
  -v $(pwd)/ChannelMap.json:/app/ChannelMap.json \
  -v $(pwd)/output:/app/output \
  -p 8585:80 \
  xmltvguide-generator
```

> **Access the service:**
>
> - **Web UI**: `http://localhost:8585`
> - **Guide XML**: `http://localhost:8585/guide.xml`
> - **Health Check**: `http://localhost:8585/health`
> - **API Endpoints**: `http://localhost:8585/api/config`, `http://localhost:8585/api/cronlogs`
> - **Config Backup**: export/restore from the Web UI

For Docker Compose deployments, set `AUTH_USERNAME`, `AUTH_PASSWORD`, and `AUTH_EMAIL` in a `.env` file or edit the values in `docker-compose.yml`. Environment variables override the defaults in `appsettings.json`.

**Note:** Cron is automatically enabled and runs EPG updates every 20 minutes (configurable in `crontab.txt`).

---

## 3. Export Image for Portainer Upload

If you want to move the image to a system running Portainer:

```bash
docker save xmltvguide-generator:latest | gzip > xmltvguide.tar.gz
```

Copy the `.tar.gz` to your Portainer host and load it:

```bash
docker load < xmltvguide.tar.gz
```

---

## 4. Create a .tar Archive with Brew Tar (macOS)

If you need to upload a bundle of files manually via Portainer:

```bash
brew install gnu-tar
gtar -cvf xmltvguide.tar Dockerfile crontab.txt cron-wrapper.sh entrypoint.sh src xmltvguide-generator.csproj xmltvguide-generator.sln ChannelMap.json epg_urls.txt
```

> Use `gtar` instead of macOS default `tar` to avoid xattr issues.

You can also compress it:

```bash
gtar -czvf xmltvguide.tar.gz Dockerfile crontab.txt cron-wrapper.sh entrypoint.sh src xmltvguide-generator.csproj xmltvguide-generator.sln ChannelMap.json epg_urls.txt
```

---

## 5. Deploy in Portainer

### A. Upload and Build the Image

1. Go to **Images** → **Build a new image**
2. Upload your `.tar.gz` or connect to a Git repo

### B. Add the Stack

1. Go to **Stacks** → **+ Add stack**
2. Name the stack `xmltvguide`
3. Choose one of the following configurations based on your needs:

#### Option 1: Minimal Configuration (Testing)

Quick setup with no persistence. Data is lost on container restart.

```yaml
version: "3.8"
services:
  xmltvguide:
    image: xmltvguide:latest
    container_name: xmltvguide
    ports:
      - "8585:80"
    restart: unless-stopped
    environment:
      - RUN_AS_WEB=true
      - CORS_ALLOWED_ORIGINS=
```

#### Option 2: Named Volumes (Recommended)

Data persists across restarts. Config files are in the image, editable via Web UI.

```yaml
version: "3.8"
services:
  xmltvguide:
    image: xmltvguide:latest
    container_name: xmltvguide
    ports:
      - "8585:80"
    restart: unless-stopped
    environment:
      - RUN_AS_WEB=true
      - EPG_URL_FILES=/app/epg_urls.txt
      - CHANNEL_MAP_PATH=/app/ChannelMap.json
      - SETTINGS_PATH=/app/settings.json
      - USE_CHANNEL_NAMES_INSTEAD_OF_NUMERIC_IDS=
      - SORT_CHANNELS_BY_ID=
      - CORS_ALLOWED_ORIGINS=
      - OUTPUT_PATH=/app/output/guide.xml
    volumes:
      - xmltvguide-config:/app/config
      - xmltvguide-output:/app/output
      - xmltvguide-logs:/app/logs

volumes:
  xmltvguide-config:
  xmltvguide-output:
  xmltvguide-logs:
```

#### Option 3: Host Path Volumes (Advanced)

Direct access to files on host. Update paths to match your server's directory structure.

```yaml
services:
  xmltvguide:
    image: xmltvguide:latest
    container_name: xmltvguide
    ports:
      - "8585:80"
    restart: unless-stopped
    environment:
      - RUN_AS_WEB=true
      - EPG_URL_FILES=/app/epg_urls.txt
      - CHANNEL_MAP_PATH=/app/ChannelMap.json
      - SETTINGS_PATH=/app/settings.json
      - USE_CHANNEL_NAMES_INSTEAD_OF_NUMERIC_IDS=
      - SORT_CHANNELS_BY_ID=
      - CORS_ALLOWED_ORIGINS=
      - OUTPUT_PATH=/app/output/guide.xml
    volumes:
      - /volume1/docker/xmltvguide/epg_urls.txt:/app/epg_urls.txt
      - /volume1/docker/xmltvguide/ChannelMap.json:/app/ChannelMap.json
      - /volume1/docker/xmltvguide/settings.json:/app/settings.json
      - /volume1/docker/xmltvguide/output:/app/output
      - /volume1/docker/xmltvguide/logs:/app/logs
```

> **Important for Option 3**: Create the files/directories on your host first:
>
> ```bash
> mkdir -p /volume1/docker/xmltvguide/{output,logs}
> touch /volume1/docker/xmltvguide/epg_urls.txt
> touch /volume1/docker/xmltvguide/ChannelMap.json
> printf '{\n  "channel": {\n    "useChannelNamesInsteadOfNumericIds": false,\n    "sortChannelsByIdThenDisplayName": true\n  }\n}\n' > /volume1/docker/xmltvguide/settings.json
> ```
>
> Adjust `/volume1/docker/xmltvguide/` to your actual path.

**Access the service:**

> - **Web UI**: `http://your-server:8585`
> - **Guide XML**: `http://your-server:8585/guide.xml`
> - **Health Check**: `http://your-server:8585/health`
> - **Cron Updates**: Runs automatically every 20 minutes

### C. epg_urls.txt format example

```txt
https://example.com/api/time={unixtime}
https://example.com/hgml/guide?zip_code=30303
```

---

## 6. Schedule Updates via Cron

Your Docker image already supports scheduled runs via `cron`. In web mode, `entrypoint.sh` starts cron and `cron-wrapper.sh` logs each run back to the app. The default schedule is in `crontab.txt`:

```cron
*/20 * * * * /app/cron-wrapper.sh >> /var/log/cron.log 2>&1
```

This updates the guide every 20 minutes.

You can modify the cron schedule as needed and rebuild the image. The dashboard reads the schedule and recent cron logs to show the next scheduled run and last cron result.

---

## Useful API Endpoints

- `GET /guide.xml`: generated XMLTV file.
- `GET /health`: public health summary.
- `GET /status`: guide file status.
- `GET /api/config/epg-urls`: read EPG URL file.
- `POST /api/config/epg-urls`: save EPG URL file.
- `GET /api/config/channel-map`: read channel map.
- `POST /api/config/channel-map`: save channel map.
- `POST /api/config/validate-json`: validate/analyze channel map.
- `POST /api/config/test-source`: test an EPG source URL.
- `POST /api/config/preview-channels`: preview detected channels from a source.
- `GET /api/config/backup`: export EPG URLs and channel map.
- `POST /api/config/restore`: restore EPG URLs and channel map from backup JSON.
- `POST /rebuild`: start a manual rebuild.
- `GET /api/rebuild/status`: current rebuild status.
- `GET /api/rebuild/history`: rebuild job history.
- `DELETE /api/rebuild/history`: clear rebuild job history.
- `GET /api/cronlogs`: cron run history.
- `GET /api/cronlogs/schedule`: cron schedule/next-run metadata.
- `DELETE /api/cronlogs`: clear cron run history.

---

## Verify

Once deployed, you should be able to:

- Browse to `http://localhost:8585/guide.xml` or the container's IP
- Add the URL to Emby under XMLTV settings
- Refresh guide data manually to test
- Use Preview Guide and Preview Channels in the web UI before pointing Emby at the feed
- Export a config backup before making large channel-map edits
