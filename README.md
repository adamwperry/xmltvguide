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
Pass the URL you want to fetch EPG data from:

```bash
docker run --rm -e EPG_URL="https://example.com/api/time{unixtime}" -e CHANNEL_MAP_PATH=/app/ChannelMap.json -v $(pwd)/ChannelMap.json:/app/ChannelMap.json -p 8585:80 xmltvguide-generator
```

> The output will be served at `http://localhost:8585/guide.xml`

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
gtar -cvf xmltvguide.tar Dockerfile crontab.txt entrypoint.sh src xmltvguide-generator.csproj xmltvguide-generator.sln
```

> Use `gtar` instead of macOS default `tar` to avoid xattr issues.

You can also compress it:
```bash
gtar -czvf xmltvguide.tar.gz Dockerfile crontab.txt entrypoint.sh src xmltvguide-generator.csproj xmltvguide-generator.sln ChannelMap.json
```

---

## 5. Deploy in Portainer

### A. Upload and Build the Image
1. Go to **Images** → **Build a new image**
2. Upload your `.tar.gz` or connect to a Git repo

### B. Add the Stack
1. Go to **Stacks** → **+ Add stack**
2. Name the stack `xmltvguide`
3. Use the following stack configuration:

```yaml
version: "3.8"
services:
  xmltvguide:
    image: xmltvguide-generator:latest
    container_name: xmltvguide
    ports:
      - "8585:80"
    restart: unless-stopped
    environment:
      - EPG_URL=https://example.com/api/time{unixtime}
      - CHANNEL_MAP_PATH=/app/ChannelMap.json
```

---

## 6. Schedule Updates via Cron
Your Docker image already supports scheduled runs via `cron`. Example:

```cron
0 */2 * * * EPG_URL=https://example.com/api/time{unixtime} CHANNEL_MAP_PATH=/app/ChannelMap.json /usr/bin/dotnet /app/xmltvguide-generator.dll >> /var/log/cron.log 2>&1
```

> This updates the guide **every 2 hours**.

You can modify the cron schedule as needed and rebuild the image.

---

## Verify
Once deployed, you should be able to:

- Browse to `http://localhost:8585/guide.xml` or the container's IP
- Add the URL to Emby under XMLTV settings
- Refresh guide data manually to test
