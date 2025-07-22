FROM mcr.microsoft.com/devcontainers/javascript-node:latest

LABEL maintainer="ben@subzerodev.com" \
      version="0.3" \
      description="Build Agent with Node.js, Angular CLI, TypeScript, Docker, PowerShell, and GitVersion"

# Disable telemetry
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1
ENV NUKE_TELEMETRY_OPTOUT=1

COPY templates/ /nuke/templates/
COPY artifacts/ /nuke/forge/

# Copy and setup nuke scripts and bin
COPY scripts/nuke/* /nuke/scripts/
COPY scripts/nuke/bin/ /usr/local/bin/
RUN chmod +x /usr/local/bin/*

# Install PowerShell and prerequisites
RUN apt-get update && \
    apt-get install -y curl wget apt-transport-https software-properties-common gnupg ca-certificates && \
    wget -q "https://packages.microsoft.com/config/debian/$(lsb_release -rs)/packages-microsoft-prod.deb" -O packages-microsoft-prod.deb && \
    dpkg -i packages-microsoft-prod.deb && \
    rm packages-microsoft-prod.deb && \
    apt-get update && \
    apt-get install -y git powershell docker.io dotnet-sdk-8.0 && \
    apt-get clean && rm -rf /var/lib/apt/lists/*

# Install global npm tools
RUN npm install -g @angular/cli typescript angular-cli-ghpages@latest powershell \
    && npm cache clean --force \
    && rm -rf /root/.npm/_cacache

# Install GitVersion and Nuke as global dotnet tools
RUN pwsh -Command "dotnet tool install --global GitVersion.Tool"
RUN pwsh -Command "dotnet tool install --global Nuke.GlobalTool"

# Add .dotnet/tools to PATH for all shells
ENV PATH="${PATH}:/root/.dotnet/tools"

# Verify installations
RUN node -v && npm -v && ng version && pwsh --version && docker --version && \
    pwsh -Command "dotnet tool list -g"

# Set PowerShell as the default shell
SHELL ["pwsh", "-Command"]

# Set working directory
WORKDIR /workspace

CMD ["pwsh"]
