// buildmode-probe-start.cs が記録した遷移ごとの同期時間・前後のフレーム時間・Profiler の中身を返し、PlayerLoop から外す
// 対照は遷移の直前30フレームのフレーム時間の中央値。遷移のフレームから6フレームぶんの「中央値を超えた分」の合計を超過時間とする
// Return per-transition synchronous time, surrounding frame times and Profiler contents recorded by buildmode-probe-start.cs, and uninstall it
// The control is the median frame time of the 30 frames before the transition; excess is the sum over 6 frames from the transition frame above that median
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor.Profiling;
using UnityEditorInternal;
using UnityEngine.LowLevel;

var probeKey = "moorestech.buildmodeProbe";
var state = System.AppDomain.CurrentDomain.GetData(probeKey) as Dictionary<string, object>;
if (state == null) return "FAIL probe not installed";
if (!(bool)state["done"]) return "not-done frames=" + ((List<double>)state["frameStart"]).Count;

// PlayerLoop から外し、Profiler を止める
// Remove from the PlayerLoop and stop the Profiler
var loop = PlayerLoop.GetCurrentPlayerLoop();
var subs = loop.subSystemList;
var updateIndex = System.Array.FindIndex(subs, s => s.type == typeof(UnityEngine.PlayerLoop.Update));
subs[updateIndex].subSystemList = subs[updateIndex].subSystemList.Where(s => s.type != typeof(System.Diagnostics.Stopwatch)).ToArray();
loop.subSystemList = subs;
PlayerLoop.SetPlayerLoop(loop);
ProfilerDriver.enabled = false;
System.AppDomain.CurrentDomain.SetData(probeKey, null);

var frameStart = (List<double>)state["frameStart"];
var events = (List<string>)state["events"];
var sb = new StringBuilder();
sb.Append("error=" + state["error"] + "\n");
var durations = new List<double>();
for (var i = 0; i + 1 < frameStart.Count; i++) durations.Add((frameStart[i + 1] - frameStart[i]) * 1000.0);

// 遷移ごとの同期時間と前後のフレーム時間
// Synchronous time and surrounding frame times per transition
foreach (var e in events)
{
    var at = int.Parse(e.Substring(e.IndexOf('@') + 1, e.IndexOf('=') - e.IndexOf('@') - 1));
    var before = durations.Skip(at - 30).Take(30).OrderBy(d => d).ToList();
    var median = before.Count > 0 ? before[before.Count / 2] : 0;
    var after = durations.Skip(at).Take(6).ToList();
    var excess = after.Sum(d => d - median);
    sb.Append(e + " controlMedianMs=" + median.ToString("F1") + " frameMs[k..k+5]=" + string.Join(" ", after.Select(d => d.ToString("F0"))) + " excessMs=" + excess.ToString("F1") + "\n");
}

// Profiler：遷移のサンプルがあるフレームと、その後の数フレームの中身
// Profiler: the frame holding the transition sample and the next few frames
var probeFrames = new List<int>();
for (int f = ProfilerDriver.firstFrameIndex; f >= 0 && f <= ProfilerDriver.lastFrameIndex; f++)
{
    using (var v = ProfilerDriver.GetHierarchyFrameDataView(f, 0, HierarchyFrameDataView.ViewModes.MergeSamplesWithTheSameName, HierarchyFrameDataView.columnTotalTime, false))
    {
        if (!v.valid) continue;
        var id = FindItem(v, v.GetRootItemID(), "Probe.");
        if (id < 0) continue;
        probeFrames.Add(f);
        sb.Append("profiler frame=" + f + " " + v.GetItemName(id) + " sampleMs=" + v.GetItemColumnDataAsFloat(id, HierarchyFrameDataView.columnTotalTime).ToString("F1") + " frameMs=" + v.frameTimeMs.ToString("F1") + "\n");
        sb.Append("  inside(self top10): " + TopSelf(v, id, 10) + "\n");
    }
    for (var n = 1; n <= 3; n++)
    {
        var g = f + n;
        if (g > ProfilerDriver.lastFrameIndex) break;
        using (var w = ProfilerDriver.GetHierarchyFrameDataView(g, 0, HierarchyFrameDataView.ViewModes.MergeSamplesWithTheSameName, HierarchyFrameDataView.columnTotalTime, false))
        {
            if (!w.valid) continue;
            sb.Append("  +" + n + " frameMs=" + w.frameTimeMs.ToString("F1") + " self top6: " + TopSelf(w, w.GetRootItemID(), 6) + "\n");
        }
    }
}
if (probeFrames.Count == 0) sb.Append("profiler: no probe frames (first=" + ProfilerDriver.firstFrameIndex + " last=" + ProfilerDriver.lastFrameIndex + ")\n");
ProfilerDriver.ClearAllFrames();
return sb.ToString();

int FindItem(HierarchyFrameDataView v, int root, string prefix)
{
    var stack = new Stack<int>(); stack.Push(root);
    var children = new List<int>();
    while (stack.Count > 0)
    {
        var id = stack.Pop();
        if (v.GetItemName(id).StartsWith(prefix)) return id;
        v.GetItemChildren(id, children);
        foreach (var c in children) stack.Push(c);
    }
    return -1;
}

string TopSelf(HierarchyFrameDataView v, int root, int count)
{
    var agg = new Dictionary<string, float>();
    var stack = new Stack<int>(); stack.Push(root);
    var children = new List<int>();
    while (stack.Count > 0)
    {
        var id = stack.Pop();
        v.GetItemChildren(id, children);
        foreach (var c in children) { var n = v.GetItemName(c); agg[n] = (agg.TryGetValue(n, out var s) ? s : 0) + v.GetItemColumnDataAsFloat(c, HierarchyFrameDataView.columnSelfTime); stack.Push(c); }
    }
    return string.Concat(agg.OrderByDescending(k => k.Value).Take(count).Select(kv => kv.Key.Replace("\"", "'") + "=" + kv.Value.ToString("F1") + "ms; "));
}
