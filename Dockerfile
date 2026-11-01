# Node stable LTS release and official image digest verified 2026-11-01.
FROM node:26.13.0-bookworm-slim@sha256:bc0a2afd3b74ce12222fc8bab99271e66257f021078f5f4a5d687a77d280c1d9 AS frontend
WORKDIR /frontend
COPY src/Presentation/ClientApp/package.json src/Presentation/ClientApp/package-lock.json ./
RUN npm ci
COPY src/Presentation/ClientApp/ ./
RUN npm run build

# Stable .NET SDK 10.0.403 and ASP.NET runtime 10.0.14, verified 2026-11-01.
FROM mcr.microsoft.com/dotnet/sdk:10.0.403@sha256:de4fe90a98845d75910b9769662465c84de1a934665c431279d1382b2bddc02b AS build
# Ubuntu's fixed stable package for CVE-2026-111497; the SDK base predates it.
RUN sed -i 's|http://|https://|g' /etc/apt/sources.list.d/ubuntu.sources \
    && apt-get --snapshot=20261101T000000Z -o Acquire::By-Hash=force update -qq \
    && apt-get --snapshot=20261101T000000Z install -y --only-upgrade --no-install-recommends libbrotli1=1.1.0-2ubuntu0.1 \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /src
ENV NUGET_PACKAGES=/opt/nuget/packages
COPY Directory.Build.props Directory.Packages.props global.json ./
COPY .config/dotnet-tools.json .config/
COPY src/Domain/Domain.csproj src/Domain/packages.lock.json src/Domain/
COPY src/Application/Application.csproj src/Application/packages.lock.json src/Application/
COPY src/DataAccess/DataAccess.csproj src/DataAccess/packages.lock.json src/DataAccess/
COPY src/Infrastructure/Infrastructure.csproj src/Infrastructure/packages.lock.json src/Infrastructure/
COPY src/Presentation/Presentation.csproj src/Presentation/packages.lock.json src/Presentation/
COPY src/Worker/Worker.csproj src/Worker/packages.lock.json src/Worker/
RUN dotnet tool restore && dotnet restore src/Presentation/Presentation.csproj --locked-mode && dotnet restore src/Worker/Worker.csproj --locked-mode
COPY src/ src/
RUN dotnet publish src/Presentation/Presentation.csproj -c Release --no-restore -o /out/web /p:UseAppHost=false && dotnet publish src/Worker/Worker.csproj -c Release --no-restore -o /out/worker /p:UseAppHost=false
COPY --from=frontend /frontend/dist/ /out/web/wwwroot/

# Explicit local migration runner. Runtime images contain no SDK.
FROM build AS tools
ARG LINER_SOURCE_TREE=unknown
LABEL org.opencontainers.image.revision=$LINER_SOURCE_TREE org.opencontainers.image.version=0.5.0-beta
ENTRYPOINT ["dotnet", "tool", "run", "dotnet-ef", "--"]

# Local email operator shares artifacts with the non-root web and Worker processes.
FROM build AS operator
ENV DOTNET_CLI_HOME=/home/app \
    XDG_CACHE_HOME=/home/app/.cache
RUN mkdir -p /home/app/.cache /src/scripts \
    && chown -R "$APP_UID:$APP_UID" /src /opt/nuget /home/app
USER $APP_UID
ENTRYPOINT ["dotnet", "run", "--file", "/src/scripts/phase7_local_email.cs", "--no-cache", "--"]

FROM mcr.microsoft.com/dotnet/aspnet:10.0.14@sha256:de0ee358cdd672f7017a003bed21e9ac9ed48a27c922a59b18175b6e82fa7fee AS runtime
ARG LINER_SOURCE_TREE=unknown
LABEL org.opencontainers.image.revision=$LINER_SOURCE_TREE org.opencontainers.image.version=0.5.0-beta
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080
RUN mkdir -p /app/local-state /app/keys && chown "$APP_UID:$APP_UID" /app/local-state /app/keys && chmod 700 /app/local-state /app/keys

# Root is used only to initialize this dedicated local volume.
FROM runtime AS volume-init
USER root
ENTRYPOINT ["sh", "-c", "set -eu; for category in copies recordings cache reports backups partials upstream-pacing; do mkdir -p /app/local-state/$category; chown $APP_UID:$APP_UID /app/local-state/$category; chmod 700 /app/local-state/$category; done; mkdir -p /app/keys; chown $APP_UID:$APP_UID /app/local-state /app/keys; chmod 700 /app/local-state /app/keys"]

FROM runtime AS worker
COPY --from=build /out/worker/ ./
USER $APP_UID
ENTRYPOINT ["dotnet", "LinerNotes.Worker.dll"]

# Web remains the default Docker build target.
FROM runtime AS web
COPY --from=build /out/web/ ./
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "LinerNotes.Presentation.dll"]
