FROM mcr.microsoft.com/devcontainers/javascript-node:latest

LABEL maintainer="ben@subzerodev.com" \
      version="1.0" \
      description="Build Agent with Node.js, Angular CLI, TypeScript, Docker, and PowerShell"

# Install PowerShell
RUN apt-get update && \
    apt-get install -y curl wget apt-transport-https software-properties-common && \
    wget -q "https://packages.microsoft.com/config/debian/$(lsb_release -rs)/packages-microsoft-prod.deb" -O packages-microsoft-prod.deb && \
    dpkg -i packages-microsoft-prod.deb && \
    rm packages-microsoft-prod.deb && \
    apt-get update && \
    apt-get install -y powershell docker.io && \
    apt-get clean && rm -rf /var/lib/apt/lists/*

# Install Angular CLI, TypeScript, Docker and PowerShell
RUN npm install -g @angular/cli typescript powershell

# Verify installations
RUN node -v && npm -v && ng version && pwsh --version && docker --version

# Set PowerShell as a default shell
SHELL ["pwsh", "-Command"]

# Default working directory
WORKDIR /workspace

CMD ["pwsh"]
