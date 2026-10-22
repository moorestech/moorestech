using UnityEditorInternal;
using UnityEditor.Profiling;
using System.Collections.Generic;
using System.Text;
var sb = new StringBuilder();
for (int f = ProfilerDriver.firstFrameIndex; f <= ProfilerDriver.lastFrameIndex; f++) {
  using (var v = ProfilerDriver.GetHierarchyFrameDataView(f, 0, HierarchyFrameDataView.ViewModes.Default, HierarchyFrameDataView.columnTotalTime, false)) {
    if (!v.valid) continue;
    var st = new Stack<int>(); st.Push(v.GetRootItemID()); var ch = new List<int>();
    while (st.Count > 0) { int id = st.Pop();
      if (v.GetItemName(id) == "PacketExchange.RouteResponse") {
        sb.Append("frame " + f + " frameMs=" + v.frameTimeMs.ToString("F0") + " resp ms=" + v.GetItemColumnDataAsFloat(id, HierarchyFrameDataView.columnTotalTime).ToString("F1") + " calls=" + v.GetItemColumnDataAsFloat(id, HierarchyFrameDataView.columnCalls) + " children:");
        v.GetItemChildren(id, ch); foreach (var c in ch) sb.Append(" " + v.GetItemName(c) + "=" + v.GetItemColumnDataAsFloat(c, HierarchyFrameDataView.columnTotalTime).ToString("F1"));
        sb.AppendLine(); continue; }
      v.GetItemChildren(id, ch); foreach (var x in ch) st.Push(x); }
  }
}
return sb.ToString();
