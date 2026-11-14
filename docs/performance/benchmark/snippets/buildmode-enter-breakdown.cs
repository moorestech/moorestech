// 設置モードに入る遷移の中身を分ける：本番の遷移で新しく有効になった GameObject を持ち主ごとに数え、遷移を作る処理を1つずつ時間を測る（GameScreen 中・定常後に実行する）
// Break down the build-mode entry: count GameObjects newly activated by the real transition per owner, and time each step the transition is made of (run in GameScreen on a settled world)
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewObject;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.UIState;
using Core.Master;
using UnityEngine;
using VContainer;

var inv = System.Globalization.CultureInfo.InvariantCulture;
var bf = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance;
var control = Object.FindFirstObjectByType<UIStateControl>();
if (control == null || control.CurrentState != UIStateEnum.GameScreen) return "FAIL not in GameScreen: " + control?.CurrentState;
var updateMethod = typeof(UIStateControl).GetMethod("Update", bf);
var dictionary = (UIStateDictionary)typeof(UIStateControl).GetField("_uiStateDictionary", bf).GetValue(control);
var placeState = dictionary.GetState(UIStateEnum.PlaceBlock);
var gameScreenState = dictionary.GetState(UIStateEnum.GameScreen);
var previewDisplay = placeState.GetType().GetField("_previewDisplay", bf).GetValue(placeState);
var cameraPolicy = placeState.GetType().GetField("_cameraPolicyService", bf).GetValue(placeState);
var onStateChanged = typeof(UIStateControl).GetField("_onStateChanged", bf).GetValue(control);

// 計測スクリプトと同じく、設置対象を決めてから入る
// As in the probe, set a placement target before entering
var blockId = MasterHolder.BlockMaster.GetBlockAllIds()[0];
var target = new BlockPlacementTarget(MasterHolder.BlockMaster.GetBlockMaster(blockId).BlockGuid, null);
ClientDIContext.DIContainer.DIContainerResolver.Resolve<PlaceSystemStateController>().SetTarget(target, PlacementOrigin.NonHotbar);

// 1回目は温め（JIT・プールの初回生成）として捨てる
// Discard the first cycle as warm-up (JIT, first pool creation)
Transition(UIStateEnum.PlaceBlock);
Transition(UIStateEnum.GameScreen);

// 2. 本番の遷移で新しく有効になった GameObject を持ち主ごとに数える
// 2. Count GameObjects newly activated by the real transition, per owner
var all = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
var activeBefore = new HashSet<int>();
foreach (var t in all) if (t.gameObject.activeInHierarchy) activeBefore.Add(t.GetInstanceID());
var countedEnterMs = Transition(UIStateEnum.PlaceBlock);
var categories = new Dictionary<string, int>();
foreach (var t in all)
{
    if (t == null || !t.gameObject.activeInHierarchy || activeBefore.Contains(t.GetInstanceID())) continue;
    var key = Category(t);
    categories[key] = categories.TryGetValue(key, out var c) ? c + 1 : 1;
}
var countedExitMs = Transition(UIStateEnum.GameScreen);

// 3. 遷移だけを3回測る
// 3. Time the transition alone three times
var enterMs = new List<double>();
var exitMs = new List<double>();
for (var i = 0; i < 3; i++)
{
    enterMs.Add(Transition(UIStateEnum.PlaceBlock));
    exitMs.Add(Transition(UIStateEnum.GameScreen));
}

// 4. 遷移を作る処理を1つずつ測る（GameScreen のまま、入る→戻すの組で。各3回の中央値）
// 4. Time each step of the transition alone (staying in GameScreen, as enter-then-restore pairs; median of 3)
var steps = new Dictionary<string, (List<double> on, List<double> off)>();
for (var i = 0; i < 3; i++)
{
    TimePair("gameScreenState.OnExit/OnEnter", () => Invoke(gameScreenState, "OnExit"), () => Invoke(gameScreenState, "OnEnter", new UITransitContext(UIStateEnum.GameScreen)));
    TimePair("cameraPolicy.EnterBuildMode/EnterGameplay", () => Invoke(cameraPolicy, "EnterBuildMode"), () => Invoke(cameraPolicy, "EnterGameplay"));
    TimePair("previewDisplay.OnEnter/OnExit", () => Invoke(previewDisplay, "OnEnter"), () => Invoke(previewDisplay, "OnExit"));
    TimePair("onStateChanged subscribers", () => Invoke(onStateChanged, "OnNext", UIStateEnum.PlaceBlock), () => Invoke(onStateChanged, "OnNext", UIStateEnum.GameScreen));
}

var sb = new System.Text.StringBuilder("{");
sb.Append("\"enterMs\":" + Median(enterMs) + ",\"exitMs\":" + Median(exitMs) + ",\"countedCycle\":{\"enterMs\":" + countedEnterMs.ToString("F1", inv) + ",\"exitMs\":" + countedExitMs.ToString("F1", inv) + "}");
sb.Append(",\"steps\":{" + string.Join(",", steps.Select(s => "\"" + s.Key + "\":{\"onMs\":" + Median(s.Value.on) + ",\"offMs\":" + Median(s.Value.off) + "}")) + "}");
sb.Append(",\"newlyActiveTransforms\":{" + string.Join(",", categories.OrderByDescending(kv => kv.Value).Take(25).Select(kv => "\"" + kv.Key.Replace("\"", "'") + "\":" + kv.Value)) + "}");
sb.Append(",\"newlyActiveTotal\":" + categories.Values.Sum() + "}");
return sb.ToString();

#region Internal

double Transition(UIStateEnum next)
{
    var sw = Stopwatch.StartNew();
    control.RequestTransition(next);
    updateMethod.Invoke(control, null);
    return sw.Elapsed.TotalMilliseconds;
}

void TimePair(string name, System.Action on, System.Action off)
{
    if (!steps.ContainsKey(name)) steps[name] = (new List<double>(), new List<double>());
    var sw = Stopwatch.StartNew();
    on();
    steps[name].on.Add(sw.Elapsed.TotalMilliseconds);
    sw.Restart();
    off();
    steps[name].off.Add(sw.Elapsed.TotalMilliseconds);
}

void Invoke(object target, string method, params object[] args)
{
    var m = target.GetType().GetMethods(bf).First(x => x.Name == method && x.GetParameters().Length == args.Length);
    m.Invoke(target, args);
}

string Category(Transform t)
{
    // ブロック配下は、いちばん近いプレビュー専用部品の型で分ける。それ以外は最上位とその下1段の名前で分ける
    // Under a block, group by the nearest preview-only part type; elsewhere group by the root and its first child name
    if (t.GetComponentInParent<BlockGameObject>(true) != null)
    {
        for (var p = t; p != null; p = p.parent)
        {
            var part = p.GetComponents<MonoBehaviour>().FirstOrDefault(m => m is IPreviewOnlyObject);
            if (part != null) return "block/" + part.GetType().Name;
            if (p.GetComponent<BlockGameObject>() != null) break;
        }
        return "block/(other)";
    }
    var root = t.root;
    var path = root.name;
    for (var p = t; p != null && p.parent != null; p = p.parent)
    {
        if (p.parent == root) { path += "/" + p.name; break; }
    }
    return path;
}

string Median(List<double> values)
{
    var sorted = values.OrderBy(v => v).ToList();
    return sorted[sorted.Count / 2].ToString("F1", inv);
}

#endregion
