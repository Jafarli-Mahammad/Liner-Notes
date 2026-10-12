# Stable .NET SDK 10.0.401 and ASP.NET runtime 10.0.12, verified 2026-10-12.
FROM mcr.microsoft.com/dotnet/sdk:10.0.401@sha256:e70cdb7f80b0348f5cb85f19a8f670fca061f033d57eed12fa003d58b0e06317 AS build
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

# Explicit local migration runner. Runtime images contain no SDK.
FROM build AS tools
ENTRYPOINT ["dotnet", "tool", "run", "dotnet-ef", "--"]

# Local email operator shares artifacts with the non-root web and Worker processes.
FROM build AS operator
ENV DOTNET_CLI_HOME=/home/app \
    XDG_CACHE_HOME=/home/app/.cache
RUN mkdir -p /home/app/.cache /src/scripts \
    && chown -R "$APP_UID:$APP_UID" /src /opt/nuget /home/app
USER $APP_UID
ENTRYPOINT ["dotnet", "run", "--file", "/src/scripts/phase7_local_email.cs", "--no-cache", "--"]

FROM mcr.microsoft.com/dotnet/aspnet:10.0.12@sha256:222759b391a1aaf241166672c8f99b2d4ada452e7b5319f3c6e8f265a37b5ad4 AS runtime
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080
RUN mkdir -p /app/local-state && chown "$APP_UID:$APP_UID" /app/local-state && chmod 700 /app/local-state

# Root is used only to initialize this dedicated local volume.
FROM runtime AS volume-init
USER root
ENTRYPOINT ["sh", "-c", "set -eu; for category in copies recordings cache reports backups partials; do mkdir -p /app/local-state/$category; chown $APP_UID:$APP_UID /app/local-state/$category; chmod 700 /app/local-state/$category; done; chown $APP_UID:$APP_UID /app/local-state; chmod 700 /app/local-state"]

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
