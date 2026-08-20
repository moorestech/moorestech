// generatedワールドのobjectConfig配置（メサ・砂漠の岩と瓦礫）がクライアントでMapObjectとして実体化するかを通しで確認する
// 種別ごとの個体数を数え、BigMesa付近を俯瞰で撮って見た目も残す
// End-to-end check that objectConfig placements (mesa and desert rocks and rubble) in a generated world materialise as client MapObjects.
// Counts instances per species and takes overhead shots near a BigMesa so the look is kept as evidence too.
using Client.Game.InGame.Context;
using Client.Game.InGame.Map.MapObject;
using Client.Playtest;
using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

var options = new PlaytestRunOptions { Record = false };
return PlaytestRunner.Run("generated-world-mesa-objects-survey", options, async p =>
{
    p.Note("generatedワールドのobjectConfig配置を調べる");
    var meta = (await ClientContext.VanillaApi.Response.GetMapData(default)).TerrainMeta;
    p.Assert(meta.MapMode == "generated", "generatedモードで起動している");

    // 1: 種別ごとの個体数。objectConfig経由の種が0なら配置ステージかプレハブ解決が死んでいる
    // 1: Instance count per species; zero for any objectConfig species means the stage or prefab resolution is dead
    var all = UnityEngine.Object.FindObjectsByType<MapObjectGameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
    var counts = new SortedDictionary<string, int>();
    MapObjectGameObject bigMesa = null;
    MapObjectGameObject desertBoulder = null;
    foreach (var mapObject in all)
    {
        var name = mapObject.name.Replace("(Clone)", "").Trim();
        int current;
        counts.TryGetValue(name, out current);
        counts[name] = current + 1;
        if (bigMesa == null && name.StartsWith("BigMesa_")) bigMesa = mapObject;
        if (desertBoulder == null && name.StartsWith("DesertBoulder_")) desertBoulder = mapObject;
    }
    var report = new StringBuilder();
    foreach (var kv in counts) report.Append(kv.Key).Append("=").Append(kv.Value).Append(" ");
    p.Note("mapObjects total=" + all.Length);
    p.Note("counts: " + report.ToString());

    var prefixes = new string[] { "BigMesa_", "ThinMesa_", "Boulders_", "RubbleDense_", "RubbleSparse_", "StratMesaSharp_", "DesertBoulder_", "DesertRock_" };
    foreach (var prefix in prefixes)
    {
        var sum = 0;
        foreach (var kv in counts) if (kv.Key.StartsWith(prefix)) sum += kv.Value;
        p.Assert(sum > 0, prefix + "* がシーンに存在する (count=" + sum + ")");
    }
    var hasPlaceholder = counts.ContainsKey("Pebble") || counts.ContainsKey("Bush");
    p.Assert(!hasPlaceholder, "旧プレースホルダ(Pebble/Bush)が混ざっていない");

    // 2: BigMesa付近を俯瞰で撮る。存在確認だけでなく見た目（大きさ・沈み込み）を録る
    // 2: Overhead shots near a BigMesa; records the look (size, sink) rather than mere existence
    if (bigMesa != null)
    {
        var cameraObject = new GameObject("PlaytestSurveyCamera");
        var camera = cameraObject.AddComponent<Camera>();
        camera.depth = 100f;
        camera.farClipPlane = 10000f;
        var target = bigMesa.transform.position;
        cameraObject.transform.position = target + new Vector3(-120f, 90f, -120f);
        cameraObject.transform.LookAt(target);
        await UniTask.DelayFrame(5);
        await p.Screenshot("01-bigmesa-oblique");
        cameraObject.transform.position = target + new Vector3(0f, 400f, 0f);
        cameraObject.transform.LookAt(target);
        await UniTask.DelayFrame(5);
        await p.Screenshot("02-bigmesa-overhead");
        UnityEngine.Object.Destroy(cameraObject);
    }

    if (desertBoulder != null)
    {
        var cameraObject = new GameObject("PlaytestSurveyCamera2");
        var camera = cameraObject.AddComponent<Camera>();
        camera.depth = 100f;
        camera.farClipPlane = 10000f;
        var target = desertBoulder.transform.position;
        cameraObject.transform.position = target + new Vector3(-60f, 40f, -60f);
        cameraObject.transform.LookAt(target);
        await UniTask.DelayFrame(5);
        await p.Screenshot("03-desertboulder-oblique");
        UnityEngine.Object.Destroy(cameraObject);
    }
});
