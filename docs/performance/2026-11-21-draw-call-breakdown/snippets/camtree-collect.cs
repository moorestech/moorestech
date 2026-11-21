// benchmark/snippets/profiler-start.cs で始めた Profiler の記録を止め、カメラ描画の中身を返す
// main: メインスレッドの RenderPlayModeViewCameras 配下を「親 > 子」の経路ごとに total/self/calls（1フレーム平均、深さ8まで、total 0.05ms 以上）
// threads: メインスレッド以外のスレッドを名前でまとめ（Job.Worker 0..N は1つに）、マーカーごとの self 合計の上位（1フレーム平均）
using UnityEditorInternal;
using UnityEditor.Profiling;
using System.Collections.Generic;
using System.Linq;
using System.Text;
ProfilerDriver.enabled = false;
var ci = System.Globalization.CultureInfo.InvariantCulture;
var total = new Dictionary<string, double>(); var self = new Dictionary<string, double>(); var calls = new Dictionary<string, double>();
var threadSelf = new Dictionary<string, Dictionary<string, double>>();
int first = ProfilerDriver.firstFrameIndex + 1, last = ProfilerDriver.lastFrameIndex - 1, n = 0;
for (int f = first; f <= last; f++)
{
    using (var v = ProfilerDriver.GetHierarchyFrameDataView(f, 0, HierarchyFrameDataView.ViewModes.Default, HierarchyFrameDataView.columnTotalTime, false))
    {
        if (!v.valid) continue;
        n++;
        var ch = new List<int>();
        var st = new Stack<(int id, string path, int depth, bool inCam)>();
        st.Push((v.GetRootItemID(), "", 0, false));
        while (st.Count > 0)
        {
            var (id, path, depth, inCam) = st.Pop();
            v.GetItemChildren(id, ch);
            foreach (var c in ch.ToArray())
            {
                var nm = v.GetItemName(c);
                var cam = inCam || nm == "RenderPlayModeViewCameras";
                var p = cam ? (path.Length == 0 ? nm : path + " > " + nm) : "";
                if (cam)
                {
                    total[p] = (total.TryGetValue(p, out var a) ? a : 0) + v.GetItemColumnDataAsFloat(c, HierarchyFrameDataView.columnTotalTime);
                    self[p] = (self.TryGetValue(p, out var b) ? b : 0) + v.GetItemColumnDataAsFloat(c, HierarchyFrameDataView.columnSelfTime);
                    calls[p] = (calls.TryGetValue(p, out var d) ? d : 0) + v.GetItemColumnDataAsFloat(c, HierarchyFrameDataView.columnCalls);
                }
                if (!cam || depth < 8) st.Push((c, p, cam ? depth + 1 : 0, cam));
            }
        }
    }
    // メインスレッド以外：スレッドを順に開き、マーカーごとの self を足す
    for (int t = 1; t < 256; t++)
    {
        using (var v = ProfilerDriver.GetHierarchyFrameDataView(f, t, HierarchyFrameDataView.ViewModes.Default, HierarchyFrameDataView.columnSelfTime, false))
        {
            if (!v.valid) break;
            var tn = v.threadName ?? ("thread" + t);
            if (tn.StartsWith("Job.Worker")) tn = "Job.Worker*";
            if (!threadSelf.TryGetValue(tn, out var map)) threadSelf[tn] = map = new Dictionary<string, double>();
            var ch = new List<int>(); var st = new Stack<int>(); st.Push(v.GetRootItemID());
            while (st.Count > 0)
            {
                var id = st.Pop();
                v.GetItemChildren(id, ch);
                foreach (var c in ch)
                {
                    var nm = v.GetItemName(c);
                    map[nm] = (map.TryGetValue(nm, out var a) ? a : 0) + v.GetItemColumnDataAsFloat(c, HierarchyFrameDataView.columnSelfTime);
                    st.Push(c);
                }
            }
        }
    }
}
ProfilerDriver.ClearAllFrames();
if (n == 0) return "{\"error\":\"no profiler frames\"}";
string J(string s) => "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
var sb = new StringBuilder();
sb.Append("{\"frames\":" + n + ",\"main\":[");
sb.Append(string.Join(",", total.Where(kv => kv.Value / n >= 0.05).OrderBy(kv => kv.Key, System.StringComparer.Ordinal).Select(kv =>
    "{\"path\":" + J(kv.Key) + ",\"totalMs\":" + (kv.Value / n).ToString("F3", ci) + ",\"selfMs\":" + (self[kv.Key] / n).ToString("F3", ci) + ",\"calls\":" + (calls[kv.Key] / n).ToString("F1", ci) + "}")));
sb.Append("],\"threads\":{");
sb.Append(string.Join(",", threadSelf.Where(kv => kv.Value.Values.Sum() / n >= 0.2).OrderByDescending(kv => kv.Value.Values.Sum()).Select(kv =>
    J(kv.Key) + ":{\"selfSumMs\":" + (kv.Value.Values.Sum() / n).ToString("F3", ci) + ",\"top\":{" +
    string.Join(",", kv.Value.OrderByDescending(m => m.Value).Take(25).Select(m => J(m.Key) + ":" + (m.Value / n).ToString("F3", ci))) + "}}")));
sb.Append("}}");
return sb.ToString();
