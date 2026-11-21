// 一時的な計測用の印（GearDiag.*）と主要マーカーを ProfilerRecorder で300フレーム取り、1フレーム平均の ms と回数を返す。あわせて fps も返す
using System.Collections.Generic;
using System.Linq;
using Unity.Profiling;
var names = new[] { ("GearDiag.AdvanceAll", ProfilerCategory.Scripts), ("GearDiag.MovingProcessor", ProfilerCategory.Scripts), ("GearDiag.TransformRotate", ProfilerCategory.Scripts),
    ("VContainerUpdate", ProfilerCategory.Scripts), ("PlayerLoop", ProfilerCategory.Internal), ("Main Thread", ProfilerCategory.Internal) };
var recs = names.Select(n => ProfilerRecorder.StartNew(n.Item2, n.Item1, 400, ProfilerRecorderOptions.SumAllSamplesInFrame | ProfilerRecorderOptions.CollectOnlyOnCurrentThread)).ToArray();
var f0 = Time.frameCount; var t0 = Time.realtimeSinceStartup;
while (Time.frameCount - f0 < 300) await System.Threading.Tasks.Task.Delay(50);
var fps = (Time.frameCount - f0) / (Time.realtimeSinceStartup - t0);
var ci = System.Globalization.CultureInfo.InvariantCulture;
var parts = new List<string>();
for (var i = 0; i < names.Length; i++)
{
    var samples = new List<ProfilerRecorderSample>(recs[i].Capacity); recs[i].CopyTo(samples);
    var ms = samples.Count == 0 ? 0 : samples.Average(s => (double)s.Value) / 1e6;
    var calls = samples.Count == 0 ? 0 : samples.Average(s => (double)s.Count);
    parts.Add(names[i].Item1 + "=" + ms.ToString("F3", ci) + "ms/" + calls.ToString("F0", ci) + "回(" + samples.Count + "f)");
    recs[i].Dispose();
}
return "opt=" + UnityEditor.Compilation.CompilationPipeline.codeOptimization + " fps=" + fps.ToString("F2", ci) + " | " + string.Join(" ", parts);
