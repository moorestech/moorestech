// URP の描画時間・カメラ描画・SetPass・ドローコールのフレームごとの値を ProfilerRecorder で溜め始める（Profiler の記録は使わない）
using Unity.Profiling;
var d = System.AppDomain.CurrentDomain;
var names = new[] { "Inl_UniversalRenderTotal", "RenderPlayModeViewCameras", "SetPass Calls Count", "Draw Calls Count", "Batches Count" };
var cats = new[] { ProfilerCategory.Render, ProfilerCategory.Render, ProfilerCategory.Render, ProfilerCategory.Render, ProfilerCategory.Render };
var recs = new ProfilerRecorder[names.Length];
for (var i = 0; i < names.Length; i++) recs[i] = ProfilerRecorder.StartNew(cats[i], names[i], 2000);
d.SetData("bench.render.names", names);
d.SetData("bench.render.recs", recs);
return "render recording";
