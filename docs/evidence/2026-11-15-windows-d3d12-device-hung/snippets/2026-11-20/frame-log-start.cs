// フレームごとに CPU/GPU のフレーム時間を C:\moorestech-perf\frames.csv へ書く（落ちても残るよう毎回 Flush）
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
var w = new StreamWriter(@"C:\moorestech-perf\frames.csv", false);
w.WriteLine("t,frame,cpuMs,gpuMs,cpuMainMs,cpuRenderMs,dt");
w.Flush();
var last = -1;
var timings = new FrameTiming[1];
var t0 = Time.realtimeSinceStartup;
System.Action<ScriptableRenderContext, System.Collections.Generic.List<Camera>> h = null;
h = (ctx, cams) =>
{
    if (Time.frameCount == last) return;
    last = Time.frameCount;
    FrameTimingManager.CaptureFrameTimings();
    var n = FrameTimingManager.GetLatestTimings(1, timings);
    var f = n > 0 ? timings[0] : default;
    w.WriteLine((Time.realtimeSinceStartup - t0).ToString("F3") + "," + Time.frameCount + "," + f.cpuFrameTime.ToString("F2") + "," + f.gpuFrameTime.ToString("F2") + "," + f.cpuMainThreadFrameTime.ToString("F2") + "," + f.cpuRenderThreadFrameTime.ToString("F2") + "," + (Time.unscaledDeltaTime * 1000f).ToString("F1"));
    w.Flush();
};
RenderPipelineManager.endContextRendering += h;
return "frame log started";
