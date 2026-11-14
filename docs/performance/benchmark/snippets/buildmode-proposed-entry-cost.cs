// 「半径外の矢印（PreviewOnlyObject）は有効にしない」場合の設置モード入りの時間を、本番と同じ手順を再現して部品の種類ごとに測る
// 設置モード中（buildmode-enter-only.cs の数秒後、半径の判定済み）に実行する。最初に GameScreen へ戻ってから測る
// Time build-mode entry if out-of-radius arrows (PreviewOnlyObject) were not activated, replaying the production steps and splitting per part type
// Run while in build mode (a few seconds after buildmode-enter-only.cs, radius evaluated); it returns to GameScreen first, then measures
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Client.Game.InGame.Block;
using Client.Game.InGame.Block.Preview;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewObject;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.UIState;
using UnityEngine;

var inv = System.Globalization.CultureInfo.InvariantCulture;
var bf = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance;
var control = Object.FindFirstObjectByType<UIStateControl>();
if (control == null || control.CurrentState != UIStateEnum.PlaceBlock) return "FAIL not in PlaceBlock: " + control?.CurrentState;
var blocks = ClientDIContext.BlockGameObjectDataStore.BlockGameObjectDictionary.Values.ToList();
var viewField = typeof(BlockGameObject).GetField("_previewOnlyObjectsView", bf);
var partsField = typeof(BlockPreviewOnlyObjectsView).GetField("_previewOnlyObjects", bf);
var withinField = typeof(BlockPreviewOnlyObjectsView).GetField("_isWithinPlayerRadius", bf);
var setDisplay = (System.Action<BlockGameObject, PreviewOnlyObjectsDisplay>)System.Delegate.CreateDelegate(typeof(System.Action<BlockGameObject, PreviewOnlyObjectsDisplay>), typeof(BlockGameObject).GetMethod("SetPreviewOnlyObjectsDisplay", bf));

// 設置モード中に判定された「半径内か」を読む（GameScreen へ戻っても記録は残る）
// Read the within-radius flags evaluated during build mode (they stay recorded after returning to GameScreen)
var entries = blocks.Select(b => { var view = viewField.GetValue(b); return (block: b, parts: (List<IPreviewOnlyObject>)partsField.GetValue(view), view); }).ToList();
var withinCount = entries.Count(e => (bool)withinField.GetValue(e.view));
control.RequestTransition(UIStateEnum.GameScreen);
typeof(UIStateControl).GetMethod("Update", bf).Invoke(control, null);
var within = entries.Select(e => (bool)withinField.GetValue(e.view)).ToArray();

// 1. 今の本番の手順（半径に関係なく全部を有効にする）
// 1. Current production steps (activate everything regardless of the radius)
var currentMs = new List<double>();
for (var i = 0; i < 3; i++)
{
    var sw = Stopwatch.StartNew();
    foreach (var e in entries) setDisplay(e.block, PreviewOnlyObjectsDisplay.RenderedWithinPlayerRadius);
    currentMs.Add(sw.Elapsed.TotalMilliseconds);
    HideAll();
}

// 2. 変更後の手順の再現：矢印だけ半径内のときに有効にし、ほかの部品は今と同じ指示を出す
// 2. Replay of the changed steps: activate arrows only within the radius; other parts get the same instructions as now
var proposedMs = new List<double>();
for (var i = 0; i < 3; i++)
{
    var sw = Stopwatch.StartNew();
    for (var b = 0; b < entries.Count; b++)
    {
        var w = within[b];
        foreach (var part in entries[b].parts)
        {
            part.SetActive(part is PreviewOnlyObject ? w : true);
            part.SetEnableRenderers(w);
        }
    }
    proposedMs.Add(sw.Elapsed.TotalMilliseconds);
    HideAll();
}

// 3. 同じ再現を、部品の種類と呼び出しごとに時間を積み上げて測る（計測の手間ぶん合計は増える）
// 3. The same replay, accumulating time per part type and call (the total grows by the timing overhead)
var ticks = new Dictionary<string, long>();
var calls = new Dictionary<string, int>();
var loopSw = Stopwatch.StartNew();
for (var b = 0; b < entries.Count; b++)
{
    var w = within[b];
    foreach (var part in entries[b].parts)
    {
        var name = part.GetType().Name;
        var t0 = Stopwatch.GetTimestamp();
        part.SetActive(part is PreviewOnlyObject ? w : true);
        var t1 = Stopwatch.GetTimestamp();
        part.SetEnableRenderers(w);
        var t2 = Stopwatch.GetTimestamp();
        Add(name + ".SetActive", t1 - t0);
        Add(name + ".SetEnableRenderers(" + (w ? "on" : "off") + ")", t2 - t1);
    }
}
var instrumentedTotalMs = loopSw.Elapsed.TotalMilliseconds;
HideAll();

var sb = new System.Text.StringBuilder("{");
sb.Append("\"blocks\":" + blocks.Count + ",\"withinRadius\":" + withinCount + ",\"partsTotal\":" + entries.Sum(e => e.parts.Count));
sb.Append(",\"currentEnterMs\":" + Median(currentMs) + ",\"proposedEnterMs\":" + Median(proposedMs) + ",\"instrumentedTotalMs\":" + instrumentedTotalMs.ToString("F1", inv));
sb.Append(",\"perCall\":{" + string.Join(",", ticks.OrderByDescending(kv => kv.Value).Select(kv => "\"" + kv.Key + "\":{\"ms\":" + (kv.Value * 1000.0 / Stopwatch.Frequency).ToString("F1", inv) + ",\"calls\":" + calls[kv.Key] + "}")) + "}}");
return sb.ToString();

#region Internal

void HideAll()
{
    foreach (var e in entries) setDisplay(e.block, PreviewOnlyObjectsDisplay.Hidden);
}

void Add(string key, long t)
{
    ticks[key] = (ticks.TryGetValue(key, out var v) ? v : 0) + t;
    calls[key] = (calls.TryGetValue(key, out var c) ? c : 0) + 1;
}

string Median(List<double> values)
{
    var sorted = values.OrderBy(v => v).ToList();
    return sorted[sorted.Count / 2].ToString("F1", inv);
}

#endregion
