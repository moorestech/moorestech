// 設置モードに入るときのプレビュー専用部品の有効化を、種類ごとと「半径内だけ有効にした場合」に分けて測る（設置モードの外・定常後に実行する）
// Measure activation of build-mode preview-only parts on entry, split per part type and for "activate only within the radius" (run outside build mode on a settled world)
// 本番の経路（全ブロックへ SetPreviewOnlyObjectsDisplay）を測り、そのあと種類ごとに GameObject.SetActive だけを測る。各3回の中央値
// Time the production path (SetPreviewOnlyObjectsDisplay on every block), then GameObject.SetActive alone per type; median of 3 each
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Client.Game.InGame.Block;
using Client.Game.InGame.Block.Preview;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewObject;
using Client.Game.InGame.Context;
using UnityEngine;

var inv = System.Globalization.CultureInfo.InvariantCulture;
var blocks = ClientDIContext.BlockGameObjectDataStore.BlockGameObjectDictionary.Values.ToList();
// SetPreviewOnlyObjectsDisplay は internal なので、開いたデリゲートにして呼ぶ（呼び出しの手間は直接呼ぶのとほぼ同じ）
// SetPreviewOnlyObjectsDisplay is internal, so call it through an open delegate (about the same cost as a direct call)
var setDisplayMethod = typeof(BlockGameObject).GetMethod("SetPreviewOnlyObjectsDisplay", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
var setDisplay = (System.Action<BlockGameObject, PreviewOnlyObjectsDisplay>)System.Delegate.CreateDelegate(typeof(System.Action<BlockGameObject, PreviewOnlyObjectsDisplay>), setDisplayMethod);
var sb = new System.Text.StringBuilder("{\"blocks\":" + blocks.Count);

// 1. 本番の経路：全ブロックを半径内描画モードにする（入る）／隠す（出る）
// 1. Production path: put every block in radius-rendered mode (enter) / hide it (exit)
var enterMs = new List<double>();
var exitMs = new List<double>();
for (var i = 0; i < 3; i++)
{
    var sw = Stopwatch.StartNew();
    foreach (var block in blocks) setDisplay(block, PreviewOnlyObjectsDisplay.RenderedWithinPlayerRadius);
    enterMs.Add(sw.Elapsed.TotalMilliseconds);
    if (i < 2)
    {
        sw.Restart();
        foreach (var block in blocks) setDisplay(block, PreviewOnlyObjectsDisplay.Hidden);
        exitMs.Add(sw.Elapsed.TotalMilliseconds);
    }
}

// 入った状態で、種類ごとに数と「描画中（＝半径内）」を数える
// While entered, count each type and how many are rendered (= within the radius)
var types = new[] { typeof(PreviewOnlyObject), typeof(InventoryConnectorView), typeof(GearConnectorView) };
var perType = new Dictionary<System.Type, (List<GameObject> all, List<GameObject> inRadius, int transforms)>();
foreach (var type in types)
{
    var all = Object.FindObjectsByType(type, FindObjectsInactive.Include, FindObjectsSortMode.None).Cast<Component>()
        .Where(c => c.GetComponentInParent<BlockGameObject>(true) != null).Select(c => c.gameObject).ToList();
    var inRadius = all.Where(g => g.GetComponentsInChildren<Renderer>(true).Any(r => r.enabled)).ToList();
    var transforms = all.Sum(g => g.GetComponentsInChildren<Transform>(true).Length);
    perType[type] = (all, inRadius, transforms);
}
var lastExit = Stopwatch.StartNew();
foreach (var block in blocks) setDisplay(block, PreviewOnlyObjectsDisplay.Hidden);
exitMs.Add(lastExit.Elapsed.TotalMilliseconds);

sb.Append(",\"productionEnterMs\":" + Median(enterMs) + ",\"productionExitMs\":" + Median(exitMs) + ",\"types\":{");

// 2. 種類ごとに SetActive(true/false) だけを測る（全部／半径内だけ）
// 2. Time SetActive(true/false) alone per type (all / within the radius only)
var first = true;
foreach (var type in types)
{
    var (all, inRadius, transforms) = perType[type];
    var allOn = new List<double>();
    var allOff = new List<double>();
    var radiusOn = new List<double>();
    for (var i = 0; i < 3; i++)
    {
        var sw = Stopwatch.StartNew();
        foreach (var g in all) g.SetActive(true);
        allOn.Add(sw.Elapsed.TotalMilliseconds);
        sw.Restart();
        foreach (var g in all) g.SetActive(false);
        allOff.Add(sw.Elapsed.TotalMilliseconds);
        sw.Restart();
        foreach (var g in inRadius) g.SetActive(true);
        radiusOn.Add(sw.Elapsed.TotalMilliseconds);
        foreach (var g in inRadius) g.SetActive(false);
    }
    sb.Append((first ? "" : ",") + "\"" + type.Name + "\":{\"count\":" + all.Count + ",\"inRadius\":" + inRadius.Count + ",\"transforms\":" + transforms
              + ",\"activateAllMs\":" + Median(allOn) + ",\"deactivateAllMs\":" + Median(allOff) + ",\"activateInRadiusMs\":" + Median(radiusOn) + "}");
    first = false;
}
sb.Append("}");

// 3. 有効化を除いた、全ブロックをたどる経路だけの時間（すでに Hidden のブロックへ Hidden をもう一度指示する）
// 3. Cost of walking every block without activation (issue Hidden again to already hidden blocks)
var loopMs = new List<double>();
for (var i = 0; i < 3; i++)
{
    var sw = Stopwatch.StartNew();
    foreach (var block in blocks) setDisplay(block, PreviewOnlyObjectsDisplay.Hidden);
    loopMs.Add(sw.Elapsed.TotalMilliseconds);
}
sb.Append(",\"walkAllBlocksHiddenAgainMs\":" + Median(loopMs) + "}");
return sb.ToString();

#region Internal

string Median(List<double> values)
{
    var sorted = values.OrderBy(v => v).ToList();
    return sorted[sorted.Count / 2].ToString("F1", inv);
}

#endregion
