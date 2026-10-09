# Offline client release publisher

Uses .NET 10 and built-in RSA-PSS/SHA-256. No network or database access. These commands are intended for an owner-controlled offline signing machine. Production key generation is a separate owner-authorized release action.

```powershell
dotnet run --project tools/VideoGrabber.ClientReleasePublisher -- generate-key --private-key D:/Protected/preview-private.pem --public-key D:/Protected/preview-public.pem --key-id preview-2026
dotnet run --project tools/VideoGrabber.ClientReleasePublisher -- sign --input D:/Release/draft.json --private-key D:/Protected/preview-private.pem --key-id preview-2026 --output D:/Release/preview.json
```

All parent directories must already exist. Outputs must be unused paths; the tool refuses to replace existing keys/catalogs. Signing validates the draft before opening the key, then writes the complete envelope through a flushed temporary file and atomic rename in the destination directory. To publish a new catalog, sign to a fresh staging file and replace the served catalog only in the approved release workflow.

Private keys are PKCS#8 PEM, RSA 3072 bits. On Unix the private file is created with mode `0600`; on Windows its DACL allows only the current user. Signing requires the current Windows user as actual file owner and accepts allow ACEs only for that user or SYSTEM; Unix group/other permissions are rejected. Public files contain SubjectPublicKeyInfo PEM; stdout reports the key id or signed sequence/version, never key bytes. Keep private files and their protected parent directory outside Git, downloads, API containers and public backups.

The draft uses `VideoGrabber.Platform.Contracts.ClientUpdates.ClientReleaseManifest` with strict camelCase JSON. Schema/product/channel/realm are `1` / `videograbber` / `preview` / `videograbber-main`. Supply the real release version, monotonic sequence, validity interval, API/site HTTPS origins, exact installer byte size and SHA-256. The optional rollback installer must reference a lower version. The client contract and validator are authoritative; invalid, oversized or duplicate-field input fails without creating output.

Public API configuration:

- `VG_CLIENT_RELEASE_MANIFEST_PATH`: default `/var/lib/videograbber/downloads/client-release.json`; served anonymously at `/v1/client-release/preview`, maximum 64 KiB, `no-store`.
- `VG_WINDOWS_RELEASES_PATH`: default `/var/lib/videograbber/downloads/releases`; immutable setup path `VERSION/VideoGrabber-Setup.exe`, served at `/download/windows/releases/VERSION/setup`. Version strings are parsed by the shared SemVer policy. Linked version directories/files are rejected. Range requests are supported.

The API has no signing key. It checks envelope syntax/bounds before serving; clients verify the pinned signature and full manifest policy. Existing latest/portable/checksum routes are unchanged. Publish the matching public key pin, immutable binaries and mirror metadata through the owner-approved release procedure.

Exit codes: `0` success, `1` input/key/filesystem failure, `2` command syntax error.
