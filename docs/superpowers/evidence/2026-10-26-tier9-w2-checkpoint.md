# Tier9 W2 checkpoint — 2026-10-26 JST

## Acceptance state

- Session A: Tasks12–18 and Task20 independently approved. Task19 remains **Spec❌ / Needs fixes** for the block-ordering policy below. Tasks21–24, J2, final pinned gates, recordings, evaluations and PRs are not complete.
- Master branch `feat/tier9-integrated`: pushed `5d6ca05cf0e191dd56d126ca865770072740078d`. This is still an integration worktree, not the accepted production pin.
- Code before this documentation checkpoint: `f90a68e075694a7eb1b25adf86806d224d9c4fa3`. Master pin is still `630fdce08eccee47feaa15721ca607f407000ab9`.
- B handoff received at `50762d535d1f57723df7b277f5e0d96d470ec19e`. Normal merge and A-owned Editor compile/G4/G5 at J2 have **not** happened.

## Accepted slice evidence

| Task | Master HEAD | Independent outcome / evidence |
|---|---|---|
|12|`7e3c88789d50d36148e6c3a591d011df2bd41446`|Spec/apply and GUID tools; module combination and GUID-summed research-cost false-PASS regressions fixed; 98 selftests.|
|13|`7dcfdc6137a9c7ec69faf4f399a4e27ec62853a8`|Upstream/IPA/steel-frame recipes and ports; 14 GUIDs, repeat0 and byte-identical; G1 PASS.|
|14|`1f5d9c406904fb65e6529ed2c360ebf5085ed33b`|Wafer/die/chip variants and six families; 29 GUIDs, repeat0 and byte-identical; G1 PASS.|
|15|`1ad4f6e4b22f4f52392f4e12ddc442087f199288`|Dedicated machines/cleanroom/process recipes, obsolete items removed; 24 GUIDs and 217 values checked; G1 PASS.|
|16|`ab58014efc2c723bfc84cf7a77d32d56583109b2`|20 modules and five GPU levels; 36 GUIDs, repeat0; module comparisons and additional118 field assertions; G1 PASS.|
|17|`c496d77848b343bb8162984bca0f88cece00deef`|S1 research sync and nodeGraph PASS; specified S1 products/research reachable; S1 non-time mismatch0. Sand view required by sync makes39, not the estimated38. IPA recipe follows existing ADR0033/MOVE into the petroleum research.|
|18|`683cc9a6a05eb951abf28d9ccff95d2b6f8127c3`|Compute data, satellite payloads, launch pad, space submenu, seven recipes; five GUIDs, repeat0/23 files byte-identical; G1 PASS and six negative cases rejected. Block ordering explicitly unfinished.|
|20|`5d6ca05cf0e191dd56d126ca865770072740078d`|Fixed fusion/antimatter content and measured dimensions; GUID issuance0 and repeat byte-identical. G1 positive/three negative/restored-positive repeated in the isolated QA Editor; independent re-review Approved for data and Session A's corrected operation. Historical incident remains open.|

Execution evidence/reviews are archived under `moorestech_logs/harness/sdd/feat-tier9-integrated/workspace/`, especially `task-N-report.md`, `task-N-review.md`, and the re-review/fix/isolation reports. `.superpowers/sdd` is not committed to the code repository.

## Task19: one remaining design blocker

S2 research/G6 implementation is `8648561c2ce333fba8c970e79fc6a45884b8bf03`; fixes are `2d5893e7c3de6a50ad29fe84979368ac23dc56a5`.

- I2 closed: `prerequisites_before` uses research GUID identity; Produces, sample/caller and direct duplicate-name/self-exclusion contract tests now agree. This controller default is confined to Task19; Task22 does not call this helper.
- I3 closed: factory demand is tested before rounding. The independently reproduced 4.002962…-machine false PASS now FAILs; exactly4 still PASSes. Threshold4 was not relaxed.
- **I1 open:** at the Task19 observation point, all 82 blocks lacked the `itemGuid` needed by master-refine's legacy item-linked rule. Apply preserves block `sortPriority` for refinement, so no owner actually recalculates the launch pad/base/fusion placeholders' zero values. Reapplying zero is not evidence of a calculated final order.
- Human choice required: **A** use explicit spec block priorities, **B** derive block order independently from `unlockBlock` and nodeGraph (controller recommendation), or **C** retain the visible zero-priority ordering deficit. None is adjudicated.
- Task21 is explicitly atomic with Task22's spec10, so it cannot be independently completed while this policy remains unresolved.

Current automatic evidence is intentionally not all green: 118 selftests passed; G2 has27 failures/19 unchanged allowlisted findings pending S3 research; G6 has9 failures (S1 time target plus missing S3 consumers/section and propagated totals). Task23 balance targets and Task24 final-pin acceptance remain mandatory. The21 manual Surface failures retained from W1 remain unmet, not silently counted as passing.

## Shared-HEAD incident and isolation

Tracked separately as **`moorestech-xyuaf.2.4`**.

The first Task20 Editor startup actually changed the shared master clone from `0d129c8` to `630fdce` through `SyncToRecordedCommitsAtStartup`; Editor.log and the shared reflog prove it. **The shared HEAD has not been restored.** No claim is made that this incident or other sessions' startup routes are repaired.

Session A's future Editor launches are limited to `/Users/sakastudio/hermes-agent/data/worktrees/tier9-integrated-qa/moorestech/moorestech_client`, whose code, sibling master and private assets are independent detached worktrees. A positive isolation test started the dedicated master at `0d129c8`, observed startup moving only that dedicated master to the recorded `630fdce`, and observed unchanged shared reflog/HEAD, original private/main repositories, BODY, MASTER branch and saves. No symlinks escaped the QA root.

In this Editor, compile was0 errors/0 warnings; G1 explicitly loaded integration MASTER's55-research/deuterium-bearing data rather than the QA pin's44-research data, passed positive/negative/restoration checks, and had internal error counters0 for the positive intervals. The owner receipt and own-Editor shutdown were verified. The old BODY Editor must not be launched again under the shared-parent layout.

QA source contains two saved SchemaWatcher `_CompileRequester.cs` marker changes, not feature implementation; they are listed in the isolation report and are not committed. The old pre-clear Error1 remains unidentified: reproducing an `OnTerrainChanged` entry with inconsistent uloop log classification does not identify the historical entry. Existing ResourceWarning noise also remains recorded.

## Resume boundary

Obtain the block-ordering ruling, implement/review it, and close Task19 before the dependent combined Tasks21/22. Continue Task23/24, then normal-merge B and rerun A's compile/G4/G5 in the dedicated QA Editor. Preserve the no-distribution-build/no-save-writeback ruling: the user opens the existing MacBook Pro world directly in Editor after final acceptance.
