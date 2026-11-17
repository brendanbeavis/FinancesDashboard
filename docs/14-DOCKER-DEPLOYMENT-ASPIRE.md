# Docker Deployment with .NET Aspire (Web + API)

## Overview

This solution deploys as separate containers for:

- `webfrontend` (Blazor Interactive Server)
- `apiservice` (ASP.NET Core API + EF Core + SQLite)

The Aspire AppHost is used to generate deployment artifacts and is **not** part of the production runtime workload.

## Prerequisites

- .NET SDK 10
- Docker Engine (Linux host or Docker Desktop)
- .NET Aspire AppHost project dependencies restored
- Aspire CLI available (`aspire`) when using `aspire deploy` workflow

If `aspire` is not on PATH, install the Aspire CLI first, then verify:

```bash
aspire --help
aspire deploy --help
```

## Configuration model

### Web (Interactive Server)

- API base URL comes from `Api:BaseUrl`.
- Development default: `https+http://apiservice` (Aspire service discovery).
- Production default: `http://apiservice:8080` (internal Docker service DNS).
- Forwarded headers are enabled for reverse proxies and Cloudflare Tunnel.

### API

- SQLite connection string comes from `ConnectionStrings:financesdb`.
- Development default: `Data Source=financesdashboard.db`.
- Production default: `Data Source=/data/financereport.db`.
- API creates the SQLite directory when needed.
- EF migrations run on startup.

### Data Protection

- Web supports optional persisted keys via `DataProtection:KeysPath`.
- Production default in config: `/keys`.

## Persistent storage

Use persistent mounts for:

- `/data` (required, SQLite DB)
- `/keys` (recommended, Data Protection keys)
- `/uploads` (optional, only if file uploads are later persisted to disk)

Backed-up files:

- `/data/financereport.db`
- `/data/financereport.db-wal`
- `/data/financereport.db-shm`

## Publish and deployment artifacts

Generate artifacts from AppHost (primary source of Compose output):

```bash
aspire publish -o ./artifacts
```

List deploy pipeline steps:

```bash
aspire deploy --list-steps
```

Prepare Docker Compose deployment for production:

```bash
aspire do prepare-docker-compose --environment production
```

Deploy:

```bash
aspire deploy
```

Do not manually maintain generated Compose files as the primary source; regenerate from AppHost changes.

## Environment variables (production)

Set values in generated environment files or deployment environment:

- `ASPNETCORE_ENVIRONMENT=Production`
- `ConnectionStrings__financesdb=Data Source=/data/financereport.db` (API override if needed)
- `Api__BaseUrl=http://apiservice:8080` (Web override if needed)
- `DataProtection__KeysPath=/keys` (Web override if needed)

## Networking and reverse proxy

- Publish external port only for `webfrontend`.
- Keep `apiservice` internal to the Docker network.
- Route public traffic to `webfrontend` through reverse proxy / Cloudflare Tunnel.
- Ensure WebSocket support is enabled in proxy for Blazor Interactive Server SignalR circuits.
- Ensure proxy forwards `X-Forwarded-For`, `X-Forwarded-Proto`, and `X-Forwarded-Host`.

## Update workflow

1. Pull updated code.
2. Regenerate deployment artifacts from AppHost.
3. Redeploy using Aspire deploy pipeline.
4. Verify health and logs.

## Backup and restore

### Backup

On host, stop write-heavy traffic and copy SQLite files from mounted `/data`:

```bash
cp financereport.db financereport.db.bak
cp financereport.db-wal financereport.db-wal.bak
cp financereport.db-shm financereport.db-shm.bak
```

### Restore

1. Stop containers.
2. Restore DB files into mounted `/data`.
3. Start containers.
4. Verify API startup and migration logs.

## Logs and troubleshooting

Container logs:

```bash
docker compose logs webfrontend
docker compose logs apiservice
docker compose logs -f
```

Common checks:

- SQLite permission issue: verify mounted `/data` is writable by container user.
- Data Protection reset symptoms (new antiforgery/circuit key): persist `/keys` volume.
- Reverse proxy HTTPS loop: confirm forwarded headers and proxy TLS forwarding.
- Web cannot reach API: verify internal DNS name `apiservice`, network, and API listening port.

## ARM64 Raspberry Pi 5 guidance

Recommended:

- Run deployment on the ARM64 host itself so images build/run natively.

If building from another host, use a build pipeline that targets `linux/arm64` (for example Docker Buildx-capable workflow) and ensure produced images are ARM64 compatible.

For mixed-fleet deployments, produce images for both `linux/amd64` and `linux/arm64` through your CI image build workflow.

## Validation checklist

After deployment:

1. Browser loads app via reverse proxy.
2. Interactive Server components function.
3. SignalR circuit establishes and remains stable.
4. Web reaches API through `http://apiservice:8080`.
5. API is not publicly exposed.
6. Database reads/writes succeed.
7. Data persists after container recreation.
8. App health endpoint responds (`/health` on Web, `/api/health` on API).
