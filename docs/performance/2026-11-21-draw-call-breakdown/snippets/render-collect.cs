// render-start.cs で溜めた値をフレーム平均にして JSON で返し、記録を止める。時間は ns を ms に直す
using System.Collections.Generic;
using System.Linq;
using Unity.Profiling;
var d = System.AppDomain.CurrentDomain;
var names = (string[])d.GetData("bench.render.names");
var recs = (ProfilerRecorder[])d.GetData("bench.render.recs");
var ci = System.Globalization.CultureInfo.InvariantCulture;
var parts = new List<string>();
for (var i = 0; i < names.Length; i++)
{
    var r = recs[i];
    var samples = new List<ProfilerRecorderSample>(r.Capacity); r.CopyTo(samples);
    var values = samples.Select(s => (double)s.Value).ToList();
    var isTime = r.UnitType == ProfilerMarkerDataUnit.TimeNanoseconds;
    var avg = values.Count == 0 ? 0 : values.Average() / (isTime ? 1e6 : 1);
    parts.Add("\"" + names[i] + "\":{\"avg\":" + avg.ToString("F3", ci) + ",\"frames\":" + values.Count + ",\"unit\":\"" + (isTime ? "ms" : "count") + "\"}");
    r.Dispose();
}
d.SetData("bench.render.recs", null);
// FrameTimingManager の値（GPU 時間が 0 のフレームは、まだ GPU の計測が返っていないので捨てる）
var hook = (System.Action<UnityEngine.Rendering.ScriptableRenderContext, List<Camera>>)d.GetData("bench.render.hook");
if (hook != null) UnityEngine.Rendering.RenderPipelineManager.endContextRendering -= hook;
var timings = (List<FrameTiming>)d.GetData("bench.render.timings") ?? new List<FrameTiming>();
var gpu = timings.Where(t => t.gpuFrameTime > 0).ToList();
void Add(string name, IEnumerable<double> xs) { var l = xs.ToList(); parts.Add("\"" + name + "\":{\"avg\":" + (l.Count == 0 ? 0 : l.Average()).ToString("F3", ci) + ",\"frames\":" + l.Count + ",\"unit\":\"ms\"}"); }
Add("ft.cpuFrame", timings.Select(t => t.cpuFrameTime));
Add("ft.cpuMainThread", timings.Select(t => t.cpuMainThreadFrameTime));
Add("ft.cpuRenderThread", timings.Select(t => t.cpuRenderThreadFrameTime));
Add("ft.cpuMainThreadPresentWait", timings.Select(t => t.cpuMainThreadPresentWaitTime));
Add("ft.gpuFrame", gpu.Select(t => t.gpuFrameTime));
d.SetData("bench.render.hook", null);
d.SetData("bench.render.timings", null);
return "{" + string.Join(",", parts) + "}";
