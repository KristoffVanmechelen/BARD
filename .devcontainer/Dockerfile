FROM mcr.microsoft.com/devcontainers/dotnet:1-8.0-bookworm

# Node.js + npm (frontend build/dev tooling)
ARG NODE_VERSION=20
RUN curl -fsSL https://deb.nodesource.com/setup_${NODE_VERSION}.x | bash - \
    && apt-get install -y nodejs \
    && npm install -g npm@latest

# sqlcmd / mssql-tools for readiness checks and manual DB inspection
RUN curl -fsSL https://packages.microsoft.com/keys/microsoft.asc | tee /etc/apt/trusted.gpg.d/microsoft.asc \
    && curl -fsSL https://packages.microsoft.com/config/debian/12/prod.list | tee /etc/apt/sources.list.d/mssql-release.list \
    && apt-get update \
    && ACCEPT_EULA=Y apt-get install -y mssql-tools18 unixodbc-dev \
    && echo 'export PATH="$PATH:/opt/mssql-tools18/bin"' >> /etc/bash.bashrc

# dotnet-ef for migrations
RUN dotnet tool install --global dotnet-ef --version 8.* \
    && echo 'export PATH="$PATH:$HOME/.dotnet/tools"' >> /etc/bash.bashrc

ENV PATH="${PATH}:/opt/mssql-tools18/bin:/root/.dotnet/tools"
