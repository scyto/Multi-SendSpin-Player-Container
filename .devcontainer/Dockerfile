# Development container for Multi-Room Audio Controller
# Based on Microsoft's .NET 8 devcontainer image

FROM mcr.microsoft.com/devcontainers/dotnet:8.0

# Install additional tools
RUN apt-get update && export DEBIAN_FRONTEND=noninteractive \
    && apt-get -y install --no-install-recommends \
        # Audio tools (for testing concepts, though actual audio won't work in container)
        pulseaudio-utils \
        alsa-utils \
        # Playwright dependencies
        libnss3 \
        libnspr4 \
        libatk1.0-0 \
        libatk-bridge2.0-0 \
        libcups2 \
        libdrm2 \
        libxkbcommon0 \
        libxcomposite1 \
        libxdamage1 \
        libxfixes3 \
        libxrandr2 \
        libgbm1 \
        libpango-1.0-0 \
        libcairo2 \
        libasound2 \
        # Useful dev tools
        jq \
        curl \
        wget \
    && apt-get clean \
    && rm -rf /var/lib/apt/lists/*

# Install .NET 9 runtime (required by C# Dev Kit extension for IDE tooling)
# Your app still builds/runs on .NET 8 - this is only for the VS Code extension
RUN wget -q https://dot.net/v1/dotnet-install.sh -O /tmp/dotnet-install.sh \
    && chmod +x /tmp/dotnet-install.sh \
    && /tmp/dotnet-install.sh --channel 9.0 --runtime aspnetcore --install-dir /usr/share/dotnet \
    && rm /tmp/dotnet-install.sh

# Install PowerShell via direct download (cross-architecture compatible)
RUN ARCH=$(dpkg --print-architecture) \
    && if [ "$ARCH" = "amd64" ]; then \
         wget -q https://github.com/PowerShell/PowerShell/releases/download/v7.4.7/powershell_7.4.7-1.deb_amd64.deb -O /tmp/powershell.deb; \
       elif [ "$ARCH" = "arm64" ]; then \
         wget -q https://github.com/PowerShell/PowerShell/releases/download/v7.4.7/powershell_7.4.7-1.deb_arm64.deb -O /tmp/powershell.deb; \
       fi \
    && dpkg -i /tmp/powershell.deb || apt-get install -f -y \
    && rm /tmp/powershell.deb

# Add dotnet tools to PATH (for any future dotnet tools)
ENV PATH="${PATH}:/home/vscode/.dotnet/tools"

# Set working directory
WORKDIR /workspaces/Multi-SendSpin-Player-Container

# Default to bash
CMD ["bash"]
