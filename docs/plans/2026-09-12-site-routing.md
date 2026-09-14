# Site-specific network routing implementation plan
Goal: saved per-domain physical-interface rules inside VideoGrabber, without editing OS routes, DNS, hosts, VPN, or firewall.
Approval: user requested persistent per-site routing after reviewing the proposed direct/VPN comparison.
Evidence: artifacts/site-routing-20260912/interface-probe.json; system path timed out, interface 7 connected with verified TLS and HTTP 302 in 0.21s.
Architecture: in-process loopback SOCKS5 TCP relay shared by yt-dlp and embedded WebView2. Destination hostname selects an explicit rule; other hosts use the OS route. TLS stays end-to-end.
No invented public IP, cloud proxy, interception certificate, privileged service or silent failover. Selected-interface failure must NOT fallback to system.
The outgoing interface is chosen per socket using Windows IP_UNICAST_IF; current IPv4/index are refreshed from saved adapter ID.
Windows system HTTP/PAC proxies are distinct from VPN routes: when routing is enabled this version uses its own app proxy. Warn in UI; do not claim inherited HTTP-proxy behavior.

## Six verified deliveries
1. Rule policy + atomic persistence: exact normalized DNS host by default; optional subdomains, most-specific match; reject wildcards/credentials/private-IP/invalid input; empty rules use original app behavior.
   Files: Infrastructure/Networking/SiteRouteSettings.cs; Tests/SiteRouteTests.cs. Test RED then GREEN, roundtrip and corrupted data.
2. Physical-interface socket connector: re-resolve host, reject non-public destinations and non-web ports; no fallback if adapter down, capped connect timeout and cancellation.
   Files: Infrastructure/Networking/RouteConnector.cs. Test private address ranges, IPv4 mapping, adapter mismatch, real TLS.
3. Bounded SOCKS5 relay: bind only 127.0.0.1 random port, no-auth protocol for browser, CONNECT only ports80/443, no UDP, <=32 active clients, handshake timeout, bounded buffers; close all tunnels on dispose.
   Files: Infrastructure/Networking/SiteRouteProxy.cs. Tests protocol/auth/version/private-IP/port rejection, real relay and shutdown.
4. App integration: separate settings card, domain/adapter/subdomain selectors, add/remove/save, connection check. Network changes close embedded session and are blocked while media is busy.
   Files: App/MainWindow.Network.cs; minimal hooks in MainWindow, Download, Browser. Proxy only when rules exist; no global environment mutation.
5. Download regression: proxy endpoint passed only if configured, retain --progress and all other flags, preserve no-proxy existing tests. Browser uses same endpoint with original TLS verification.
   Tests: recording runner + actual HTTPS page in installed browser, downloaded fixture via actual yt-dlp.
6. Build package preview.4 in new folder; preserve v0.1.9 and existing scheduled tasks; no git push. Run all real media/Whisper tests, smoke new UI, save evidence and local commit.

## Acceptance / boundaries
Selected course website must reach TLS/HTTP through physical connection with VPN still active. Full private lesson requires owner's sign-in and is a distinct unverified criterion.
Do not hardcode destination IP or gateway. Domain rules do not automatically include external CDN/login domains; user can explicitly add required hosts.
Direct physical selection is IPv4 in this release. Missing adapter or blocked provider yields an honest error, not VPN shutdown or silent fallback.
WebView2 proxy switches are runtime-sensitive; label preview and test actual installed runtime. App relay is not a machine-wide VPN/privacy product.
Logs: record host/routing mode/result, not URLs/query/cookies/passwords/traffic bodies. Existing 30-day/size retention stays.
