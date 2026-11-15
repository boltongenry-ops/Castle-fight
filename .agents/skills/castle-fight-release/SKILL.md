---
name: castle-fight-release
description: Prepare, test and publish Castle Fight Android updates, diagnose APK installation and poison tick regressions, and preserve compatible content and signing identity.
---

# Castle Fight development and releases

Use the existing checkout: each cloud task is isolated; do not create a worktree unless requested. Read root AGENTS.md and inspect git status before changes. Preserve user changes. Never publish or push unless the user authorized that action. Saving environment settings does not publish or execute them; do not repeat earlier environment changes after publication.

## Project and editor

Canonical project: UnityProject, Unity 6000.6.4f1, revision 12bfff696524. Launcher: source/Launcher. Do not recreate the old partial source/Unity export.

Before opening a new checkout:

```sh
python3 tools/unity_assets.py restore
python3 tools/unity_assets.py verify
```

These restore and verify 13,750 external assets; the project is approximately 39 GiB. Preserve .meta GUIDs and immutable GitHub asset packs. Library, Temp, Builds and signing material stay out of Git. Check free disk space before downloads and imports.

The editor is currently /workspace/tools/unity-6000.6.4f1/Editor/Unity. /workspace/tools/run-unity sets writable XDG_CONFIG_HOME, XDG_CACHE_HOME, XDG_DATA_HOME and UPM_CACHE_ROOT under /workspace/unity-state. Future environments must verify paths rather than assume this filesystem was restored. Set GRADLE_USER_HOME to a writable private directory when necessary.

Official installation metadata: https://services.api.unity.com/unity/editor/release/v1/releases?version=6000.6.4f1 and https://download.unity3d.com/download_unity/12bfff696524/unity-6000.6.4f1-linux.ini. Verify artifacts against official integrity values, preserve TLS and checksum verification. Linux metadata currently points to the Android support .pkg: it contains a xar archive with a compressed cpio Payload. Extract the payload into Editor/Data/PlaybackEngines/AndroidPlayer. SDK/JDK/NDK destinations and checksums are in the official release metadata. Preserve executable bits AND ZIP symlinks: Python ZipFile.extractall alone turns NDK clang/clang++ symlinks into text files, causing 'clang: not found'. Check clang++ --version after extraction.

## Authentication

Editor -version working does not establish licensing. Unity Personal activation was needed here. The official Unity CLI supports auth login with a remote sign-in URL; pass that generated URL to the user, never fabricate a link or request passwords/tokens in chat. Check the process result and auth status. In a container without a keyring, login may fail with KEYRING_WRITE_FAILED. Clearly disclose any use of --insecure-storage; restrict file permissions and keep tokens outside the checkout and out of snapshots, release assets, scripts and logs. Do not print token values. Use existing task authorization for installation and license activation. Never buy a subscription without explicit authorization.

## Validation

```sh
/workspace/tools/run-unity -batchmode -nographics -quit \
  -projectPath /workspace/Castle-fight/UnityProject \
  -executeMethod BuildFullMap.CompileCheck -logFile /workspace/unity-compile.log
```

Require exit 0 and CF_COMPILE_CHECK_OK. This checks compilation, not gameplay. FinalRaceScenarios.PoisonRefresh accepts Nerubian and Human JSON catalogs plus a check callback; run it through a temporary Editor runner. It contains six real simulation checks and is also included in the Nerubian scenario. The regression failed before the fix, passed afterward. Remove temporary runner .cs and .meta files after use.

Do not claim device installation, screenshots, gameplay or Windows/Android tests unless actually executed. Preserve exit codes and verify fresh result files.

## Release both platforms (required)

The user requires EVERY update to ship both Android and Windows. Never silently publish an Android-only or Windows-only update. Build and validate both before switching the live channel. Use the same visible release version in Android and PcGame, and in Windows PlayerSettings for full builds; preserve matching legacy metadata for verified code-only Windows updates.

Windows entry point: `BuildWindowsRelease.Build`, Editor arguments `-batchmode -nographics -quit -buildTarget Win64 -projectPath /workspace/Castle-fight/UnityProject -executeMethod BuildWindowsRelease.Build -logFile /workspace/windows-release.log`. It runs the six poison simulation regressions, rebuilds the indexed supplemental bundle and builds the saved scene without historical local archives or PowerShell. Package and verify the portable separately before publication. For code-only releases, restore EVERY player file from a previous portable whose whole-file SHA-256 and per-entry hashes match its published index, verify UnityPlayer.dll is the same Unity version, and set `CF_WINDOWS_SCRIPT_ONLY=1`. The entry point checks resources.assets, globalgamemanagers and sharedassets0.assets stay byte-identical and replaces the supplemental Windows bundle. Do not use this mode if base scene/resources changed. Legacy player data has Application.version 0.20.0; preserve that metadata during script-only builds while PcGame.Version carries the current visible game version. A full scene rebuild is much more expensive and needs no CF_WINDOWS_SCRIPT_ONLY setting.

## Android build and joint publication

1. Fetch and inspect upstream; preserve local fixes. Confirm desired version and versionCode. Previously published 0.32.0 uses code 16; the prepared joint release is 0.32.1/code 17. A replacement APK must have a strictly higher versionCode, the SAME application ID com.castlefight.unitymap and SAME signing certificate.
2. Receive the signing keystore securely, outside the checkout. Read its password through an environment binding or secure prompt. Never commit keys, passwords, personal account data or credential files. Compare the keystore certificate SHA-256 with the actual preceding release APK using keytool and apksigner verify --print-certs before building. Do not substitute a new debug key.
3. Android support needs the matching module, JDK, SDK, build-tools and NDK. Configure AndroidExternalToolsSettings with the installed paths. ARM64 IL2CPP is the existing delivery target. Prefer the pinned module tools. Diagnose Java proxy/trust problems with supported proxy and CA configuration; never disable verification.
4. The reusable entry point is `BuildAndroidRelease.Build`. Set `CF_KEYSTORE_PATH`, `CF_KEYSTORE_PASSWORD`, optional `CF_KEY_ALIAS` and optional `CF_ANDROID_TOOLS_ROOT` privately, then run the Editor with `-batchmode -nographics -quit -buildTarget Android -projectPath /workspace/Castle-fight/UnityProject -executeMethod BuildAndroidRelease.Build -logFile /workspace/android-release.log`. It restores signing settings and prepares a missing build manifest from the existing bootstrap catalog.
5. For code-only updates reuse Assets/MobileBootstrap/content.json and existing immutable content assets. BuildMobileContent.RebuildPlayer expects Builds/ContentAndroid021/manifest.json; when the content is unchanged, copy the bootstrap catalog there. Preserve Startup.unity and link.xml. Keep minimumPlayerVersion at the existing compatible content version rather than forcing an unnecessary full resource rebuild.
6. Configure PlayerSettings.Android signing from runtime inputs, set the intended version/code in the build code, then invoke BuildMobileContent.RebuildPlayer. The output is UnityProject/Builds/MapAndroid/CastleFight-Android-OnlineAssets.apk. Inspect the build log and require actual success. Restore signing settings and project settings afterward; never persist passwords in tracked configuration.
7. The build temporarily moves Resources to RemoteContentSource and StreamingAssets to RemoteStreamingSource. Finally it must restore both. The performance-test package can create a new Resources folder with PerformanceTestRunInfo/Settings JSON files. BuildMobileContent.Restore archives only those known generated files to Builds/AndroidBuildTelemetry; unexpected contents still block restoration. Never delete arbitrary assets to resolve a collision. After interrupted builds recover original folders before retrying. Verify git status contains only intended changes.
8. Verify APK signature, application ID, versionName/versionCode, native ARM64 payload, manifest provider/permissions and new updater Java methods. Compare SHA-256 and size with release metadata. Run focused logic regressions. If no device is available, state the exact installation/runtime validation limit.
9. Prepare release notes and source changes. Upload binaries to a new GitHub release first. Root release.json is the live update channel: update it LAST, after assets are published and checked. Preserve an unchanged launcher section but rebuild the Windows game for every update, preserve all older downloadable content hashes. Do not point the live channel at untested or missing files.
10. The Android manifest callback must be idempotent: repeated builds must leave exactly one UpdateProvider with the expected authority, exported=false and grantUriPermissions=true. Inspect the final binary manifest, not only Java source.

11. Use gh structured arguments or --notes-file/--body-file. Do not run tools/publish_github031.py blindly: it is a historical publisher with fixed version and local artifact expectations. Verify remote release asset sizes/digests and download access, then commit/push the exact source and index changes authorized by the user. Confirm the actual remote HEAD/channel.

## Known bugs and regression intent

- Invalid update path: Unity Application.temporaryCachePath can differ from Context.getCacheDir(). UpdateProvider.downloadPath returns the SAME internal cache APK used by validate and openFile; C# must download there. Keep strict package, signature, version, URI and read-only provider checks. Do not relax validation to accept arbitrary paths.
- An already installed broken updater cannot repair its own code remotely. The bootstrap fix is one manual browser download and installation OVER the existing app, without uninstalling. Subsequent signed builds can use the repaired updater. Do not promise a server-side metadata edit fixes the old client.
- Poison refresh: ApplyEffectData used to replace the effect and zero dotTick. Same-source refreshes of the same active poison now preserve fractional tick time; duration still refreshes. Do not stack poison DPS or multiply neurotoxin repeatedly. Weaker poison remains rejected. Expired effects start fresh. Balance coefficients stay unchanged.

## Portable packaging

`python3 tools/package_windows_release.py --build UnityProject/Builds/MapWindows --reference /private/path/previous-portable.exe --reference-index /private/path/previous-release.json --output /workspace/release/VERSION/CastleFight-Portable.exe` verifies the reference and every output entry. The portable format is prefix + standalone ZIP + 64-byte footer: little-endian int64 ZIP offset, int64 ZIP length, 32-byte SHA-256 of ZIP, and ASCII `CFPORTABLE-V1!!!`. A plain appended ZIP without this footer is broken. ZIP directory offsets must be relative to the standalone ZIP, while release.json per-file offsets refer to the absolute EXE positions. Keep total EXE below 2^31 bytes for the existing bootstrap. `tools/VerifyPortable.cs` independently verifies the footer, checksum and decompression using .NET ZipArchive; compile/run with an available Mono toolchain. This is container verification, not a Windows GUI launch test. Test a GitHub HTTP Range download against a new compressed entry and check its decompressed SHA-256 before switching the live index.

## Observations from the 0.32.1 release work

- The supplied signing key was checked against the actual 0.32.0 APK before use. Matching alias or a filename such as debug.keystore is NOT proof of identity; the certificate SHA-256 is what matters. Never generate a replacement signing key for an update.
- Android APK checks established package com.castlefight.unitymap, versionName 0.32.1, versionCode 17, minSdk 26, targetSdk 36 and ARM64 IL2CPP. No physical phone was available. Validate the final rebuilt file again, not an earlier intermediate APK.
- Official Android/Windows support packages for this editor are .pkg even in the Linux installer metadata. Their cpio payloads can be extracted on Linux; verify official integrity first. NDK had 35 ZIP symlinks that needed correct extraction. Missing clang links caused an actual failed build, not a game-code failure.
- Java ignores ordinary HTTP_PROXY/HTTPS_PROXY environment variables unless configured. A Java HTTPS probe succeeded using the inherited proxy host/port and /etc/ssl/certs/java/cacerts. Set corresponding Gradle systemProp proxy settings and system trust without disabling TLS. Keep private settings out of Git. First Gradle dependency resolution is slow; subsequent builds reused its cache.
- The performance-test package generates Resources/PerformanceTestRunInfo.json and PerformanceTestRunSettings.json while Android resources are moved. Preserve unexpected contents, archive only these known generated files, restore original folders and check git status. This collision masked an earlier native-build error until diagnosed separately.
- Android manifest postprocessing must update an existing UpdateProvider and remove duplicates. aapt xmltree prints both parsed and Raw values, so substring counting sees one attribute twice: count matching attribute RECORDS instead.
- Full Windows scene builds are expensive for this approximately 39 GiB source project. A long quiet log with high CPU/read activity did not establish a hang. The initial full build was stopped to reuse verified published player data for a code-only fix. Do not describe that aborted run as passed.
- The existing published Windows player is Unity 6000.6.4f1 (12bfff696524), with legacy Application.version 0.20.0. The visible game version comes from PcGame.Version. Keep the baseline metadata consistent when patching its managed code.
- IMPORTANT: Unity BuildScriptsOnly here changed level0, globalgamemanagers, native startup settings and some package assemblies, not only Assembly-CSharp.dll. Its regenerated ResourceManager had different pointers and added resource names. Restoring only the big resources.assets file is insufficient: do not ship mixed old assets and newly generated lookup tables.
- Required code-only finalization: run tools/finalize_windows_code_update.py after the Unity build, with --version set to the new game version, the verified old portable/index, --mono, a compiled tools/CompareWindowsManaged.cs checker, and a private --archive directory. It preserves all original player files except the compatible Assembly-CSharp.dll and explicitly rebuilt centaur supplement, restores altered unrelated files and archives new extras. This release verified unchanged layouts of 103 non-generated types, unchanged assembly dependencies and the actual dotTick fix in the built DLL. Use this approach ONLY when serialized layouts and base resources remain compatible; otherwise do a full build.
- Compile CompareWindowsManaged.cs against the modern Mono.Cecil.dll under Editor/Data/Tools/BuildPipeline/Compilation/Unity.CompilationPipeline.Common, plus the netstandard facade when using mcs. Put Cecil beside the checker EXE. The older lib/mono/unity/Cecil API lacks ReadAssembly; pointing MONO_PATH at that folder can override mscorlib and crash with System.RuntimeType missing. Avoid that override.
- The old portable contains a custom 64-byte footer, not merely an appended ZIP. Inspect/validate its reference SHA-256 and footer before reusing its prefix. The bootstrap copies its standalone ZIP into memory and checks SHA-256; the existing size limit must be respected. Python's ability to open an appended archive is not proof the real bootstrap accepts it.
- The new GitHub release must also include an unchanged copy of CastleFight-Launcher.exe, because README's releases/latest/download link expects that asset. Verify the copied launcher against the previous index. Preserve old content manifests and immutable SHA assets; do not delete older releases.
- The user explicitly requires BOTH Windows and Android for EVERY update. Never switch the global update channel after only one platform is ready. Package, validate, upload and check both, then change release.json LAST. Record untested device/UI behavior honestly.

## Verified code-only Windows commands

After restoring the verified previous player and running BuildWindowsRelease.Build with CF_WINDOWS_SCRIPT_ONLY=1, use the checker and finalizer before packaging:

```sh
cf_unity_data=/workspace/tools/unity-6000.6.4f1/Editor/Data
cf_release_tools=/workspace/release-tools
mkdir -p "$cf_release_tools"
cp "$cf_unity_data/Tools/BuildPipeline/Compilation/Unity.CompilationPipeline.Common/Mono.Cecil.dll" "$cf_release_tools/Mono.Cecil.dll"
"$cf_unity_data/MonoBleedingEdge/bin/mcs" \
  -r:"$cf_unity_data/MonoBleedingEdge/lib/mono/4.5/Facades/netstandard.dll" \
  -r:"$cf_release_tools/Mono.Cecil.dll" \
  -out:"$cf_release_tools/CompareManaged.exe" tools/CompareWindowsManaged.cs
python3 tools/finalize_windows_code_update.py --version NEW_VERSION \
  --build UnityProject/Builds/MapWindows \
  --reference /private/previous-portable.exe --reference-index /private/previous-release.json \
  --mono "$cf_unity_data/MonoBleedingEdge/bin/mono" \
  --compat-check "$cf_release_tools/CompareManaged.exe" \
  --archive /workspace/unshipped-windows-NEW_VERSION
```

The current packager reuses original compressed bytes only when the source file SHA-256 matches the authenticated previous index. Changed files are compressed anew. It regenerates ZIP headers/central directory/footer and decompresses/hashes every final entry. This uses CPython ZipFile internal writing fields and must be revalidated after Python upgrades. Recompressing unchanged multi-GiB resources is unnecessary and was a major delay during this release.

## GitHub upload authentication observation

During 0.32.1 publication the platform-injected GH_TOKEN successfully read/wrote api.github.com and pushed the release tag, but uploads.github.com returned HTTP 401 Bad credentials. Changing Authorization from token to Bearer did not help; POSTing an asset through api.github.com returned 404. Do not assume release creation or Git push establishes release-asset upload access. Do not extract proxy-held credentials or blindly retry uploads.

A supported fallback is normal GitHub CLI device authentication in a separate private GH_CONFIG_DIR outside the repository, with GH_TOKEN unset ONLY for that CLI subprocess. Start `gh auth login --hostname github.com --git-protocol https --web`; answer No to reconfiguring Git so the working platform Git proxy stays intact. Give the user github.com/login/device and the one-time user code, never request a token/password in chat. Keep the login process running. After authorization check login completion and retry one small release asset before the large portable. Use the authenticated config directory for uploads. If local keyring storage is unavailable, disclose any plaintext fallback and restrict file permissions; never include that directory in Git or release assets.

A draft release and remote v0.32.1 tag were created while this upload issue was being resolved. Main's live release.json was deliberately NOT pushed while assets were missing. Before resuming inspect the existing draft/assets to avoid duplicate creation or accidental overwrite. The tag points to source commit 0a02f454; later skill-only documentation commits do not change the built code.

The separate GitHub CLI device login completed successfully for the repository owner. Its small verification.json upload to the same draft release succeeded where the injected GH_TOKEN had returned 401. Credentials fell back to plaintext in the separate private config directory; directory permissions were restricted to 0700 and hosts.yml to 0600. For subsequent upload commands use `env -u GH_TOKEN GH_CONFIG_DIR=/private/release-auth gh release upload ...` so the injected variable does not override that authenticated session. Do not reconfigure the working Git proxy or include the auth directory in Git/release assets.

## Completed publication checks for 0.32.1

Both platform artifacts were uploaded using the separate authenticated GitHub CLI session. GitHub asset metadata confirmed exact sizes and SHA-256 digests for the APK, portable, unchanged launcher, content catalog, release index and verification files. The public Windows DLL Range request returned 206 with the requested Content-Range; raw Deflate decompression matched the indexed DLL SHA-256. The public APK returned HTTP 200 and the published release.json matched the prepared index. Only after these checks should main's live index be pushed and the release marked latest. A repeated baseline finalization reported 341 preserved files, zero additional restorations and zero extra files. The portable was checked by .NET: 343 entries, 6,478,019,892 extracted bytes, 2,097,334,733 ZIP payload bytes. Physical Android installation and Windows GUI execution were not tested.

Final live check: main was pushed successfully, GitHub marked v0.32.1 latest/non-draft with all seven assets uploaded, and the exact unmodified raw main/release.json URL returned 0.32.1 matching the verified local index. The working tree was clean after publication. The first manual APK installation remains necessary for a client whose old updater rejects its own download path.


## Nerubian / Paladin audit and requested tower poison (2026-11-15)

Audit runner: `UnityProject/Assets/Editor/CombatAbilityAudit.cs`. Run `CombatAbilityAudit.Run` for the complete audit (includes 3,920 isolated wave fights, takes minutes), or `CombatAbilityAudit.RunTower` for tower mechanics and the existing Nerubian regression suite. Set CF_AUDIT_OUTPUT to an absolute JSON report path. A failed case is retained in JSON and causes a nonzero Unity exit. These are actual HumanMatch simulations, not screenshots, device tests or full-match balance win rates. Retain the runner as a reusable diagnostic; it replaces the earlier temporary-runner advice for these checks.

Reports are in [references](references/): combat-audit-before-tower-poison.json, tower-audit-before-poison.json and tower-audit-after-poison.json. The first two describe the published 0.32.1 mechanics before the user's new tower change. Do not present their damage measurements as post-change results.

- Matriarch is intentionally hNS1 → hNS2 → hNO1. Correct catalog fixture: nine base buildings, eight upgrades and one native brood-helper row = 18 original presentation rows. A stale ten-base/seven-upgrade assertion was fixed without restoring direct Matriarch construction. Tested actual building, upgrades, 300-wood requirement, 520 cumulative gold, one legendary slot and 41-second production.
- HumanMatchChecks passed 1,781 checks after seeding BOTH combat randomState and private purchasing botRandom in its equal-seed test. Guid-seeded purchasing was why the old fixture diverged. Real bot randomness was not changed.
- Paladin resurrection works through normal Step/autocast. A03H is followup_only: a nearby corpse alone does not trigger it. A03K must first heal a wounded friendly unit, then inner fire and the one-second delayed raise run. Heal costs 33, inner fire 33, resurrection 70 mana; complete fresh chain costs 136 before regeneration. Paladin starts at 200, max 300, regeneration 2.5/s; raise cooldown 40s and friendly-corpse range 900. Low mana, dead caster, enemy/out-of-range corpse and active cooldown correctly block it. Continuous wounded-ally simulation for 120 seconds produced one raise and roughly one remaining mana: repeated healing/buffs compete with resurrection. Do not claim resurrection is broken outright. Its building chain costs 265+260=525 gold and produces every 41.25s, not the incremental row's 260 gold / 28s.
- Nerubian existing suite and added mechanics passed: brood counts/lifetimes, death spiders, chitin, magic-immune air web, multi-web, hunt shared one-proc-per-victim-per-second limit, ambush, third-hit poison scatter, cocoons/cleanse/spider healing, Matriarch aura nonstacking/command, colossus fissure, acid/plague targeting and Neurotoxin poison refresh/transfer rules.
- TWO genuine edge failures were retained, not fixed as part of the requested tower poison: (1) plague kills a 60-HP target on its sixth/final 10-damage tick, but the two nearby wounded allies receive no 30-HP death heal. TickEffects has already set effect.left to zero; NerubDied requires left>0. The spider birth still queues. Death earlier in an active plague correctly heals. (2) direct Raise of a killed temporary summoned/noBounty spider creates a permanent, bounty-bearing replacement with summonLeft=0. This second reproduction is a merged-catalog/direct-Raise diagnostic; a legal Ultimate draw plus normal Paladin followup was NOT executed. Do not claim that specific full player flow was reproduced.
- Balance matrix: eight Nerubian rosters at exactly 1,000 cumulative building gold vs seven enemy compositions, 20 seeded repetitions in both orientations, no producer buildings, economy, wood progression, items or special-building support; 3,920 fights. Most tested Nerubian compositions lost all 40 trials against human/orc mixed/undead groups. Four upgraded guards won 40/40 against human infantry/rifles, but lost 38/40 against human air (two draws). In the baseline mixed mirror Neurotoxin changed 16W/14L/10D to 26W/10L/4D. Poison-heavy rosters remained unsuccessful. This indicates composition dependence and test-scenario weaknesses; it does NOT establish a live faction win rate. Never use one mixed roster to declare an entire race unplayable.
- Compare full upgrade costs and actual m.Interval(hut), not incremental cost/seconds rows. Neurotoxin amplifies slowing poison only: AUN4 4→6 DPS, AUN5 9→13.5 DPS for five seconds. Acid 4 DPS and plague 10 DPS have no slowing-poison flag and are not amplified. Strong poison is not replaced by weak poison; same-source refresh preserves the fractional tick.

### Tower audit and user-authorized change

Before adding poison: hNT1 cost 160 gold/250 wood, HP1200/armor5, piercing 54/1.6=33.75 base weapon DPS and range800. hNT2 costs another 220 gold/250 wood (TOTAL380/500), HP1700/armor7, 92/1.6=57.5 base weapon DPS and the same range. Human h006 costs150/300, HP1500/armor8, 45/.5=90 base weapon DPS, range950. These are raw weapon rates before armor and attack-type multipliers. Orc o000 has TWO weapons: ground22.5/.6 and air70/.4; do not describe it as air-only.

Both Nerubian towers really shoot ground and air. A 30s stationary normal-armor target test before poison received972 and1656 damage respectively (18 landed hits; 19 attacks started, startup/travel/step effects). Weapon picks nearest eligible enemy; the independent web can target a farther flyer while weapon aims at ground. Out-of-range targets are rejected. AUWB costs zero mana, catches ONE flyer for5s with12s cooldown, grounds and roots even magic-immune air, and restores flight afterward. Both levels use the same web. T2 attacks apply a four-second hunt mark; the tower alone does not consume the bonus, allied Nerubian units do. Baseline tower had no poison and no Neurotoxin synergy.

Visual inspection: the separate building icon PNGs resolve correctly and were viewed; T1/T2 show essentially the same silhouette, with shade/frame/two upgrade arrows distinguishing T2. Four correct team/tier prefabs resolve with renderers; clips contain only Stand and Stand Alternate, so there is no dedicated attack animation. This was asset/icon inspection, not live GUI rendering. Presentation row offsets and GUIDs were kept.

User then explicitly requested: “Яд добавь ей”. Implemented AUN4 on base tower and AUN5 on upgraded tower, in all three catalog representations (Nerubian032 supplement, Resources Nerubian catalog, Resources Ultimate catalog), including row/stats/buildingStats ability lists. Extend NerubAfterWeapon to accept both tower levels and apply their catalog poison only after an actual damaging hit, using Valid and ApplyEffect. Unit poison continues through its existing OnHit path. Base poison:4DPS/5s,slow8%; upgraded:9DPS/5s,slow15%,armor−3. Existing Neurotoxin factor applies once; weak/strong nonstacking and magic-immunity validation stay shared with unit poison. Do not sum raw tower DPS and poison DPS as a universal effective DPS: armor reduction, attack-type multipliers, spell resistance and uptime matter.

At the end of the audit this was a source change pending a paired release; the live channel was still0.32.1. See the completed0.32.2 publication below. To ship, compile and verify BOTH Windows and Android and publish a new version/code. Catalog data changed: rebuild the Windows supplement AND Android supplement/content manifest; reusing the old Android0.32.0 immutable content alone will omit tower abilities. Preserve older hash assets. BuildAndroidRelease.Build by itself currently rebuilds only the code APK, so it is insufficient for this data change without regenerating/registering the supplement. Do not overwrite an old immutable manifest/version or call historical AndroidPlayer blindly (it hardcodes content version0.32.0). Do not claim Git source edits have reached installed players.

Post-change verification: CombatAbilityAudit.RunTower exited0, 93 assertions across 13 cases, zero failures. This includes actual ground/air hits by both tiers, per-tier poison/slow/armor values, Neurotoxin factors, real damage ticks, no duplicate poison group, magic immunity, stronger-poison retention, original poison refresh regressions, all existing Nerubian scenarios and prefab/icon references. Full Human checks and wave matrix were completed before this tower-only change; no Android APK or Windows player was built for the new poison yet.


## Paired 0.32.2 release: tower poison

The user authorized publication with “Так обнови”. Version0.32.2 uses Android code18. Runtime/catalog changes are limited to the tower poison and delivery of its revised data; the two unrelated audit edge failures above remain recorded and unfixed.

Use BuildAndroidRelease.BuildUpdate for a data/supplement release: it calls PrepareContent then the signed code build. PrepareContent rebuilds only the indexed centaur-028 compatibility supplement, updates its hash/size/file in a NEW content catalog, registers the same resource keys, sets catalog/minimumPlayerVersion0.32.2, and embeds it in the APK. It reuses all32 other bundles and streaming media unchanged. BuildAndroidRelease.Build still builds only the APK and is appropriate only when existing content already matches.

Because old immutable bundles remain on their existing content host and the new supplement is published on GitHub, MobileContent now uses ContentCatalog031.AssetUrl(catalog,row): optional row.url overrides baseUrl/file. Compatible checks every resolved URL is HTTPS. Verified override selection, unchanged-row fallback, HTTP rejection, and rejection of the new catalog by a0.32.1 player. Hash/size validation and download resume remain in place. The content cache is still content-v1 keyed by SHA, so unchanged cached packs stay reusable. A network Range probe to the existing frontend bundle returned206. New supplement is51,511,886 bytes (about49.1MiB), only23 bytes larger than its predecessor but a new complete bundle download.

Both Unity build processes exited0. RunTower passed93 assertions/13 cases with zero failures in each preparation. Extracted BOTH compiled platform supplements with UnityPy and inspected the actual embedded TextAsset: hNT1/hNT2 have AUN4/AUN5 in row/stats/buildingStats lists. Extracted data.unity3d from the actual APK and verified its embedded content catalog equals the staged new manifest. APK is27,362,621 bytes, com.castlefight.unitymap, version0.32.2/code18, minSdk26,target36, ARM64 IL2CPP. Signature SHA-256 matches the supplied key and previous APK; there is exactly one correctly restricted UpdateProvider and downloadPath is present in DEX. Unity's build summary totalSize was373,721,979, which is NOT the final APK size; use the actual artifact's length.

Windows finalized against authenticated0.32.1 portable/index: 341 baseline files preserved, one unrelated file restored and one extra archived. Managed checker verified unchanged layouts of103 original types/dependencies, poison tick fix, both tower branches and actual ApplyEffect call, plus per-package URL helper. Portable packager checked343 entry hashes. Independent .NET extraction/checksum completed:343 entries,6,478,020,561 extracted bytes,2,097,325,385 ZIP payload bytes. Actual Windows GUI and physical Android execution were not performed.

Build cleanup: Unity can serialize an empty restored AndroidKeystoreName as the literal '{inproject}: '. Restore that empty field before committing; never persist the private signing path or passwords. Keep Android versionCode18 and the explicit Standalone Mono backend. Preserve original CRLF in large catalog sources to keep diffs readable; JSON serialization need not rewrite unrelated13,000-line catalogs. The published binary manifest/catalog is checked semantically against the source/staged manifest.


0.32.2 publication checks completed: the source commit/tag is3df6cbd278a683c52ad76aa4a6f318bc5b25ab6c. All8 assets uploaded using the existing separate GitHub CLI authentication; GitHub state/size/SHA-256 digests match every local file. The portable is2,101,068,169 bytes, SHA-2565dfbeeb11e09dd6527d645bf4567b5cd623fae8c824f956e47390bc480da7561. Release was first published without making it latest; live main/release.json still pointed at0.32.1 during public checks. Full unauthenticated downloads of the new APK, new51MB bundle, content manifest and release index matched hashes. Portable DLL HTTP Range returned206, exact Content-Range, and decompressed SHA matched the new index. Only after this were latest/main channel switched to0.32.2. The launcher section remains unchanged, while an identical launcher asset is present in the new release for README's latest/download link.

GitHub observation: the releases/tags endpoint returned404 for this DRAFT even though gh release view found it. Query the release list or use its numeric release ID while drafting; do not interpret that alone as lost authorization. The50.71MiB tracked Windows source supplement generated GitHub's recommended-size warning but push succeeded (it is below the100MiB limit). Do not migrate repository assets or rewrite history during a small gameplay release merely to silence that recommendation.
