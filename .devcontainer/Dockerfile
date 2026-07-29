FROM mcr.microsoft.com/devcontainers/dotnet:1-8.0-bookworm

ARG NODE_VERSION=20

RUN rm -f /etc/apt/sources.list.d/yarn.list \
    /etc/apt/sources.list.d/yarn.sources \
    && curl -fsSL https://deb.nodesource.com/setup_${NODE_VERSION}.x | bash - \
    && apt-get install -y --no-install-recommends nodejs \
    && npm install -g npm@10 \
    && apt-get clean \
    && rm -rf /var/lib/apt/lists/*