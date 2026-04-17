# syntax=docker/dockerfile:1.7

# Stage 1 — compile the plugin DLL against the .NET 9 SDK
# (Jellyfin 10.11.x runs on .NET 9, up from .NET 8 in 10.9.x)
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY Directory.Build.props jellyfin.ruleset Jellyfin.Plugin.AgeRating.sln ./
COPY Jellyfin.Plugin.AgeRating/ ./Jellyfin.Plugin.AgeRating/

RUN dotnet build Jellyfin.Plugin.AgeRating.sln \
      -c Release \
      /property:GenerateFullPaths=true \
      /consoleloggerparameters:NoSummary

# Stage 2 — tiny installer image that drops the DLL + meta.json into /config
FROM alpine:3.20
COPY --from=build /src/Jellyfin.Plugin.AgeRating/bin/Release/net9.0/Jellyfin.Plugin.AgeRating.dll /plugin/
COPY dev/meta.json /plugin/meta.json
COPY dev/install-plugin.sh /install-plugin.sh
RUN chmod +x /install-plugin.sh
ENTRYPOINT ["/install-plugin.sh"]
