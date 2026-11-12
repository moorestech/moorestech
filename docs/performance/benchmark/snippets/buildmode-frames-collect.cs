// 設置モード遷移の前後に記録したフレームの最大・合計と、最大フレームの上位を返す
// Return max/total frame time around the build-mode transition and the top items of the slowest frame
// 最大フレームは溜まった受信イベントの処理で決まることがあるので、遷移を処理した UIStateControl.Update のフレームも別に返す
// The slowest frame can be a burst of queued events, so also report the frame where UIStateControl.Update handled the transition
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor.Profiling;
using UnityEditorInternal;
ProfilerDriver.enabled = false;
var sb = new StringBuilder();
var frameMsList = new List<string>();
float max = 0, total = 0, transitionUpdateMs = -1, transitionFrameMs = -1; int maxFrame = -1, transitionFrame = -1, frames = 0;
for (int f = ProfilerDriver.firstFrameIndex; f <= ProfilerDriver.lastFrameIndex; f++)
{
    using (var v = ProfilerDriver.GetHierarchyFrameDataView(f, 0, HierarchyFrameDataView.ViewModes.MergeSamplesWithTheSameName, HierarchyFrameDataView.columnTotalTime, false))
    {
        if (!v.valid) continue;
        frames++; total += v.frameTimeMs;
        frameMsList.Add(v.frameTimeMs.ToString("F0"));
        if (v.frameTimeMs > max) { max = v.frameTimeMs; maxFrame = f; }
        var uiUpdate = SumTotalByNameFragment(v, "UIStateControl.Update");
        if (uiUpdate > transitionUpdateMs) { transitionUpdateMs = uiUpdate; transitionFrame = f; transitionFrameMs = v.frameTimeMs; }
    }
}
// 記録が読めないまま最大フレームを引くと例外になるので、空なら理由を返す
// Reading the slowest frame of an empty recording throws, so report why instead
if (maxFrame < 0) return "{\"error\":\"no profiler frames (first=" + ProfilerDriver.firstFrameIndex + " last=" + ProfilerDriver.lastFrameIndex + ")\"}";
sb.Append("{\"frames\":" + frames + ",\"maxFrameMs\":" + max.ToString("F1") + ",\"totalMs\":" + total.ToString("F1"));
sb.Append(",\"frameMsList\":\"" + string.Join(" ", frameMsList) + "\"");
sb.Append(",\"top\":\"" + TopSelf(maxFrame, 15) + "\"");
sb.Append(",\"transitionUIStateUpdateMs\":" + transitionUpdateMs.ToString("F1") + ",\"transitionFrameMs\":" + transitionFrameMs.ToString("F1"));
sb.Append(",\"transitionTop\":\"" + TopSelf(transitionFrame, 10) + "\"}");
ProfilerDriver.ClearAllFrames();
return sb.ToString();

float SumTotalByNameFragment(HierarchyFrameDataView v, string fragment)
{
    float sum = 0;
    var stack = new Stack<int>(); stack.Push(v.GetRootItemID());
    var children = new List<int>();
    while (stack.Count > 0)
    {
        var id = stack.Pop();
        if (v.GetItemName(id).Contains(fragment)) { sum += v.GetItemColumnDataAsFloat(id, HierarchyFrameDataView.columnTotalTime); continue; }
        v.GetItemChildren(id, children);
        foreach (var c in children) stack.Push(c);
    }
    return sum;
}

string TopSelf(int frame, int count)
{
    using (var v = ProfilerDriver.GetHierarchyFrameDataView(frame, 0, HierarchyFrameDataView.ViewModes.MergeSamplesWithTheSameName, HierarchyFrameDataView.columnTotalTime, false))
    {
        var agg = new Dictionary<string, float>();
        var stack = new Stack<int>(); stack.Push(v.GetRootItemID());
        var children = new List<int>();
        while (stack.Count > 0)
        {
            var id = stack.Pop();
            v.GetItemChildren(id, children);
            foreach (var c in children) { var n = v.GetItemName(c); agg[n] = (agg.TryGetValue(n, out var s) ? s : 0) + v.GetItemColumnDataAsFloat(c, HierarchyFrameDataView.columnSelfTime); stack.Push(c); }
        }
        return string.Concat(agg.OrderByDescending(k => k.Value).Take(count).Select(kv => kv.Key.Replace("\"", "'") + "=" + kv.Value.ToString("F1") + "ms; "));
    }
}
