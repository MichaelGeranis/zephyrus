# Running Zephyrus in production

The Docker setup in the README is a demo: well-known tokens, ports on
`127.0.0.1` only, no TLS. This guide turns it into a deployment a team can
use: real credentials, HTTPS through a reverse proxy, health checks, and
backups you can restore from.

Zephyrus is alpha software. Run it for a team you trust, on a network you
control, and keep backups.

## What runs

| Service | Image | Listens on | Reached by |
|---------|-------|------------|------------|
| PostgreSQL 18 | `postgres:18-alpine` | 5432 | the API only |
| API | `ghcr.io/michaelgeranis/zephyrus-api` | 8080 | the proxy, GitHub webhooks |
| Web UI | built by you (see below) | 3000 | the proxy |
| Reverse proxy | Caddy, nginx or a cloud load balancer | 443 | browsers |

Only the reverse proxy is published to the network. The API, the web UI and the
database are reached through it or not at all.

The browser calls the API directly, so the two get their own hostnames, for
example `zephyrus.example.com` for the UI and `api.zephyrus.example.com` for the
API.

## 1. Build the web image for your API address

The web UI is built with the API's address in it, both for its API calls and
for its Content-Security-Policy, which allows the browser to connect to that
address only. The released web image is built for `http://localhost:5000`, so
build your own:

```bash
docker build -f docker/web.Dockerfile --build-arg NEXT_PUBLIC_API_URL=https://api.zephyrus.example.com -t zephyrus-web:0.1.0 .
```

The API image needs no rebuild.

## 2. Configure the API

Set these on the API container. Generate every token and password with
`openssl rand -hex 32`.

| Setting | Value |
|---------|-------|
| `Demo__Enabled` | `false` |
| `ConnectionStrings__DefaultConnection` | `Host=db;Database=zephyrus;Username=zephyrus;Password=<db password>` |
| `Database__MigrateOnStartup` | `true` to apply migrations when the API starts, or run them yourself before upgrading |
| `Team__Members__N__Email` / `__DisplayName` / `__TokenHash` / `__Roles__0` | one entry per team member. `TokenHash` is the SHA-256 of the token (`printf %s "$TOKEN" \| sha256sum`), so the configuration holds no token anyone could sign in with; give each member their token directly. `__Token` (the token as written) still works, with a startup warning |
| `AgentRuns__ContentRetentionDays` | how long agent runs keep what they read and wrote (the constitution, artifacts, issue bodies, code), for example `365`. The run's record stays. `0`, the default, keeps them |
| `Cors__AllowedOrigins__0` | `https://zephyrus.example.com` |
| `DataProtection__KeyPath` | `/data/keys` (the image's default), on a volume you back up |
| `GitHub__Webhook__Secret` | the secret you set on the GitHub webhook |
| `ReverseProxy__Enabled` | `true` (see the next section) |
| `ReverseProxy__KnownProxies__0` or `ReverseProxy__KnownNetworks__0` | your proxy's address, or the network it is on |
| `Claude__ApiKey`, `LanguageModel__…`, `GitHub__App__…` | as in the README |

## 3. Put it behind a reverse proxy with TLS

The proxy terminates TLS and forwards plain HTTP to the API and the UI. Every
request then reaches the API from the proxy's address, so the API must be told
which proxy to believe:

- `ReverseProxy__Enabled=true` makes the API read the client's address from
  `X-Forwarded-For` and the scheme from `X-Forwarded-Proto`.
- The headers are believed only from the addresses in
  `ReverseProxy__KnownProxies` or the CIDR ranges in
  `ReverseProxy__KnownNetworks`. From anyone else they are ignored. The API
  refuses to start with the setting on and neither list set.
- Only the last address in `X-Forwarded-For` is used: the one your proxy added.

This matters for two things:

- **The failed sign-in limit** counts wrong tokens per client address. Without
  forwarded headers every user shares the proxy's address, so one person
  mistyping their token ten times blocks the whole team for 15 minutes.
- **HSTS.** The API sends `Strict-Transport-Security` only on requests it sees
  as HTTPS, which behind a proxy it learns from `X-Forwarded-Proto`.

Give the proxy a fixed address so the setting stays valid. With Docker Compose,
pin the network's subnet:

```yaml
networks:
  default:
    ipam:
      config:
        - subnet: 172.30.0.0/24
```

and set `ReverseProxy__KnownNetworks__0=172.30.0.0/24`.

### Caddy

Caddy obtains and renews certificates itself and sets both forwarded headers,
replacing any `X-Forwarded-For` the client sent.

```
zephyrus.example.com {
    reverse_proxy web:3000
}

api.zephyrus.example.com {
    reverse_proxy api:8080
}
```

### nginx

Set the headers rather than appending to what the client sent:

```nginx
location / {
    proxy_pass http://api:8080;
    proxy_set_header Host $host;
    proxy_set_header X-Forwarded-For $remote_addr;
    proxy_set_header X-Forwarded-Proto $scheme;
}
```

Use the same block for the UI with `proxy_pass http://web:3000`, and add
`Strict-Transport-Security` to the UI's server block; the API sends its own.

### GitHub webhooks

Point the repository's webhook at `https://api.zephyrus.example.com/api/webhooks/github`,
content type `application/json`, with the same secret as
`GitHub__Webhook__Secret`, for the **Pull requests** and **Deployment statuses**
events. Deliveries that are not signed with the secret are rejected.

## 4. Health checks

| Endpoint | Means | Use it for |
|----------|-------|------------|
| `GET /health` on the API | the process answers | liveness: restart the container if it fails |
| `GET /health/ready` on the API | it also reaches the database | readiness: send traffic only while it passes |
| `GET /health` on the web UI | the server answers | liveness |

They need no token and answer with the status and version, for example
`{"status":"Healthy","version":"0.1.0","checks":{"database":"Healthy"}}`. A
failing readiness check answers 503.

Both images declare a Docker `HEALTHCHECK`, and `docker compose up --wait`
returns once every service is healthy. Liveness deliberately ignores the
database: a database outage should stop traffic, not restart the API in a loop.

On Kubernetes:

```yaml
livenessProbe:
  httpGet: { path: /health, port: 8080 }
readinessProbe:
  httpGet: { path: /health/ready, port: 8080 }
```

## 5. Back up the database and the key ring

Two things hold state, and you need **both** to restore:

- **PostgreSQL**: projects, features, approvals, the audit trail and agent runs.
  Artifact contents live in GitHub.
- **The Data Protection key ring** (`DataProtection__KeyPath`, `/data/keys` in
  the image). It encrypts the GitHub tokens stored with each project. Restore
  the database without it and every project's token must be entered again.

Back up the database daily with `pg_dump`, which is safe while the API runs:

```bash
docker compose exec -T db pg_dump -U zephyrus -d zephyrus --format=custom > zephyrus-$(date +%F).dump
```

Copy the key ring with each database backup:

```bash
docker compose cp api:/data/keys ./keys-$(date +%F)
```

Together the two can decrypt the stored tokens, so keep backups where only the
people who run Zephyrus can read them.

To restore into an empty database, stop the API, then:

```bash
docker compose exec -T db pg_restore -U zephyrus -d zephyrus --clean --if-exists < zephyrus-2026-11-10.dump
```

Put the key ring back in the API's volume before starting it again. Test a
restore before you need one.

## 6. Upgrade

1. Read the [CHANGELOG](../CHANGELOG.md) for the new version, especially
   **Changed**.
2. Back up the database and the key ring.
3. Pull the new API image, rebuild the web image at the same tag, and restart.
   With `Database__MigrateOnStartup=true` the API applies new migrations as it
   starts. Migrations only move forward, so going back to an older version
   means restoring the backup.

Pin versions (`zephyrus-api:0.1.0`) rather than `latest`, so a restart never
upgrades by surprise.

## 7. Logs and traces

The API writes JSON logs when `Logging__Console__FormatterName=json` is set,
and sends traces when `OTEL_EXPORTER_OTLP_ENDPOINT` points at an OTLP backend.
See [Tracing and logs](../README.md#tracing-and-logs).

## Limits to know

- **One API instance.** The job queue handles several, but the failed sign-in
  counters are kept in memory per instance.
- **The team is configuration.** Adding or removing a member, or rotating a
  token, is an environment change and a restart.
