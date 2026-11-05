// Profiler記録を止め、メインスレッドのフレーム時間分布と主要マーカーの total/self/calls をJSONで返す
using UnityEditorInternal;
using UnityEditor.Profiling;
using System.Collections.Generic;
using System.Linq;
using System.Text;
ProfilerDriver.enabled = false;
var ci = System.Globalization.CultureInfo.InvariantCulture;
var self = new Dictionary<string, double>(); var total = new Dictionary<string, double>(); var calls = new Dictionary<string, double>();
var frameMs = new List<double>();
// 先頭と末尾のフレームは記録の開始・停止をまたぐので捨てる
int first = ProfilerDriver.firstFrameIndex + 1, last = ProfilerDriver.lastFrameIndex - 1;
for (int f = first; f <= last; f++) {
  using (var v = ProfilerDriver.GetHierarchyFrameDataView(f, 0, HierarchyFrameDataView.ViewModes.Default, HierarchyFrameDataView.columnTotalTime, false)) {
    if (!v.valid) continue;
    frameMs.Add(v.frameTimeMs);
    var st = new Stack<int>(); int root = v.GetRootItemID(); st.Push(root); var ch = new List<int>();
    while (st.Count > 0) { int id = st.Pop();
      if (id != root) { var nm = v.GetItemName(id);
        self[nm] = (self.TryGetValue(nm, out var a) ? a : 0) + v.GetItemColumnDataAsFloat(id, HierarchyFrameDataView.columnSelfTime);
        total[nm] = (total.TryGetValue(nm, out var b) ? b : 0) + v.GetItemColumnDataAsFloat(id, HierarchyFrameDataView.columnTotalTime);
        calls[nm] = (calls.TryGetValue(nm, out var c) ? c : 0) + v.GetItemColumnDataAsFloat(id, HierarchyFrameDataView.columnCalls); }
      v.GetItemChildren(id, ch); foreach (var x in ch) st.Push(x); }
  }
}
int n = frameMs.Count;
ProfilerDriver.ClearAllFrames();
if (n == 0) return "{\"error\":\"no profiler frames\"}";
var sorted = frameMs.OrderBy(x => x).ToList();
System.Func<double, double> pct = p => sorted[System.Math.Min(n - 1, (int)System.Math.Floor(p * (n - 1)))];
var sb = new StringBuilder();
sb.Append("{\"frames\":" + n + ",\"frameMs\":{\"avg\":" + frameMs.Average().ToString("F3", ci) + ",\"p50\":" + pct(0.5).ToString("F3", ci) + ",\"p95\":" + pct(0.95).ToString("F3", ci) + ",\"max\":" + sorted[n - 1].ToString("F3", ci) + "},\"markers\":{");
// 比較対象：固定の主要マーカー＋計測用マーカー全部＋self上位
string[] fixedNames = { "PlayerLoop", "EditorLoop", "UniTaskLoopRunnerYieldUpdate", "RenderPlayModeViewCameras", "Update.ScriptRunBehaviourUpdate", "FixedUpdate.PhysicsFixedUpdate", "PostLateUpdate.UpdateAllRenderers", "PostLateUpdate.UpdateAllSkinnedMeshes", "GC.Alloc", "GC.Collect" };
var names = new HashSet<string>(fixedNames.Where(total.ContainsKey));
foreach (var k in total.Keys) if (k.StartsWith("PacketExchange.") || k.StartsWith("BlockState.")) names.Add(k);
foreach (var kv in self.OrderByDescending(k => k.Value).Take(20)) names.Add(kv.Key);
sb.Append(string.Join(",", names.OrderByDescending(k => total[k]).Select(k =>
  "\"" + k.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\":{\"totalMs\":" + (total[k] / n).ToString("F3", ci) + ",\"selfMs\":" + (self[k] / n).ToString("F3", ci) + ",\"calls\":" + (calls[k] / n).ToString("F1", ci) + "}")));
sb.Append("}}");
return sb.ToString();
