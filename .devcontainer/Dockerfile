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

# Install PowerShell (needed for Playwright CLI)
RUN dotnet tool install --global PowerShell

# Add dotnet tools to PATH
ENV PATH="${PATH}:/home/vscode/.dotnet/tools"

# Set working directory
WORKDIR /workspaces/Multi-SendSpin-Player-Container

# Default to bash
CMD ["bash"]
