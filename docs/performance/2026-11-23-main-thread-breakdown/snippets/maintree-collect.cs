// benchmark/snippets/profiler-start.cs で始めた Profiler の記録を止め、メインスレッドの階層全体を返す
// main: ルートから「親 > 子」の経路ごとに total/self/calls（1フレーム平均、深さ10まで、total 0.1ms 以上）。2026-11-21 の camtree-collect.cs をカメラ描画の外まで広げたもの
// selfTop: メインスレッドのマーカー名ごとの self 合計の上位40（1フレーム平均。経路をまたいで同じ名前を足す）
using UnityEditorInternal;
using UnityEditor.Profiling;
using System.Collections.Generic;
using System.Linq;
using System.Text;
ProfilerDriver.enabled = false;
var ci = System.Globalization.CultureInfo.InvariantCulture;
var total = new Dictionary<string, double>(); var self = new Dictionary<string, double>(); var calls = new Dictionary<string, double>();
var selfByName = new Dictionary<string, double>();
int first = ProfilerDriver.firstFrameIndex + 1, last = ProfilerDriver.lastFrameIndex - 1, n = 0;
for (int f = first; f <= last; f++)
{
    using (var v = ProfilerDriver.GetHierarchyFrameDataView(f, 0, HierarchyFrameDataView.ViewModes.Default, HierarchyFrameDataView.columnTotalTime, false))
    {
        if (!v.valid) continue;
        n++;
        var ch = new List<int>();
        var st = new Stack<(int id, string path, int depth)>();
        st.Push((v.GetRootItemID(), "", 0));
        while (st.Count > 0)
        {
            var (id, path, depth) = st.Pop();
            v.GetItemChildren(id, ch);
            foreach (var c in ch.ToArray())
            {
                var nm = v.GetItemName(c);
                var p = path.Length == 0 ? nm : path + " > " + nm;
                var s = v.GetItemColumnDataAsFloat(c, HierarchyFrameDataView.columnSelfTime);
                selfByName[nm] = (selfByName.TryGetValue(nm, out var e) ? e : 0) + s;
                if (depth < 10)
                {
                    total[p] = (total.TryGetValue(p, out var a) ? a : 0) + v.GetItemColumnDataAsFloat(c, HierarchyFrameDataView.columnTotalTime);
                    self[p] = (self.TryGetValue(p, out var b) ? b : 0) + s;
                    calls[p] = (calls.TryGetValue(p, out var d) ? d : 0) + v.GetItemColumnDataAsFloat(c, HierarchyFrameDataView.columnCalls);
                }
                st.Push((c, p, depth + 1));
            }
        }
    }
}
ProfilerDriver.ClearAllFrames();
if (n == 0) return "{\"error\":\"no profiler frames\"}";
string J(string s) => "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
var sb = new StringBuilder();
sb.Append("{\"frames\":" + n + ",\"main\":[");
sb.Append(string.Join(",", total.Where(kv => kv.Value / n >= 0.1).OrderBy(kv => kv.Key, System.StringComparer.Ordinal).Select(kv =>
    "{\"path\":" + J(kv.Key) + ",\"totalMs\":" + (kv.Value / n).ToString("F3", ci) + ",\"selfMs\":" + (self[kv.Key] / n).ToString("F3", ci) + ",\"calls\":" + (calls[kv.Key] / n).ToString("F1", ci) + "}")));
sb.Append("],\"selfTop\":{");
sb.Append(string.Join(",", selfByName.OrderByDescending(kv => kv.Value).Take(40).Select(kv => J(kv.Key) + ":" + (kv.Value / n).ToString("F3", ci))));
sb.Append("}}");
return sb.ToString();
