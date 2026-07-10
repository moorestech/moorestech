---
name: server-tests-immutable-package
description: New server test .cs files need a Unity RESTART (not Refresh/Resolve) to compile/run via the client project
metadata: 
  node_type: memory
  type: project
  originSessionId: e8821b9e-6095-45c2-b0c8-053716a4b996
---

moorestech_server/Assets/Scripts is mounted into moorestech_client as a local `file:` package (`tech.moores.server` in `moorestech_client/Packages/manifest.json`, `file:../../moorestech_server/Assets/Scripts`). Unity treats external `file:` packages as **immutable** and caches their file list at package-resolve time.

**Consequence:** A *newly created* .cs file under moorestech_server (e.g. a new test in `Tests/CombinedTest/...`) is NOT picked up by the client editor. `uloop compile` reports Success but silently omits it; `AssetDatabase.Refresh(ForceSynchronousImport)` and `PackageManager.Client.Resolve()` do NOT help. `uloop run-tests` then returns "No tests found matching the specified filter criteria" even though compile is green.

**How to confirm the cause:** `grep -a "<TypeName>" moorestech_client/Library/ScriptAssemblies/Server.Tests.dll` — if the type is absent and the DLL mtime is stale, the file was never compiled in.

**Fix:** Restart Unity (`uloop launch <client> -r`). On restart the package re-caches and the new file compiles (revealing any real compile errors). After that first restart, *editing* the now-known file recompiles normally — only brand-new files need the restart.

**Editing existing files is fine** — they're already in the cached file list, so normal `uloop compile` recompiles them.

Related: [[editmode-test-domain-reload]] (run-tests `--test-mode` defaults to 1=PlayMode; pass `--test-mode 0` for EditMode).

---

**2026-06-05 UPDATE — contradicted in current toolchain (verify, don't blindly restart):** In session building `ElectricToGearGenerator` (Unity `6000.3.8f1`, uloop-cli `1.7.3` / server `1.6.3`), two BRAND-NEW server test `.cs` were created *after* Unity was already running (`ElectricToGearGeneratorTest.cs`, `ElectricToGearOutputModeProtocolTest.cs`). Both were picked up by `uloop compile` (0 errors) AND discovered+run by `uloop run-tests` (3/3 and 2/2 passing) with **NO restart**. New non-test server `.cs` (component, template, protocol, state DTO) also compiled without restart, and Unity auto-generated their `.meta`. So the "immutable file: package cache" behavior above did NOT occur — likely fixed by a newer uloop/Unity version. Going forward: try `compile`/`run-tests` first; only restart if a new type is genuinely absent (confirm via the `grep ScriptAssemblies` check above) — don't restart preemptively.
