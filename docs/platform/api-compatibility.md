# VideoGrabber API compatibility

Platform protocol compatibility is explicit and fail-closed.

- Current protocol: 2.
- Minimum supported protocol: 1.
- Legacy `X-VideoGrabber-Api-Version: 1` remains accepted.
- New clients send `X-VideoGrabber-Protocol`.
- Responses advertise `X-VideoGrabber-Protocol-Current` and `X-VideoGrabber-Protocol-Minimum`.
- Unsupported or malformed protocol values return HTTP 426 with `client_upgrade_required`.

JSON contracts are additive within the supported window. Unknown request fields are ignored only when the known fields form a valid request. Removing or changing the meaning of an existing field requires a protocol change.

## Worker compatibility

Workers declare both a protocol version and supported operation names when claiming work. Protocol-1 workers without an explicit capability list are treated as legacy download-only workers.

The API validates declared capabilities and only leases jobs whose `kind` is supported by that worker. This prevents an old worker from claiming MP3, trim, join, or transcription work it cannot execute.

Current server-worker operations are `download`, `mp3`, `trim`, `join`, and `transcribe` when ASR is configured. Without server ASR, transcription is not advertised.

## Regression contract

`ApiCompatibilityTests` verifies the current and previous protocol, HTTP 426 behavior, legacy API version 1, additive JSON handling, legacy worker filtering, and unsupported worker rejection.

Compatibility is a release gate, not only a unit-test convention. The release gate records the exact source SHA and refuses READY when mandatory automated or live evidence is missing.
