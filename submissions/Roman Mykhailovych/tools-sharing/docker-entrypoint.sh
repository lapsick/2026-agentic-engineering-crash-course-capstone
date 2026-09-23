#!/bin/sh
# Entrypoint for the `app` service (see docker-compose.yml / Dockerfile).
#
# Runs the dedicated, separate migration step (ToolShare.DbMigrator) only
# when RUN_MIGRATIONS=true, then always execs the Blazor host — migrations
# are never applied by the running app itself (Constitution VI). Both `cd`s
# use each published app's own directory (not a relative "dbmigrator/..." or
# "blazor/..." path) so ASP.NET Core's Generic Host resolves appsettings.json
# from its own content root correctly (see the T101a note in tasks.md/README —
# the Generic Host's default content root is the process's current directory).
set -e

if [ "$RUN_MIGRATIONS" = "true" ]; then
    echo "RUN_MIGRATIONS=true - applying database migrations via ToolShare.DbMigrator..."
    (cd /app/dbmigrator && dotnet ToolShare.DbMigrator.dll)
    echo "Migrations complete."
fi

cd /app/blazor
exec dotnet ToolShare.Blazor.dll
