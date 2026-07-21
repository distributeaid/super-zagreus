#!/bin/bash
set -euo pipefail  # Exit on error, undefined vars, and pipeline failures
IFS=$'\n\t'       # Stricter word splitting

# 1. Extract Docker DNS info BEFORE any flushing
DOCKER_DNS_RULES=$(iptables-save -t nat | grep "127\.0\.0\.11" || true)

# Flush existing rules and delete existing ipsets
iptables -F
iptables -X
iptables -t nat -F
iptables -t nat -X
iptables -t mangle -F
iptables -t mangle -X
ipset destroy allowed-domains 2>/dev/null || true

# Fail closed from the very first instant: default-DROP before anything is
# (re)built. `iptables -F` clears rules but NOT chain policies, and this
# script re-runs on every container start (postStartCommand), so at no
# point may the policies pass through ACCEPT -- a concurrently running
# process could exfiltrate during such a window. Re-runnability from an
# enforced state is provided by the narrow ZAGREUS-BOOTSTRAP rules below.
iptables -P INPUT DROP
iptables -P FORWARD DROP
iptables -P OUTPUT DROP

# IPv6: the compose network is IPv4-only, so no v6 allowlist is needed --
# lock IPv6 egress down entirely (default ip6tables policy is ACCEPT,
# which would otherwise leave an untouched escape hatch): default-DROP
# with only loopback and already-established traffic permitted.
ip6tables -F
ip6tables -X
ip6tables -P INPUT DROP
ip6tables -P FORWARD DROP
ip6tables -P OUTPUT DROP
ip6tables -A INPUT -i lo -j ACCEPT
ip6tables -A OUTPUT -o lo -j ACCEPT
ip6tables -A INPUT -m state --state ESTABLISHED,RELATED -j ACCEPT
ip6tables -A OUTPUT -m state --state ESTABLISHED,RELATED -j ACCEPT

# 2. Selectively restore ONLY internal Docker DNS resolution
if [ -n "$DOCKER_DNS_RULES" ]; then
    echo "Restoring Docker DNS rules..."
    iptables -t nat -N DOCKER_OUTPUT 2>/dev/null || true
    iptables -t nat -N DOCKER_POSTROUTING 2>/dev/null || true
    echo "$DOCKER_DNS_RULES" | xargs -L 1 iptables -t nat
else
    echo "No Docker DNS rules to restore"
fi

# Temporary fail-closed bootstrap: rebuilding the allowlist below needs
# exactly two things -- DNS, and HTTPS to api.github.com (for /meta).
# Grant only that, in dedicated chains that are flushed and removed again
# before the final REJECT tail is installed, so the final ruleset contains
# only intended rules and arbitrary egress is never possible mid-run.
iptables -N ZAGREUS-BOOTSTRAP-IN
iptables -N ZAGREUS-BOOTSTRAP-OUT
iptables -I INPUT 1 -j ZAGREUS-BOOTSTRAP-IN
iptables -I OUTPUT 1 -j ZAGREUS-BOOTSTRAP-OUT
iptables -A ZAGREUS-BOOTSTRAP-IN -i lo -j ACCEPT
iptables -A ZAGREUS-BOOTSTRAP-IN -m state --state ESTABLISHED,RELATED -j ACCEPT
iptables -A ZAGREUS-BOOTSTRAP-OUT -o lo -j ACCEPT
iptables -A ZAGREUS-BOOTSTRAP-OUT -m state --state ESTABLISHED,RELATED -j ACCEPT
iptables -A ZAGREUS-BOOTSTRAP-OUT -p udp --dport 53 -j ACCEPT

echo "Resolving api.github.com for bootstrap..."
bootstrap_ips=$(dig +noall +answer A api.github.com | awk '$4 == "A" {print $5}')
if [ -z "$bootstrap_ips" ]; then
    echo "ERROR: Failed to resolve api.github.com for bootstrap"
    exit 1
fi
while read -r ip; do
    if [[ ! "$ip" =~ ^[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}$ ]]; then
        echo "ERROR: Invalid IP from DNS for api.github.com: $ip"
        exit 1
    fi
    echo "Adding bootstrap rule for api.github.com $ip"
    iptables -A ZAGREUS-BOOTSTRAP-OUT -d "$ip/32" -p tcp --dport 443 -j ACCEPT
done < <(echo "$bootstrap_ips")

# First allow DNS and localhost before any restrictions
# Allow outbound DNS
iptables -A OUTPUT -p udp --dport 53 -j ACCEPT
# Allow inbound DNS responses
iptables -A INPUT -p udp --sport 53 -j ACCEPT
# Allow outbound SSH
iptables -A OUTPUT -p tcp --dport 22 -j ACCEPT
# Allow inbound SSH responses
iptables -A INPUT -p tcp --sport 22 -m state --state ESTABLISHED -j ACCEPT
# Allow localhost
iptables -A INPUT -i lo -j ACCEPT
iptables -A OUTPUT -o lo -j ACCEPT

# Create ipset with CIDR support
ipset create allowed-domains hash:net

# Fetch GitHub meta information and aggregate + add their IP ranges
echo "Fetching GitHub IP ranges..."
gh_ranges=$(curl -s https://api.github.com/meta)
if [ -z "$gh_ranges" ]; then
    echo "ERROR: Failed to fetch GitHub IP ranges"
    exit 1
fi

if ! echo "$gh_ranges" | jq -e '.web and .api and .git' >/dev/null; then
    echo "ERROR: GitHub API response missing required fields"
    exit 1
fi

echo "Processing GitHub IPs..."
while read -r cidr; do
    if [[ ! "$cidr" =~ ^[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}/[0-9]{1,2}$ ]]; then
        echo "ERROR: Invalid CIDR range from GitHub meta: $cidr"
        exit 1
    fi
    echo "Adding GitHub range $cidr"
    ipset add -exist allowed-domains "$cidr"
done < <(echo "$gh_ranges" | jq -r '(.web + .api + .git)[]' | aggregate -q)

# Resolve and add other allowed domains.
# Groups: upstream Claude/VS Code defaults; Claude login; Yarn; .NET/NuGet;
# OAuth token verification for the API (Google/Microsoft).
for domain in \
    "registry.npmjs.org" \
    "api.anthropic.com" \
    "sentry.io" \
    "statsig.com" \
    "marketplace.visualstudio.com" \
    "vscode.blob.core.windows.net" \
    "update.code.visualstudio.com" \
    "claude.ai" \
    "console.anthropic.com" \
    "platform.claude.com" \
    "repo.yarnpkg.com" \
    "api.nuget.org" \
    "dot.net" \
    "builds.dotnet.microsoft.com" \
    "ci.dot.net" \
    "oauth2.googleapis.com" \
    "www.googleapis.com" \
    "login.microsoftonline.com" \
    "graph.microsoft.com"; do
    echo "Resolving $domain..."
    ips=$(dig +noall +answer A "$domain" | awk '$4 == "A" {print $5}')
    if [ -z "$ips" ]; then
        echo "ERROR: Failed to resolve $domain"
        exit 1
    fi

    while read -r ip; do
        if [[ ! "$ip" =~ ^[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}$ ]]; then
            echo "ERROR: Invalid IP from DNS for $domain: $ip"
            exit 1
        fi
        echo "Adding $ip for $domain"
        ipset add -exist allowed-domains "$ip"
    done < <(echo "$ips")
done

# Allow the compose-internal network(s): the db service and the VS Code
# port-forward path live on directly-attached subnets (kernel routes).
for subnet in $(ip -o -f inet route show proto kernel | awk '{print $1}'); do
    echo "Allowing attached subnet $subnet"
    iptables -A INPUT -s "$subnet" -j ACCEPT
    iptables -A OUTPUT -d "$subnet" -j ACCEPT
done

# Default policies are already DROP (set fail-closed at the top of this
# script, before the allowlist build); reassert for clarity.
iptables -P INPUT DROP
iptables -P FORWARD DROP
iptables -P OUTPUT DROP

# First allow established connections for already approved traffic
iptables -A INPUT -m state --state ESTABLISHED,RELATED -j ACCEPT
iptables -A OUTPUT -m state --state ESTABLISHED,RELATED -j ACCEPT

# Then allow only specific outbound traffic to allowed domains
iptables -A OUTPUT -m set --match-set allowed-domains dst -j ACCEPT

# Tear down the temporary bootstrap chains: the final rules above now
# cover everything legitimate, and removing these before the REJECT tail
# keeps the finished ruleset limited to intended rules only. In-flight
# connections opened during bootstrap survive via the ESTABLISHED rule.
iptables -D INPUT -j ZAGREUS-BOOTSTRAP-IN
iptables -D OUTPUT -j ZAGREUS-BOOTSTRAP-OUT
iptables -F ZAGREUS-BOOTSTRAP-IN
iptables -F ZAGREUS-BOOTSTRAP-OUT
iptables -X ZAGREUS-BOOTSTRAP-IN
iptables -X ZAGREUS-BOOTSTRAP-OUT

# Explicitly REJECT all other outbound traffic for immediate feedback
iptables -A OUTPUT -j REJECT --reject-with icmp-admin-prohibited

echo "Firewall configuration complete"
echo "Verifying firewall rules..."
if curl --connect-timeout 5 https://example.com >/dev/null 2>&1; then
    echo "ERROR: Firewall verification failed - was able to reach https://example.com"
    exit 1
else
    echo "Firewall verification passed - unable to reach https://example.com as expected"
fi

# Verify GitHub API access
if ! curl --connect-timeout 5 https://api.github.com/zen >/dev/null 2>&1; then
    echo "ERROR: Firewall verification failed - unable to reach https://api.github.com"
    exit 1
else
    echo "Firewall verification passed - able to reach https://api.github.com as expected"
fi

# Verify Anthropic API access
if ! curl --connect-timeout 5 https://api.anthropic.com >/dev/null 2>&1; then
    echo "ERROR: Firewall verification failed - unable to reach https://api.anthropic.com"
    exit 1
else
    echo "Firewall verification passed - able to reach https://api.anthropic.com as expected"
fi
