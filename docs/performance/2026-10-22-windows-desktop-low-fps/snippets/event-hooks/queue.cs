using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
var helper = typeof(Cysharp.Threading.Tasks.PlayerLoopHelper);
var bf = BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public;
var sb = new System.Text.StringBuilder();
foreach (var fname in new[] { "yielders", "runners" }) {
  var arr = helper.GetField(fname, bf)?.GetValue(null) as Array;
  if (arr == null) { sb.AppendLine(fname + " null"); continue; }
  for (int i = 0; i < arr.Length; i++) {
    var q = arr.GetValue(i); if (q == null) continue;
    var counts = new Dictionary<string, int>();
    foreach (var lf in new[] { "actionList", "waitingList", "loopItems", "waitQueue" }) {
      var f = q.GetType().GetField(lf, bf); if (f == null) continue;
      var v = f.GetValue(q);
      System.Collections.IEnumerable items = v as System.Collections.IEnumerable;
      if (items == null) continue;
      foreach (var it in items) {
        if (it == null) continue;
        string key;
        if (it is Delegate d) {
          var t = d.Target; 
          key = t == null ? d.Method.Name : (t.GetType().IsGenericType ? t.GetType().GetGenericArguments()[0].FullName : t.GetType().FullName) + "." + d.Method.Name;
        } else key = it.GetType().FullName;
        counts[lf + ":" + key] = (counts.TryGetValue(lf + ":" + key, out var c) ? c : 0) + 1;
      }
    }
    if (counts.Count == 0) continue;
    sb.AppendLine("== " + fname + "[" + i + "] " + q.GetType().Name);
    foreach (var kv in counts.OrderByDescending(k => k.Value).Take(25)) sb.AppendLine(kv.Value.ToString().PadLeft(7) + " " + kv.Key);
  }
}
return sb.ToString();
