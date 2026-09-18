ARG SDK_IMAGE=mcr.microsoft.com/dotnet/sdk@sha256:4beef5b8919dcaa2dc924233bd069257e883cc7a061e09088a97d152d6a48510
ARG RUNTIME_IMAGE=mcr.microsoft.com/dotnet/aspnet@sha256:6a94333d37514e385650a3c81a55e5350b67253dbe136e9cf17e499c35606a8c
FROM ${SDK_IMAGE} AS build
WORKDIR /src
COPY . .
RUN dotnet restore src/VideoGrabber.Platform.Api/VideoGrabber.Platform.Api.csproj --locked-mode
RUN dotnet publish src/VideoGrabber.Platform.Api/VideoGrabber.Platform.Api.csproj \
    -c Release --no-restore -o /out

FROM ${RUNTIME_IMAGE} AS runtime
RUN useradd --system --uid 10001 --create-home --home-dir /var/lib/videograbber vgapi
WORKDIR /app
COPY --from=build /out/ .
USER 10001
ENV ASPNETCORE_URLS=http://0.0.0.0:8080
EXPOSE 8080
ENTRYPOINT ["dotnet","VideoGrabber.Platform.Api.dll"]
