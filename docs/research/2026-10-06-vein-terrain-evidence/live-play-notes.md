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
