FROM mcr.microsoft.com/devcontainers/javascript-node:22-bookworm

ARG GITVERSION_VERSION=6.5.1
ARG NUKE_VERSION=10.1.0

# Optional: pin these too for reproducibility
ARG TYPESCRIPT_VERSION=latest
ARG TSX_VERSION=latest
ARG ANGULAR_CLI_VERSION=21.1.2
ARG ANGULAR_GHPAGES_VERSION=3.0.2

LABEL maintainer="ben@subzerodev.com" \
      version="1.0" \
      description="Build Agent with Node.js, Angular CLI, TypeScript, Docker, PowerShell, and GitVersion"
LABEL org.opencontainers.image.source="https://github.com/The-Running-Dev/Docker-BuildAgent"

ENV DEBIAN_FRONTEND=noninteractive \
    DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    NUKE_TELEMETRY_OPTOUT=1 \
    PATH="${PATH}:/root/.dotnet/tools"

COPY templates/ /nuke/templates/
COPY artifacts/ /nuke/forge/

COPY scripts/nuke/*.* /nuke/scripts/
COPY scripts/nuke/bin/ /usr/local/bin/

RUN find /usr/local/bin -maxdepth 1 -type f -exec chmod +x {} \;

RUN set -eux; \
    apt-get update; \
    apt-get install -y --no-install-recommends \
      ca-certificates curl wget gnupg apt-transport-https; \
    wget -q "https://packages.microsoft.com/config/debian/12/packages-microsoft-prod.deb" -O /tmp/msprod.deb; \
    dpkg -i /tmp/msprod.deb; \
    rm -f /tmp/msprod.deb; \
    apt-get update; \
    # Add Docker official GPG key and repository (install modern docker CLI)
    mkdir -p /etc/apt/keyrings; \
    curl -fsSL https://download.docker.com/linux/debian/gpg | gpg --dearmor -o /etc/apt/keyrings/docker.gpg; \
    chmod a+r /etc/apt/keyrings/docker.gpg; \
    echo "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.gpg] https://download.docker.com/linux/debian bookworm stable" > /etc/apt/sources.list.d/docker.list; \
    apt-get update; \
    # sanity check: if the repo is wrong, this will show "Candidate: (none)" in logs
    apt-cache policy powershell | sed -n '1,20p'; \
    apt-get install -y --no-install-recommends \
      git powershell docker-ce-cli docker-buildx-plugin docker-compose-plugin dotnet-sdk-8.0 \
      dotnet-sdk-9.0 dotnet-sdk-10.0; \
    apt-get clean; \
    rm -rf /var/lib/apt/lists/*

RUN set -eux; \
    npm install -g \
      "typescript@${TYPESCRIPT_VERSION}" \
      "tsx@${TSX_VERSION}" \
      "@angular/cli@${ANGULAR_CLI_VERSION}" \
      "angular-cli-ghpages@${ANGULAR_GHPAGES_VERSION}"; \
    npm cache clean --force; \
    rm -rf /root/.npm/_cacache

RUN set -eux; \
    pwsh -NoLogo -NoProfile -Command "dotnet tool install --global GitVersion.Tool --version ${GITVERSION_VERSION}"; \
    pwsh -NoLogo -NoProfile -Command "dotnet tool install --global Nuke.GlobalTool --version ${NUKE_VERSION}"

RUN set -eux; \
    node -v; \
    npm -v; \
    ng version; \
    pwsh --version; \
    docker --version; \
    pwsh -NoLogo -NoProfile -Command "dotnet tool list -g"

SHELL ["pwsh", "-Command"]

WORKDIR /workspace
CMD ["pwsh"]
