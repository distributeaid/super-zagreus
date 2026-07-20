FROM mcr.microsoft.com/devcontainers/dotnet:8.0

# Firewall prerequisites (iptables/ipset used by init-firewall.sh, task 2),
# postgres client for the smoke test, jq/aggregate for GitHub IP-range handling.
RUN apt-get update && apt-get install -y --no-install-recommends \
    iptables \
    ipset \
    iproute2 \
    dnsutils \
    aggregate \
    jq \
    postgresql-client \
  && apt-get clean && rm -rf /var/lib/apt/lists/*

# Pre-create the named-volume mount points with vscode ownership so fresh
# volumes are initialized writable by the container user (no sudo chown needed).
RUN mkdir -p /home/vscode/.claude /home/vscode/.microsoft/usersecrets \
  && chown -R vscode:vscode /home/vscode/.claude /home/vscode/.microsoft

USER vscode
RUN dotnet tool install --global dotnet-ef
ENV PATH=$PATH:/home/vscode/.dotnet/tools
