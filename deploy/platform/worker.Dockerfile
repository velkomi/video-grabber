FROM mcr.microsoft.com/dotnet/sdk@sha256:4beef5b8919dcaa2dc924233bd069257e883cc7a061e09088a97d152d6a48510 AS test-runner
ARG YTDLP_VERSION=2026.09.16.232951
ARG YTDLP_SHA256=f8ca14db511702a5dbfc5a527056312907ddd0914d0b4036f108d6849e17ef61
RUN apt-get update \
 && apt-get install -y --no-install-recommends ca-certificates curl ffmpeg python3 \
 && rm -rf /var/lib/apt/lists/* \
 && curl -fsSL "https://github.com/yt-dlp/yt-dlp-nightly-builds/releases/download/${YTDLP_VERSION}/yt-dlp" -o /usr/local/bin/yt-dlp \
 && echo "${YTDLP_SHA256}  /usr/local/bin/yt-dlp" | sha256sum -c - \
 && chmod 0555 /usr/local/bin/yt-dlp
ENV VG_WORKER_YTDLP=/usr/local/bin/yt-dlp \
    VG_WORKER_FFMPEG=/usr/bin/ffmpeg \
    VG_WORKER_FFPROBE=/usr/bin/ffprobe
WORKDIR /src
COPY . .
RUN dotnet restore tests/VideoGrabber.Platform.Worker.Tests/VideoGrabber.Platform.Worker.Tests.csproj --locked-mode
RUN dotnet build tests/VideoGrabber.Platform.Worker.Tests/VideoGrabber.Platform.Worker.Tests.csproj \
    -c Release --no-restore --nologo
CMD ["dotnet","test","tests/VideoGrabber.Platform.Worker.Tests/VideoGrabber.Platform.Worker.Tests.csproj","-c","Release","--no-build","--no-restore","--nologo"]

FROM mcr.microsoft.com/dotnet/sdk@sha256:4beef5b8919dcaa2dc924233bd069257e883cc7a061e09088a97d152d6a48510 AS build
WORKDIR /src
COPY . .
RUN dotnet restore src/VideoGrabber.Platform.Worker/VideoGrabber.Platform.Worker.csproj --locked-mode
RUN dotnet publish src/VideoGrabber.Platform.Worker/VideoGrabber.Platform.Worker.csproj \
    -c Release --no-restore -o /out

FROM mcr.microsoft.com/dotnet/runtime@sha256:8a153b5889d796b6450295b383596b13308c24c230515f8a7770ce1b94e0c460 AS runtime
ARG YTDLP_VERSION=2026.09.16.232951
ARG YTDLP_SHA256=f8ca14db511702a5dbfc5a527056312907ddd0914d0b4036f108d6849e17ef61
RUN apt-get update \
 && apt-get install -y --no-install-recommends ca-certificates curl ffmpeg python3 \
 && rm -rf /var/lib/apt/lists/* \
 && curl -fsSL "https://github.com/yt-dlp/yt-dlp-nightly-builds/releases/download/${YTDLP_VERSION}/yt-dlp" -o /usr/local/bin/yt-dlp \
 && echo "${YTDLP_SHA256}  /usr/local/bin/yt-dlp" | sha256sum -c - \
 && chmod 0555 /usr/local/bin/yt-dlp \
 && useradd --system --uid 10001 --create-home --home-dir /var/lib/videograbber vgworker \
 && install -d -o vgworker -g vgworker -m 0700 /var/lib/videograbber/jobs
WORKDIR /app
COPY --from=build /out/ .
USER 10001
ENV VG_WORKER_YTDLP=/usr/local/bin/yt-dlp \
    VG_WORKER_FFMPEG=/usr/bin/ffmpeg \
    VG_WORKER_FFPROBE=/usr/bin/ffprobe \
    VG_WORKER_JOB_ROOT=/var/lib/videograbber/jobs
ENTRYPOINT ["dotnet","VideoGrabber.Platform.Worker.dll"]
