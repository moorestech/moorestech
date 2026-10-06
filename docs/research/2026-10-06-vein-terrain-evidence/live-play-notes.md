# 2026-10-06 default seed actual PlayMode investigation

User: 「デフォルトseed実プレイで海面より低い地形が生成されてる」。

Fresh isolated generated world, seed196, master 47f79caa83c55b1c945dc3d86b1a91c5a0e5c3b3, generator4.0.0, nine2049² tiles. Initial boot uses PlaytestBoot (PureNature); follow-up switches to Runtime via DebugEnvironmentController, matching generated-play menu environment. No fake flat ground created. Existing saves were read only; all nine existing world_generated r16 hashes match this newly generated world.

Live Terrain data contains low ground, e.g. (892.5781,2.74675,0), dominant Gravel1 texture. This alone is not evidence of an inland defect: ocean/coast must remain. Screenshot at this point shows gravel slope/basin without visible water.

Full heightmap-vertex scan of both live BK/Water renderer bounds finds minimum5.859733 and6.262589, above plane4.3. Material waveHeight0.5. This does not reproduce the reported water protrusion. Classification regeneration across the whole world also finds0 landMask>0.5 samples below4.3. Do not interpret this as disproving the user's report. Exact affected location/build remains unresolved; one location question is pending.

Evidence files: live-state.txt, live-plane-min.txt, live-low-ground.txt (whole-world scan at4-pixel stride; field lowNonzeroSamples includes zero), sea-all-unbounded-measurement.txt. The latter script output retains its earlier EditMode label, but was invoked while PlayMode was running; it calls the generator independently, not the displayed Terrain.

Recording root (gitignored): moorestech_client/PlaytestResults/20261006_155818/. sea-default-live failed at required opening-skit skip (no skit accepted), so it is not a passing playtest. sea-default-live-inspect then gathered live state and spawn screenshot successfully; sea-low-ground-view recorded the low point in Runtime environment. Both screenshots were visually inspected. This is diagnostic evidence, not fix validation.
