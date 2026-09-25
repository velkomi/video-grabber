ARG BASE_IMAGE=mcr.microsoft.com/dotnet/runtime@sha256:8a153b5889d796b6450295b383596b13308c24c230515f8a7770ce1b94e0c460
FROM ${BASE_IMAGE}

ARG WIREPROXY_VERSION=1.1.3
ARG WIREPROXY_SHA256=e88c1d090740373fc606c1bafd81d9a5eadc642cce5667616e20e9d7a444f51c

RUN apt-get update \
 && apt-get install -y --no-install-recommends ca-certificates curl tar gzip \
 && rm -rf /var/lib/apt/lists/* \
 && curl -fsSL "https://github.com/windtf/wireproxy/releases/download/v${WIREPROXY_VERSION}/wireproxy_linux_amd64.tar.gz" -o /tmp/wireproxy.tgz \
 && echo "${WIREPROXY_SHA256}  /tmp/wireproxy.tgz" | sha256sum -c - \
 && tar -xzf /tmp/wireproxy.tgz -C /usr/local/bin wireproxy \
 && chmod 0555 /usr/local/bin/wireproxy \
 && rm -f /tmp/wireproxy.tgz

USER 0:0
ENTRYPOINT ["/usr/local/bin/wireproxy"]
