# Build & Deploy xmltvguide-generator via Docker

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
  --urlfile=/app/epg_urls.txt \
  --channelmap=/app/ChannelMap.json \
  --output=/app/output/guide.xml
```

> After completion, the generated file will be in `./output/guide.xml`

### 2b. With Web UI
Runs a persistent web service with a management interface and scheduled EPG updates.

```bash
docker run --rm \
  -e RUN_AS_WEB=true \
  -e EPG_URL_FILES=/app/epg_urls.txt \
  -e CHANNEL_MAP_PATH=/app/ChannelMap.json \
  -v $(pwd)/epg_urls.txt:/app/epg_urls.txt \
  -v $(pwd)/ChannelMap.json:/app/ChannelMap.json \
  -v $(pwd)/output:/app/output \
  -p 8585:80 \
  xmltvguide-generator
```

> **Access the service:**
> - **Web UI**: `http://localhost:8585`
> - **Guide XML**: `http://localhost:8585/guide.xml`
> - **Health Check**: `http://localhost:8585/health`
> - **API Endpoints**: `http://localhost:8585/api/config`, `http://localhost:8585/api/cron-logs`

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
      - OUTPUT_PATH=/app/output/guide.xml
    volumes:
      - xmltvguide-output:/app/output
      - xmltvguide-logs:/app/logs

volumes:
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
      - OUTPUT_PATH=/app/output/guide.xml
    volumes:
      - /volume1/docker/xmltvguide/epg_urls.txt:/app/epg_urls.txt
      - /volume1/docker/xmltvguide/ChannelMap.json:/app/ChannelMap.json
      - /volume1/docker/xmltvguide/output:/app/output
      - /volume1/docker/xmltvguide/logs:/app/logs
```

> **Important for Option 3**: Create the files/directories on your host first:
> ```bash
> mkdir -p /volume1/docker/xmltvguide/{output,logs}
> touch /volume1/docker/xmltvguide/epg_urls.txt
> touch /volume1/docker/xmltvguide/ChannelMap.json
> ```
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
Your Docker image already supports scheduled runs via `cron`. Example:

```cron
0 */2 * * * EPG_URL=https://example.com/api/time{unixtime} CHANNEL_MAP_PATH=/app/ChannelMap.json /usr/bin/dotnet /app/xmltvguide-generator.dll >> /var/log/cron.log 2>&1
```

or if you are using the epg_urls.txt file 

```cron
0 */2 * * * CHANNEL_MAP_PATH=/app/ChannelMap.json /usr/bin/dotnet /app/xmltvguide-generator.dll >> /var/log/cron.log 2>&1
```

> This updates the guide **every 2 hours**.

You can modify the cron schedule as needed and rebuild the image.

---

## Verify
Once deployed, you should be able to:

- Browse to `http://localhost:8585/guide.xml` or the container's IP
- Add the URL to Emby under XMLTV settings
- Refresh guide data manually to test
