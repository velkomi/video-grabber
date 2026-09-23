ARG SDK_IMAGE=mcr.microsoft.com/dotnet/sdk@sha256:4beef5b8919dcaa2dc924233bd069257e883cc7a061e09088a97d152d6a48510
ARG RUNTIME_IMAGE=mcr.microsoft.com/dotnet/aspnet@sha256:6a94333d37514e385650a3c81a55e5350b67253dbe136e9cf17e499c35606a8c
FROM ${SDK_IMAGE} AS build
WORKDIR /src
COPY . .
RUN dotnet restore src/VideoGrabber.Platform.Api/VideoGrabber.Platform.Api.csproj --locked-mode
RUN dotnet publish src/VideoGrabber.Platform.Api/VideoGrabber.Platform.Api.csproj \
    -c Release --no-restore -o /out

FROM ${RUNTIME_IMAGE} AS runtime
ARG YTDLP_VERSION=2026.09.16.232951
ARG YTDLP_SHA256=f8ca14db511702a5dbfc5a527056312907ddd0914d0b4036f108d6849e17ef61
RUN apt-get update \
 && apt-get install -y --no-install-recommends ca-certificates curl gosu \
 && rm -rf /var/lib/apt/lists/* \
 && curl -fsSL "https://github.com/yt-dlp/yt-dlp-nightly-builds/releases/download/${YTDLP_VERSION}/yt-dlp" -o /usr/local/bin/yt-dlp \
 && echo "${YTDLP_SHA256}  /usr/local/bin/yt-dlp" | sha256sum -c - \
 && chmod 0555 /usr/local/bin/yt-dlp \
 && groupadd --system --gid 10001 vgapi \
 && useradd --system --uid 10001 --gid 10001 --create-home --home-dir /var/lib/videograbber vgapi \
 && install -d -o 10001 -g 10001 -m 0700 /var/lib/videograbber/jobs/uploads
WORKDIR /app
COPY --from=build /out/ .
USER 10001
ENV ASPNETCORE_URLS=http://0.0.0.0:8080 \
    VG_YTDLP_PATH=/usr/local/bin/yt-dlp
EXPOSE 8080
ENTRYPOINT ["dotnet","VideoGrabber.Platform.Api.dll"]
