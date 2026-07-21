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

# Install the firewall script; vscode's ONLY sudo right is running it.
# The default devcontainer passwordless-sudo grant is removed so an
# auto-mode agent inside the container cannot simply flush iptables.
#
# Installed as zagreus-init-firewall.sh, NOT init-firewall.sh: the
# ghcr.io/anthropics/devcontainer-features/claude-code:1 feature (see
# devcontainer.json) unconditionally installs its own generic script at
# /usr/local/bin/init-firewall.sh, and devcontainer feature installation
# always runs as a build stage after the Dockerfile's own instructions
# (regardless of instruction order in this file), so a COPY to that exact
# path here would be silently clobbered. A distinct filename avoids the
# collision while keeping the same security property: a root-owned,
# vscode-immutable script that is the sole sudo grant.
COPY init-firewall.sh /usr/local/bin/zagreus-init-firewall.sh
RUN chmod 755 /usr/local/bin/zagreus-init-firewall.sh \
  && rm -f /etc/sudoers.d/* \
  && echo "vscode ALL=(root) NOPASSWD: /usr/local/bin/zagreus-init-firewall.sh" > /etc/sudoers.d/init-firewall \
  && chmod 0440 /etc/sudoers.d/init-firewall

USER vscode
RUN dotnet tool install --global dotnet-ef --version 8.*
ENV PATH=$PATH:/home/vscode/.dotnet/tools
