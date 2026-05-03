# syntax=docker/dockerfile:1

ARG DOTNET_VERSION=6.0
ARG NODE_VERSION=20-bullseye
ARG DEBIAN_VERSION=bookworm-slim

FROM --platform=$BUILDPLATFORM node:${NODE_VERSION} AS node

FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION} AS build
ARG BUILD_SOURCEBRANCHNAME=container
ARG READARR_VERSION=0.4.19.0
ARG TARGETARCH

ENV BUILD_SOURCEBRANCHNAME=${BUILD_SOURCEBRANCHNAME} \
    DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    READARRVERSION=${READARR_VERSION}

COPY --from=node /usr/local /usr/local

RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        ca-certificates \
        git \
        python3 \
    && rm -rf /var/lib/apt/lists/* \
    && npm install --global --force yarn@1.22.19

WORKDIR /src

COPY package.json yarn.lock .yarnrc ./
COPY .editorconfig tsconfig.json ./
COPY frontend ./frontend
COPY Logo ./Logo
COPY src ./src
COPY distribution ./distribution
COPY build.sh test.sh LICENSE.md ./

RUN case "${TARGETARCH}" in \
        amd64) rid="linux-x64" ;; \
        arm64) rid="linux-arm64" ;; \
        *) echo "Unsupported TARGETARCH: ${TARGETARCH}" >&2; exit 1 ;; \
    esac \
    && ./build.sh --backend --frontend --packages --framework net6.0 --runtime "${rid}" \
    && mkdir -p /app \
    && cp -a "_artifacts/${rid}/net6.0/Readarr" /app/Readarr

FROM debian:${DEBIAN_VERSION} AS runtime

ENV HOME=/config \
    UMASK=022

RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        ca-certificates \
        curl \
        libicu72 \
        libsqlite3-0 \
        tzdata \
    && rm -rf /var/lib/apt/lists/* \
    && groupadd --gid 1000 readarr \
    && useradd --uid 1000 --gid readarr --home-dir /config --shell /usr/sbin/nologin readarr \
    && mkdir -p /app/readarr /config \
    && chown -R readarr:readarr /app/readarr /config

WORKDIR /app/readarr
COPY --from=build --chown=readarr:readarr /app/Readarr/ ./
COPY docker/entrypoint.sh /usr/local/bin/readarr-entrypoint

RUN chmod 0755 /usr/local/bin/readarr-entrypoint

VOLUME ["/config"]
EXPOSE 8787

USER readarr
ENTRYPOINT ["/usr/local/bin/readarr-entrypoint"]
