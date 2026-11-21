// gear-profile.cs で始めた記録を止め、名前に Gear・Tick・VContainer・Update を含むマーカーと PlayerLoop を1フレーム平均で返す
using UnityEditorInternal;
using UnityEditor.Profiling;
using System.Collections.Generic;
using System.Linq;
ProfilerDriver.enabled = false;
var total = new Dictionary<string, double>(); var self = new Dictionary<string, double>(); var calls = new Dictionary<string, double>();
int first = ProfilerDriver.firstFrameIndex + 1, last = ProfilerDriver.lastFrameIndex - 1, n = 0;
for (int f = first; f <= last; f++)
using (var v = ProfilerDriver.GetHierarchyFrameDataView(f, 0, HierarchyFrameDataView.ViewModes.Default, HierarchyFrameDataView.columnTotalTime, false))
{
    if (!v.valid) continue;
    n++;
    var st = new Stack<int>(); var ch = new List<int>(); st.Push(v.GetRootItemID());
    while (st.Count > 0)
    {
        var id = st.Pop(); v.GetItemChildren(id, ch);
        foreach (var c in ch)
        {
            var nm = v.GetItemName(c);
            if (nm == "PlayerLoop" || nm.Contains("Gear") || nm.Contains("Tick") || nm.Contains("VContainer") || nm.Contains("Update"))
            {
                total[nm] = (total.TryGetValue(nm, out var a) ? a : 0) + v.GetItemColumnDataAsFloat(c, HierarchyFrameDataView.columnTotalTime);
                self[nm] = (self.TryGetValue(nm, out var b) ? b : 0) + v.GetItemColumnDataAsFloat(c, HierarchyFrameDataView.columnSelfTime);
                calls[nm] = (calls.TryGetValue(nm, out var d) ? d : 0) + v.GetItemColumnDataAsFloat(c, HierarchyFrameDataView.columnCalls);
            }
            st.Push(c);
        }
    }
}
ProfilerDriver.ClearAllFrames();
return "frames=" + n + "\n" + string.Join("\n", total.Where(kv => kv.Value / n >= 0.05).OrderByDescending(kv => kv.Value).Take(40)
    .Select(kv => (kv.Value / n).ToString("F2") + " self=" + (self[kv.Key] / n).ToString("F2") + " calls=" + (calls[kv.Key] / n).ToString("F1") + "  " + kv.Key));
