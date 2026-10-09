# Client updates ledger

Base 361d3f2681ca39b250196a767a0b54e8471e7483. Existing linked isolated worktree reused, branch codex/videograbber-client-updates-20261009. Owner approved notification/service-migration proposal with “да”; AGENTS7 requests continuing sufficient, reversible work without repeated routine confirmation. Detailed spec operationalizes approved scope, no new domain or DB relocation.

Ruling: stage1 verified download + explicit manual installer launch instructions, not auto-run/forced close — owner requested notifications and user-controlled updates; preserves live media jobs and avoids surprise UAC/termination. Signed rollback package exposed explicitly, not automatic downgrade.

Ruling: publisher is offline CLI, API only reads public signed file — production private key outside repository/container. No extra dependencies/services. Root handles protected key generation only after protocol tests and review; no delegate production side effects.

Ruling: expiry is enforced for active migrated config; it cannot silently return auth tokens to shipped former domain. Anonymous bootstrap/mirror can recover metadata, DPAPI/queue remain untouched. Same-site renewed catalog updates cache validity.

State: protocol/publisher/API/cache/transport/directory gate and native UI implemented. No production key, signed live catalog or new release published yet.

Task1: first protocolRED41fail/8pass thenCore86GREEN; projectlegacy preview.N-rc.M comparison actual9→10 bug fixed with explicitdomainhelper (standardSemVer retained), Core98GREEN. Task2: publisher17/API15GREEN includingprivatekey owner+foreignACE denial andjunction/traversal safety. Root fixedtools ignore andregisteredsolution/API Coreprojectreference; externaldependencylock entries unchanged.

Root: cacheinitial missingtypesRED thenfixture reserved.test DNS corrected toexample.com,6GREEN;reviewcorruption/recovery/wrapper6RED then12GREEN. Signedhighwaterproof separatelyatomic firstwrite, recoverypending remainsblockeduntilapproval. TransportmissingtypesRED then8GREEN;bodydeadlineactualREDthen20secmetadata/30secidle+30mininstallerGREEN. Decision3tests legacyorderfailurefixed. DirectorymissingtypeRED,expiry/sameoriginrenewal/authorigin/corrupt andmigrationcases GREEN;initialbootstrap+multipleapprovals2actualRED fixedbyretainedruntimeproof. CombinedInfra32GREEN.

Wholefeature independentreview accepted (protocol-publisher-review.md). Core98,Infra744PASS/1unrelatedWhisperskip,publisher17,API15. NormalManagedcompileandLocalsolutionpass;2temporaryfilelockwarningsfromconcurrentbuildreplacedbyfreshsequential0warnings0errorsbuild. QAcompilepasses; finalprobeStart-Process rejectedbyautomaticapprovalreview, no reason beyondblockedbypolicy; no alternate launch tried. Test-only syntheticupdate/migration/longnotice probes isolatedAppData andno network remainNOT_RUN.

LinuxbroaderisolatedPlatform/publisher attemptNOT_RUN: preexistingcachedSDKimage4bee absent, dockerexit125; productionDBunchanged. ExistingtestPostgres andprotectedtestpasswordusedonlyforplannedisolatedtests, no schema/application changes. RelevantAPI15 endpointtests ranlocally with ephemeralKestrel/noDB.

Ruling: production signing-key generation/public pin/catalog activation are an explicit final owner-authorized security-sensitive release action. Complete code/tests/review first; request that approval before new permanent trust root. No new site/database transfer. Deployment/publication prior general authorization noted, but newkeyauthority must be concrete and separatelyconfirmed.

Owner explicitlyconfirmed final release with “Делаем”. QA launch stillrejected byautomaticreview, no alternate launch tried. Key generatedwithreviewedCLI onLinux usingexistingruntime image andnetworknone;0600private,0700directory,privateoutsideGit/API. PublicKeyId vg-client-20261009 pinned;publicPEM SHA25696c74c54357d56e53eeee02b8c7c08bd2e39bb0a7595e1923bed5da03822b411. ProtectedcopyandWindows DPAPI off-serverbackup roundtripverified;privatebytesneverprinted/plainlocalfilecreated. Currentsite/DB/authkeysnotchanged.
