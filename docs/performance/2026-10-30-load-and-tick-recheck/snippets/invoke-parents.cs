// 定常時の Profiler 記録から、名前の無い UpdateFunction.Invoke を親マーカー別に集計し、UniTask のループ登録内容も列挙する
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditorInternal;
using UnityEditor.Profiling;
ProfilerDriver.enabled = false;
const string InvokeName = "UnityEngine.CoreModule.dll!::UpdateFunction.Invoke() [Invoke]";
var selfByParent = new Dictionary<string, double>(); var callsByParent = new Dictionary<string, double>();
var childByParent = new Dictionary<string, Dictionary<string, double>>();
int n = 0;
for (int f = ProfilerDriver.firstFrameIndex + 1; f < ProfilerDriver.lastFrameIndex; f++) {
  using (var v = ProfilerDriver.GetHierarchyFrameDataView(f, 0, HierarchyFrameDataView.ViewModes.Default, HierarchyFrameDataView.columnTotalTime, false)) {
    if (!v.valid) continue; n++;
    var st = new Stack<(int id, string parent)>(); st.Push((v.GetRootItemID(), "")); var ch = new List<int>();
    while (st.Count > 0) {
      var (id, parent) = st.Pop(); var name = v.GetItemName(id);
      if (name == InvokeName) {
        selfByParent[parent] = (selfByParent.TryGetValue(parent, out var a) ? a : 0) + v.GetItemColumnDataAsFloat(id, HierarchyFrameDataView.columnSelfTime);
        callsByParent[parent] = (callsByParent.TryGetValue(parent, out var b) ? b : 0) + v.GetItemColumnDataAsFloat(id, HierarchyFrameDataView.columnCalls);
        v.GetItemChildren(id, ch);
        if (!childByParent.TryGetValue(parent, out var cd)) { cd = new Dictionary<string, double>(); childByParent[parent] = cd; }
        foreach (var c in ch) { var cn = v.GetItemName(c); cd[cn] = (cd.TryGetValue(cn, out var x) ? x : 0) + v.GetItemColumnDataAsFloat(c, HierarchyFrameDataView.columnTotalTime); }
      }
      v.GetItemChildren(id, ch);
      foreach (var c in ch.ToList()) st.Push((c, name == InvokeName ? parent : name));
    }
  }
}
ProfilerDriver.ClearAllFrames();
var sb = new StringBuilder();
sb.AppendLine("frames=" + n);
sb.AppendLine("== UpdateFunction.Invoke self ms/frame by parent marker");
foreach (var kv in selfByParent.OrderByDescending(k => k.Value)) {
  sb.AppendLine((kv.Value / n).ToString("F2").PadLeft(7) + " self  calls/f=" + (callsByParent[kv.Key] / n).ToString("F1") + "  parent=" + kv.Key);
  foreach (var c in childByParent[kv.Key].OrderByDescending(x => x.Value).Take(5)) sb.AppendLine("          child " + (c.Value / n).ToString("F2") + "ms  " + c.Key);
}
// UniTask のループに登録されている処理（PlayerLoopRunner の loopItems と ContinuationQueue）を型別に数える
var helper = typeof(Cysharp.Threading.Tasks.PlayerLoopHelper);
var bf = BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public;
foreach (var fname in new[] { "runners", "yielders" }) {
  var arr = helper.GetField(fname, bf)?.GetValue(null) as Array; if (arr == null) continue;
  for (int i = 0; i < arr.Length; i++) {
    var q = arr.GetValue(i); if (q == null) continue;
    var counts = new Dictionary<string, int>();
    foreach (var lf in new[] { "loopItems", "actionList", "waitingList" }) {
      var fld = q.GetType().GetField(lf, bf); if (fld == null) continue;
      if (!(fld.GetValue(q) is System.Collections.IEnumerable items)) continue;
      foreach (var it in items) {
        if (it == null) continue;
        string key;
        if (it is Delegate d) { var t = d.Target; key = t == null ? d.Method.DeclaringType + "." + d.Method.Name : (t.GetType().IsGenericType ? t.GetType().GetGenericArguments()[0].FullName : t.GetType().FullName); }
        else { var t = it.GetType(); key = t.IsGenericType ? t.Name + "<" + string.Join(",", t.GetGenericArguments().Select(g => g.Name)) + ">" : t.FullName; }
        counts[key] = (counts.TryGetValue(key, out var c) ? c : 0) + 1;
      }
    }
    if (counts.Count == 0) continue;
    sb.AppendLine("== UniTask " + fname + "[" + i + "] (" + (Cysharp.Threading.Tasks.PlayerLoopTiming)(i) + ")");
    foreach (var kv in counts.OrderByDescending(k => k.Value).Take(15)) sb.AppendLine(kv.Value.ToString().PadLeft(6) + "  " + kv.Key);
  }
}
System.IO.File.WriteAllText(@"C:\moorestech-perf\invoke-parents.txt", sb.ToString());
return "ok frames=" + n;
