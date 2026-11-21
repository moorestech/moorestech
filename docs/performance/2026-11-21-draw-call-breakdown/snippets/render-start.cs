// URP の描画時間・カメラ描画・SetPass・ドローコール・影を落とす数・三角形・メインスレッドと描画スレッドの時間を ProfilerRecorder で溜め始める（Profiler の記録は使わない）
// 2026-11-20 のパイプの計測の render-start.cs に、影を落とす数・三角形・スレッドの時間を足したもの
using Unity.Profiling;
var d = System.AppDomain.CurrentDomain;
var names = new[] { "Inl_UniversalRenderTotal", "RenderPlayModeViewCameras", "SetPass Calls Count", "Draw Calls Count", "Batches Count", "Shadow Casters Count", "Triangles Count", "Main Thread", "Render Thread" };
var cats = new[] { ProfilerCategory.Render, ProfilerCategory.Render, ProfilerCategory.Render, ProfilerCategory.Render, ProfilerCategory.Render, ProfilerCategory.Render, ProfilerCategory.Render, ProfilerCategory.Internal, ProfilerCategory.Internal };
var recs = new ProfilerRecorder[names.Length];
for (var i = 0; i < names.Length; i++) recs[i] = ProfilerRecorder.StartNew(cats[i], names[i], 2000);
d.SetData("bench.render.names", names);
d.SetData("bench.render.recs", recs);
return "render recording";
