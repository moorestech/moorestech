// 設置モード入りの作り方の案を、本番と同じ部品への指示を再現して比べる（各5回の中央値）
// 設置モード中（buildmode-enter-only.cs の数秒後、半径の判定済み）に実行する。最初に GameScreen へ戻ってから測る
// Compare candidate ways of entering build mode by replaying the same part instructions as production (median of 5 each)
// Run while in build mode (a few seconds after buildmode-enter-only.cs, radius evaluated); it returns to GameScreen first, then measures
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Client.Game.InGame.Block;
using Client.Game.InGame.Block.Preview;
using Client.Game.InGame.Block.Preview.PlacementFrame;
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

var withinBlocks = Enumerable.Range(0, entries.Count).Where(b => within[b]).ToList();
var results = new List<string>();

// 0. 今の本番の手順（半径に関係なく全部を有効にする）
// 0. Current production steps (activate everything regardless of the radius)
Measure("0_current", () =>
{
    foreach (var e in entries) setDisplay(e.block, PreviewOnlyObjectsDisplay.RenderedWithinPlayerRadius);
});

// 1. 矢印だけ半径内で有効にする（ほかは今と同じ指示）
// 1. Activate arrows only within the radius (other parts get the same instructions as now)
Measure("1_arrowsRadiusOnly", () =>
{
    for (var b = 0; b < entries.Count; b++)
    {
        var w = within[b];
        foreach (var part in entries[b].parts)
        {
            part.SetActive(part is PreviewOnlyObject ? w : true);
            part.SetEnableRenderers(w);
        }
    }
});

// 2. 1 に加え、歯車の接続線も半径内で有効にする
// 2. On top of 1, activate gear connector lines only within the radius too
Measure("2_arrowsAndGearRadiusOnly", () =>
{
    for (var b = 0; b < entries.Count; b++)
    {
        var w = within[b];
        foreach (var part in entries[b].parts)
        {
            part.SetActive(part is PreviewOnlyObject || part is GearConnectorView ? w : true);
            part.SetEnableRenderers(w);
        }
    }
});

// 3. 2 に加え、半径外の矢印・歯車の線・枠の借り口には何も呼ばない（隠れたままでよい）。搬入出口の線は今と同じ
// 3. On top of 2, make no calls to out-of-radius arrows, gear lines and frame leases (they stay hidden); inventory connector lines as now
Measure("3_skipHiddenCalls", () =>
{
    for (var b = 0; b < entries.Count; b++)
    {
        var w = within[b];
        foreach (var part in entries[b].parts)
        {
            if (!w && (part is PreviewOnlyObject || part is GearConnectorView || part is BlockPreviewEdgeFrameLease)) continue;
            part.SetActive(true);
            part.SetEnableRenderers(w);
        }
    }
});

// 4. すべての部品を半径内だけにし、半径外のブロックには何も呼ばない（全ブロックは回る）
// 4. Every part within the radius only; no calls for out-of-radius blocks (still walks every block)
Measure("4_allPartsRadiusOnly", () =>
{
    for (var b = 0; b < entries.Count; b++)
    {
        if (!within[b]) continue;
        foreach (var part in entries[b].parts)
        {
            part.SetActive(true);
            part.SetEnableRenderers(true);
        }
    }
});

// 5. 半径内のブロックの一覧だけを回る
// 5. Walk only the list of within-radius blocks
Measure("5_walkWithinListOnly", () =>
{
    foreach (var b in withinBlocks)
    {
        foreach (var part in entries[b].parts)
        {
            part.SetActive(true);
            part.SetEnableRenderers(true);
        }
    }
});

return "{\"blocks\":" + blocks.Count + ",\"withinRadius\":" + withinCount + ",\"partsTotal\":" + entries.Sum(e => e.parts.Count) + "," + string.Join(",", results) + "}";

#region Internal

void Measure(string name, System.Action enter)
{
    var ms = new List<double>();
    for (var i = 0; i < 5; i++)
    {
        var sw = Stopwatch.StartNew();
        enter();
        ms.Add(sw.Elapsed.TotalMilliseconds);
        HideAll();
    }
    results.Add("\"" + name + "\":" + Median(ms));
}

void HideAll()
{
    foreach (var e in entries) setDisplay(e.block, PreviewOnlyObjectsDisplay.Hidden);
}

string Median(List<double> values)
{
    var sorted = values.OrderBy(v => v).ToList();
    return sorted[sorted.Count / 2].ToString("F1", inv);
}

#endregion
