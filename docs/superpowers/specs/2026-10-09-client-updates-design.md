# VideoGrabber: уведомления об обновлении и перенос адресов

Основание: владелец попросил предусмотреть уведомления в EXE о новой версии и новом сайте при прежнем аккаунте, затем подтвердил предложенную схему словом «да». Реализация первого этапа: проверка подписанного каталога, уведомление, скачивание проверенного установщика по нажатию пользователя, смена адресов после подтверждения/перезапуска. Фактического нового российского домена пока нет; текущие адреса остаются действующими.

## Наблюдаемая архитектура

Снимок: 361d3f2681ca39b250196a767a0b54e8471e7483, preview.62. WinUI создаёт managed HttpClient до восстановления защищённой DPAPI-сессии. URL API задан в MainWindow.Managed.cs, URL сайта/тарифов также встречается в Information/FeatureUx. Автоматического обновления EXE нет. Сайт уже раздаёт Setup/ZIP/checksum. Используются .NET 10, встроенный RSA и существующие библиотеки; новые зависимости не нужны.

## Контракт каталога

Namespace `VideoGrabber.Platform.Contracts.ClientUpdates`.

- `SignedClientRelease(string KeyId,string Payload,string Signature)` — Base64 UTF-8 JSON payload, RSA-PSS/SHA-256 signature, pin key by KeyId. Algorithm is fixed by implementation, never selected by network input.
- `ClientReleaseManifest(int SchemaVersion,string Product,string Channel,long Sequence,DateTimeOffset IssuedAt,DateTimeOffset ExpiresAt,string AccountRealm,ClientServiceEndpoints Services,ClientUpdateRelease Release)`.
- `ClientServiceEndpoints(Uri ApiBase,Uri WebsiteBase)`.
- `ClientUpdateRelease(string Version,DateTimeOffset PublishedAt,string Notes,ClientUpdateArtifact Installer,ClientUpdateArtifact? RollbackInstaller)`.
- `ClientUpdateArtifact(string Version,Uri Url,long SizeBytes,string Sha256)`.

Product `videograbber`, channel `preview`, account realm `videograbber-main`. Schema 1. Sequence positive, monotonically increasing; equal sequence with different payload rejected by client cache. Maximum envelope 64 KiB, decoded payload 32 KiB, notes 1500 characters, installer 1 GiB. Manifest issuedAt not >5min in future, expiresAt >now and >issuedAt, validity <=366 days. Core verifies schema/realm/channel/product, timestamps, signature, Base64 bounds, version and artifact URLs/money-independent byte sizes. Only public HTTPS DNS endpoints: no credentials, fragments, IP literals, localhost or dotless hosts. API base is origin root `/`; website may have a path but no query. Artifact URL may have query, but must remain HTTPS.

SemVer supports numeric prerelease identifiers, strips build metadata, compares preview.9 vs preview.10 correctly. Installer.Version must equal Release.Version; rollback version strictly lower. SHA-256 exactly64 hex characters. No forced downgrade.

## Trust and publication

Public key embedded in shared Core `ClientReleaseTrust`; private PKCS8 key outside Git and API container, owner-controlled protected directory. API serves signed file only, never contains private key. Publisher uses built-in .NET RSA, signs raw serialized payload and emits envelope, validates unsigned draft first. A backup manifest at `https://raw.githubusercontent.com/velkomi/video-grabber/main/releases/windows/preview.json` supplements the current `/v1/client-release/preview`; clients use an anonymous HTTP transport and never send tokens/cookies to these channels. Signature remains authoritative for any mirror/new domain.

Manifest and immutable `/download/windows/releases/{version}/setup` are served from the existing readonly downloads volume. Missing catalog returns404; oversize/bad file503. Versioned routes strictly validate version and stay within downloads/releases; no arbitrary files. Current/latest downloads remain unchanged.

## Client flow

At launch and every24h while open, quietly check catalog; manual “Проверить обновления” in Information. Failure does not interrupt downloads or erase auth. Startup reading signed cache is synchronous/local before managed client setup; cached active endpoint must be valid, signed, correct realm. Expired active configuration never falls back silently to an old host with authentication: block online managed requests until refreshed, preserve DPAPI/offline state. Initial installation without cache uses shipped current origin. Explicit environment override remains a deliberate local diagnostic override, not network authority.

Cache keeps latest checked envelope/high-water plus separately active/previous services envelopes. Reject replay below high-water and same-sequence substitution. Corruption cannot introduce a host. Persist atomically; no access/refresh tokens in update storage/logs. Approved new services are saved separately for next app restart. “Позже” leaves active services. A same-site release can refresh active envelope validity safely; expiry/invalid signature produces neutral manual-check status, no raw payload/URLs in logs.

New EXE notification: “Доступна новая версия VideoGrabber” with “Обновить”/“Позже”. Download is anonymous, redirects only HTTPS, max3 hops, size/hash verified before exposing file. No auto-execution, process killing or automatic OS upgrade. On completion show instructions to finish downloads, close the app and run the installer, plus “Открыть папку”. A signed previous-version artifact is available for explicit rollback; no automatic downgrade. Existing NSIS preserves AppData. In-flight media jobs therefore stay intact.

Address notification: “VideoGrabber переехал на новый сайт. Ваш аккаунт сохранён” with new public site and “Применить после перезапуска”/“Позже”. Only a valid same-realm signature can change API/website. All app website/pricing links use active directory. Existing HttpClient and background worker are never retargeted mid-job. Migration deployment must preserve account IDs, auth issuer/audience, session signing and encryption keys; browser cookies on another domain may require login. No database transfer is performed by this feature.

Root notice uses current Studio style, wraps on narrow widths and supports keyboard; Information card shows current/latest version and progress/status. No VPS/crypto internals in ordinary customer copy. QA builds skip real update network calls.

## Acceptance

Tests: signature tamper/unknown key/expired/wrong realm, downgrade/replay, SemVer numeric prerelease, unsafe URLs and size limits; cache corruption/expiry/pending migration/active rollback; download interruption/cancellation/truncation/oversize/hash mismatch/HTTPS redirects/no credentials; API missing/present immutable files and traversal; publisher roundtrip with ephemeral test keys and atomic output. Build both Local and Managed. UI fixture renders idle/new version/migration/download ready/error at desktop/mobile widths without real installs. Current live signed catalog and package hashes checked only after release; no fake “new version” production campaign or domain.
