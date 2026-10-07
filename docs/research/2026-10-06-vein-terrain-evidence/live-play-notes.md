# 2026-10-06 default seed actual PlayMode investigation

User: 「デフォルトseed実プレイで海面より低い地形が生成されてる」。

Fresh isolated generated world, seed196, master 47f79caa83c55b1c945dc3d86b1a91c5a0e5c3b3, generator4.0.0, nine2049² tiles. Initial boot uses PlaytestBoot Runtime, confirmed from ConfigureFixedWorldDebugSettings. The skill reference incorrectly describes PureNature; the previous note copied that stale description. Follow-up also explicitly selects Runtime. No fake flat ground created. Existing saves were read only; all nine existing world_generated r16 hashes match this newly generated world.

Live Terrain data contains low ground, e.g. (892.5781,2.74675,0), dominant Gravel1 texture. This alone is not evidence of an inland defect: ocean/coast must remain. Screenshot at this point shows gravel slope/basin without visible water.

Full heightmap-vertex scan of both live BK/Water renderer bounds finds minimum5.859733 and6.262589, above plane4.3. Material waveHeight0.5. This does not reproduce the reported water protrusion. Classification regeneration across the whole world also finds0 landMask>0.5 samples below4.3. Do not interpret this as disproving the user's report. Exact affected location/build remains unresolved. User requested autonomous investigation; the location question is withdrawn.

Evidence files: live-state.txt, live-plane-min.txt, live-low-ground.txt (whole-world scan at4-pixel stride; field lowNonzeroSamples includes zero), sea-all-unbounded-measurement.txt. The latter script output retains its earlier EditMode label, but was invoked while PlayMode was running; it calls the generator independently, not the displayed Terrain.

Recording root (gitignored): moorestech_client/PlaytestResults/20261006_155818/. sea-default-live failed at required opening-skit skip (no skit accepted), so it is not a passing playtest. sea-default-live-inspect then gathered live state and spawn screenshot successfully; sea-low-ground-view recorded the low point in Runtime environment. Both screenshots were visually inspected. This is diagnostic evidence, not fix validation.

## Extended autonomous investigation

- Existing world_generated copied using clone-on-write into isolated test world; source untouched. Actual PlayMode loads at saved player (465.61,11.90,505.68). All broad thin renderers, including inactive objects: the two Water renderers already measured, plus inactive Debug Plane atY0. No third active ocean renderer was found.
- Locally available distribution artifact exhibition-20261001, build commit4ac0c2d96871bb91dab998baae317c743f5149f4 (dirty build), snapshot c567087d7c7419eb: all nine r16 match current fresh world byte-for-byte. All nine visual v11 height payloads also match those r16. This excludes height differences for this available artifact, not all user builds.
- Runtime aerial camera, fixed time and camera, render twice with the two BK/Water renderers enabled/disabled. 99,824 changed pixels overall; changes above10 RGB levels lie outside the generated terrain footprint only. Within pixels[64,1984)² (world[-1000,2000]²), changed pixels above10=0. This is a diagnostic aerial camera, not a player-camera screenshot, and is not a universal rendering proof.
- Player-camera recordings at both minimum-height overlap points were also visually inspected: no protruding ocean surface observed. Artifacts: PlaytestResults/20261006_161001/lowest-water-overlap-points. A small blue patch seen nearby was ray-traced to OutcropMesh using Blue/URP-Lit at(-360.5,6.5,499.5), not the ocean. This identifies only our observed patch, not the user's report.
- Live OutcropGameObject scan:1415 total,39 anchor positions below Terrain.SampleHeight;120 fluid outcrops,4 buried fluid anchors. Maximum depth1.500769 at(438.5,17.5,-401.5), terrain19.00077. Metric is anchor position, not the entire mesh. Raw-generation and runtime counts have different populations/interpolation paths; do not treat them as identical tests.
- MacBook and Windows verification hosts were checked via configured SSH aliases; both timed out. No remote processes/editor were changed. No location request or permission exception is pending.

Incident status: reported ocean protrusion remains unconfirmed in the current accessible environment. The actual generation seaLevel0 and fixed visible plane4.3 are independent definitions; that structural mismatch is confirmed but must not be presented as the measured cause of the user's specific protrusion. Product code remains unchanged. Design remains proposed rather than falsely marking the sea investigation complete.

## 2026-10-07 post-implementation recorded playtests (revision 5)

Recorded with the unity-playmode-recorded-playtest DSL in this worktree (recordings are gitignored under moorestech_client/PlaytestResults/; scenarios are committed under .agents/skills/unity-playmode-recorded-playtest/scenarios/misc/vein-terrain-*.cs). Each seed196 world was freshly generated in the run (world.json createdAt checked, shared world cache moved aside, never deleted).

- tour (new seed196 world, generatorVersion 5.0.0, 9 tiles): terrain under the player at the previous worst outcrop (438.5,-401.5) = 27.99854 m, at the previous sea-plane low point (-336.43,405.76) = 5.85989 m, at a fluid vein 16.20670 m, at a coastline point 4.99087 m, on the tile seam x=0 10.60245 m; all >= 4.9 m (SeaY 4.3 + wave 0.5 + clearance 0.1). Outcrop mesh bottom minus terrain ≈ 0.001 m; seam height delta 0.000000.
- gameplay-save: hand mining raised iron ore 0 -> 2; a primitive miner and a gear pump were placed on vein pads; vein range boxes bottom minus terrain min 0.0018 m / 0.0068 m; save.json updated.
- reload-verify: both blocks at the same GUID/origin, 13 terrain probes identical, all nine r16 SHA256 identical after reload.
- legacy-world-load: a clone-on-write copy of the existing world_generated (4.0.0) loads with generatorVersion 4.0.0 and all nine r16 byte-identical to the committed v4 golden; the original's hashes are unchanged before/after.
- Defect found and fixed by these recordings: blocks placed on revision-5 vein pads sank one cell because the pad surface is stored just under the integer by 16-bit TerrainData quantization; client placement now tolerates one quantization step plus the pad clearance (commits ff7b46af5 and follow-up). After the fix: miner origin Y 10 = range bottom 10, pump 17 = 17.

Environment limits (not product verification gaps of this branch, but stated so nothing is overclaimed): the CEF Web UI server crashes on this host (Beads moorestech-wsnf / moorestech-1gc8, also on master), leaving a white full-screen RawImage. Scenarios hide that overlay and use non-UI equivalents for the build menu (hotbar assignment from the same catalog) and the pause-menu save (the same GameSaveRequester.Save()). The real Web UI routes were not exercised. The shader's actual wave peak was not measured live (only the material waveHeight 0.5 that the envelope assumes).

Incident status is unchanged: the user's reported sea-surface protrusion is still not reproduced. The synthetic below-sea fixtures and these recordings show the new guarantee holds on new worlds; they are not a reproduction of the report.
