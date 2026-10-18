# syntax=docker/dockerfile:1
# TGYSM images (ADR-0024): target "api" (API + SPA in wwwroot) and target "worker". No secrets in any layer;
# configuration comes from the environment at run time (deploy/.env).

FROM node:26-bookworm-slim AS frontend-build
WORKDIR /src/frontend
COPY frontend/package.json frontend/package-lock.json ./
RUN npm ci
COPY frontend/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS dotnet-build
WORKDIR /src
COPY global.json ./
COPY backend/ backend/
RUN dotnet publish backend/src/Tgysm.Api -c Release -o /out/api --nologo \
 && dotnet publish backend/src/Tgysm.Worker -c Release -o /out/worker --nologo

# Shared data folders: file store and Data Protection key ring, owned by the non-root app user.
# The key folder is private to that user (0700): the keys decrypt account-link tokens.
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS api
ARG APP_VERSION=dev
ENV APP_VERSION=$APP_VERSION \
    ASPNETCORE_HTTP_PORTS=8080 \
    Files__RootPath=/data/files \
    DataProtection__KeysPath=/data/keys
WORKDIR /app
# Npgsql probes GSSAPI on connect and logs an error when the library is missing; curl serves the compose health check.
RUN apt-get update \
 && apt-get install -y --no-install-recommends libgssapi-krb5-2 curl \
 && rm -rf /var/lib/apt/lists/* \
 && mkdir -p /data/files /data/keys \
 && chown -R "$APP_UID" /data \
 && chmod 0700 /data/keys
COPY --from=dotnet-build /out/api ./
COPY --from=frontend-build /src/frontend/dist ./wwwroot
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Tgysm.Api.dll"]

# The Worker references Infrastructure, which uses ASP.NET Core Data Protection, so it needs the ASP.NET runtime too.
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS worker
ARG APP_VERSION=dev
ENV APP_VERSION=$APP_VERSION \
    Files__RootPath=/data/files \
    DataProtection__KeysPath=/data/keys
WORKDIR /app
# Npgsql probes GSSAPI on connect and logs an error when the library is missing.
RUN apt-get update \
 && apt-get install -y --no-install-recommends libgssapi-krb5-2 \
 && rm -rf /var/lib/apt/lists/* \
 && mkdir -p /data/files /data/keys \
 && chown -R "$APP_UID" /data \
 && chmod 0700 /data/keys
COPY --from=dotnet-build /out/worker ./
USER $APP_UID
ENTRYPOINT ["dotnet", "Tgysm.Worker.dll"]
