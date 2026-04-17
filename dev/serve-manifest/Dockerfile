# syntax=docker/dockerfile:1.7

# Tiny nginx image that serves /srv at :80, used by the test harness's
# --via-manifest flow. Jellyfin's container reaches it at
# http://manifest-server/manifest.json over the compose network.
FROM nginx:alpine

COPY default.conf /etc/nginx/conf.d/default.conf

# The actual release artifacts (manifest.json + .zip) are bind-mounted
# at /srv by docker-compose, so nothing else needs to be baked in.
EXPOSE 80
