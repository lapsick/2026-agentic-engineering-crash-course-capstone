# syntax=docker/dockerfile:1

# ---------------------------------------------------------------------------
# Build stage: restores and publishes BOTH ToolShare.Blazor (the running app)
# and ToolShare.DbMigrator (the dedicated, separate migration step —
# Constitution VI: migrations are never applied by the running app).
# ---------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Node.js/npm are required by `abp install-libs` below to fetch
# ToolShare.Blazor's client-side theme assets (LeptonXLite) into
# wwwroot/libs — that directory is deliberately excluded from both git
# (.gitignore) and this build context (.dockerignore) because it's vendored,
# not committed, so it must be (re)installed here for a reproducible image.
# The distro-default `nodejs` package is too old for one of those packages'
# declared engines range (>=24), so install current Node from NodeSource.
RUN apt-get update \
    && apt-get install -y --no-install-recommends ca-certificates curl gnupg \
    && curl -fsSL https://deb.nodesource.com/setup_24.x | bash - \
    && apt-get install -y --no-install-recommends nodejs \
    && rm -rf /var/lib/apt/lists/*

COPY . .

RUN dotnet tool restore
RUN dotnet tool run abp install-libs

# Restore/publish the two projects directly (not the whole .slnx) — the test
# projects it also references are deliberately excluded from this build
# context by .dockerignore (Constitution VI/container-first: image builds
# don't need them), so `dotnet restore ToolShare.slnx` itself would fail.
RUN dotnet publish src/ToolShare.Blazor/ToolShare.Blazor.csproj \
    -c Release -o /app/blazor
RUN dotnet publish src/ToolShare.DbMigrator/ToolShare.DbMigrator.csproj \
    -c Release -o /app/dbmigrator

# openiddict.pfx is deliberately *.pfx-gitignored (a real private-key
# certificate, unlike the dev-only appsettings passwords, should never be
# committed) and README.md documents generating it manually for local
# non-Docker runs — but a container built from a genuinely clean checkout has
# no such file on disk to COPY, and ToolShareBlazorModule's
# AddProductionEncryptionAndSigningCertificate("openiddict.pfx", ...) fails
# hard at startup without it (Production profile only — Development
# auto-generates an ephemeral cert instead). Generate the same certificate
# here, at build time, with the exact filename/passphrase the module expects.
RUN dotnet dev-certs https -v -ep /app/blazor/openiddict.pfx -p c987672a-6212-43bf-922d-73e0c4fa15a6

# ---------------------------------------------------------------------------
# Runtime stage: no SDK, no source — just the two published outputs plus the
# entrypoint script that decides which one(s) to run.
# ---------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

COPY --from=build /app/blazor ./blazor
COPY --from=build /app/dbmigrator ./dbmigrator
COPY docker-entrypoint.sh ./docker-entrypoint.sh

RUN chmod +x ./docker-entrypoint.sh \
    && mkdir -p /app/blobs

# WORKDIR intentionally left at the DbMigrator's own output directory (not
# /app): quickstart.md's idempotence re-run overrides only the entrypoint
# (`docker compose run --rm --entrypoint "dotnet ToolShare.DbMigrator.dll"
# app`), with no working-directory override, so `ToolShare.DbMigrator.dll`
# must already resolve as a bare relative path — and the Generic Host needs
# its content root to be this same directory to find appsettings.json (see
# docker-entrypoint.sh's comment / the T101a note in tasks.md and README.md).
WORKDIR /app/dbmigrator

EXPOSE 8080

ENTRYPOINT ["/app/docker-entrypoint.sh"]
