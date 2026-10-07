// seed196新規world(生成revision5)の地表保証ツアー。旧最悪点・海plane低地点・fluid鉱脈・海岸・tile継目でプレイヤー位置の地表高>=海面包絡を実測する
// 固定world入口(PLAYTEST_WORLD_DIRECTORY=未作成パス / PLAYTEST_MAP_MODE=generated / PLAYTEST_SEED=196)で起動する。手順は .superpowers/sdd/vtg-playtest-scenario-report.md
// スクショ pv-* はプレイヤー視点(Camera.main)、diag-* だけが独自診断カメラ。録画中もNoteの[pv]/[diag]で区別する
// Surface-guarantee tour of a fresh seed-196 world (generator revision 5): terrain under the player must clear the sea envelope at five points.
// Boot through the fixed-world entry (PLAYTEST_WORLD_DIRECTORY=<not yet created>, PLAYTEST_MAP_MODE=generated, PLAYTEST_SEED=196).
// pv-* shots are the player view (Camera.main); only diag-* uses a custom diagnostic camera, and Notes tag them [pv]/[diag].
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CefUnity.Runtime;
using Client.Game.InGame.Context;
using Client.Game.InGame.Map.Outcrop;
using Client.Game.InGame.UI.UIState;
using Client.Playtest;
using Client.Playtest.Core;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.MapGeneration.Surface;
using Game.MapGeneration.Transfer;
using Game.Paths;
using Mooresmaster.Model.MapModule;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UI;

var runName = "vein-terrain-seed196-tour";
var options = new PlaytestRunOptions { Record = true, ScenarioTimeoutSeconds = 380f };
return PlaytestRunner.Run(runName, options, async p =>
{
    await BringGameViewToFront();
    await p.SkipOpeningSkitIfPlaying();
    await p.Until(() => p.CurrentUiState == UIStateEnum.GameScreen, 20f, "GameScreenに到達");
    await HideDeadCefOverlay();

    // 本番生成入口で作られた新規worldか(revision5・seed196・未セーブ)をメタとworld.jsonで確かめる
    // Confirm via meta and world.json that this is a fresh production-generated world (revision 5, seed 196, never saved)
    var mapData = await ClientContext.VanillaApi.Response.World.GetMapData(default);
    var meta = mapData.TerrainMeta;
    var world = p.ServerService<WorldDataDirectory>();
    var worldJson = JObject.Parse(File.ReadAllText(world.WorldMetaFilePath));
    p.Note($"[world] root={world.Root} generator={meta.GeneratorVersion} seed={meta.WorldSeed} tiles={meta.TerrainTileCount} id={meta.WorldId} createdAt={worldJson["createdAt"]} saveExists={File.Exists(world.SaveJsonFilePath)}");
    p.Assert(meta.MapMode == "generated" && meta.WorldSeed == 196, "generatedモード・seed196で起動した");
    p.Assert(meta.GeneratorVersion == WorldGeneratorVersion.Current && WorldGeneratorVersion.Current == "5.0.0", $"新規worldの生成版が5.0.0 (meta={meta.GeneratorVersion})");
    p.Assert((string)worldJson["generatorVersion"] == "5.0.0", "world.jsonのgeneratorVersionが5.0.0");

    // 判定閾値はSurfaceEnvelope.GeneratedV5から組む(SeaY+波+余裕=4.9m)。float補間誤差だけ許す
    // The threshold comes from SurfaceEnvelope.GeneratedV5 (SeaY + wave + clearance = 4.9 m), allowing only float interpolation noise
    var envelope = SurfaceEnvelope.GeneratedV5;
    var minLand = (double)envelope.SeaY + envelope.MaximumWaveRise + envelope.LandClearance;
    const double tolerance = 1e-4;
    var terrains = Terrain.activeTerrains.Where(t => t.name.StartsWith("Terrain_")).ToArray();
    p.Assert(terrains.Length == meta.TerrainTileCount, $"Terrain_x_zが{meta.TerrainTileCount}枚 (actual={terrains.Length})");
    var tileSize = terrains[0].terrainData.size.x;
    var side = Mathf.RoundToInt(Mathf.Sqrt(meta.TerrainTileCount));
    var spawn = p.PlayerPosition;
    var forward = Vector3.ProjectOnPlane(Camera.main.transform.forward, Vector3.up).normalized is var f && f.sqrMagnitude > 0.1f ? f : Vector3.forward;
    var probes = new List<object>();
    await p.Screenshot("pv-00-spawn");

    // 1-2: 調査記録の固定2点(旧露頭最悪点・海plane低地点)。座標は docs/research/2026-10-06-vein-terrain-evidence
    // 1-2: The two fixed points from the evidence notes (old worst outcrop, sea-plane low point)
    await Visit("old-worst-438.5,-401.5", new Vector3(438.5f, 0f, -401.5f));
    await Visit("sea-plane-low--336.43,405.76", new Vector3(-336.43f, 0f, 405.76f));

    // 3: スポーン最寄りのfluid鉱脈。露頭を画面正面に置くため手前4mに立ち、露頭の接地も測る
    // 3: The fluid vein nearest to spawn; stand 4 m short so it is in view, and measure its grounding too
    var fluid = mapData.MapVeins.Where(v => MasterHolder.MapVeinMaster.GetElementOrNull(new Guid(v.VeinGuid))?.VeinParam is FluidVeinParam)
        .OrderBy(v => Vector3.Distance(Center(v), spawn)).First();
    var fluidCenter = Center(fluid);
    await Visit($"fluid-vein-{fluid.VeinGuid}", fluidCenter - forward * 4f);
    var fluidOutcrop = NearestOutcrop(fluidCenter);
    var fluidGap = fluidOutcrop == null ? float.NaN : (float)fluidOutcrop["bottomGap"];
    p.Note($"[pv] fluid露頭 center={fluidCenter} outcrop={JsonConvert.SerializeObject(fluidOutcrop)}");
    p.Assert(fluidGap >= -0.001f && fluidGap <= 0.03f, $"fluid露頭のmesh底面が地表に接地 gap={fluidGap:F4}");

    // 4: 海岸。陸(>=4.9m)に立ち、カメラ正面12m先が海面(SeaY)未満になる点をスポーン最寄りで探す
    // 4: Coastline: the land point (>= 4.9 m) nearest to spawn whose ground 12 m ahead of the camera is below SeaY
    var coast = FindCoast();
    p.Assert(coast.HasValue, "海岸(陸→カメラ正面が海)の点が見つかった");
    if (coast.HasValue) await Visit("coastline", coast.Value);

    // 5: tile継目。スポーンに最も近い内部継目x線上で、両側タイルとも陸の点に立ち、両Terrainの高さ差も測る
    // 5: Tile seam: on the internal seam x-line nearest to spawn, stand where both tiles are land and compare both terrains
    var seamX = Enumerable.Range(1, side - 1).Select(k => meta.SceneOrigin.X + k * tileSize).OrderBy(x => Mathf.Abs(x - spawn.x)).First();
    var seamZ = Enumerable.Range(0, 500).SelectMany(i => new[] { spawn.z + i * 4f, spawn.z - i * 4f })
        .First(z => SampleTerrain(seamX - 0.25f, z) >= minLand && SampleTerrain(seamX + 0.25f, z) >= minLand);
    var west = terrains.First(t => Mathf.Approximately(t.transform.position.x + tileSize, seamX) && t.transform.position.z <= seamZ && seamZ <= t.transform.position.z + tileSize);
    var east = terrains.First(t => Mathf.Approximately(t.transform.position.x, seamX) && t.transform.position.z <= seamZ && seamZ <= t.transform.position.z + tileSize);
    var seamPoint = new Vector3(seamX, 0f, seamZ);
    var westHeight = west.SampleHeight(seamPoint) + west.transform.position.y;
    var eastHeight = east.SampleHeight(seamPoint) + east.transform.position.y;
    p.Note($"[seam] x={seamX} z={seamZ:F1} {west.name}={westHeight:F5} {east.name}={eastHeight:F5} delta={Mathf.Abs(westHeight - eastHeight):F6}");
    p.Assert(Mathf.Abs(westHeight - eastHeight) <= 0.01f, $"tile継目の両側Terrain高さが一致 delta={Mathf.Abs(westHeight - eastHeight):F6}");
    await Visit($"tile-seam-x{seamX}", seamPoint);

    // 継目は真上からでないと段差/テクスチャ境界が見えないため、ここだけ独自診断カメラで撮って即破棄する
    // Seams only show from straight above, so a custom diagnostic camera takes this one shot and is destroyed at once
    p.Note("[diag] 独自診断カメラ(プレイヤー視点ではない)で継目を真上60mから撮る");
    var diagnostic = new GameObject("VtgDiagnosticCamera").AddComponent<Camera>();
    diagnostic.depth = 100f;
    diagnostic.transform.SetPositionAndRotation(new Vector3(seamX, westHeight + 60f, seamZ), Quaternion.Euler(90f, 0f, 0f));
    await p.WaitSeconds(0.7f);
    await p.Screenshot("diag-01-seam-overhead");
    UnityEngine.Object.Destroy(diagnostic.gameObject);
    await p.WaitSeconds(0.5f);
    p.Note("[pv] 診断カメラを破棄しプレイヤー視点へ戻した");
    await p.Screenshot("pv-99-back-to-player-view");

    #region Internal

    async UniTask<Dictionary<string, object>> Visit(string label, Vector3 target)
    {
        // 地表+2mへワープして着地を待ち、プレイヤーのXZ直下を本番Terrainから採取する
        // Warp 2 m above ground, let the player land, then sample the production Terrain under the player's XZ
        p.Note($"[pv] {label}: ({target.x:F2},{target.z:F2}) へプレイヤーを立たせる");
        p.WarpPlayer(new Vector3(target.x, (SampleTerrain(target.x, target.z) ?? 60f) + 2f, target.z));
        await p.WaitSeconds(1.5f);
        var player = p.PlayerPosition;
        var height = SampleTerrain(player.x, player.z);
        var outcrop = NearestOutcrop(player);
        p.Note($"[pv] {label}: player=({player.x:F2},{player.y:F2},{player.z:F2}) terrain={height:F5} min={minLand:F2} nearestOutcrop={JsonConvert.SerializeObject(outcrop)}");
        p.Assert(height.HasValue && height.Value >= minLand - tolerance, $"{label}: プレイヤー位置の地表{height:F5}m >= {minLand:F2}m");
        probes.Add(new { label, targetX = target.x, targetZ = target.z, playerX = player.x, playerY = player.y, playerZ = player.z, terrainY = height, outcrop });
        File.WriteAllText(Path.Combine(PlaytestPaths.SessionDirectory, runName, "terrain-probes.json"),
            JsonConvert.SerializeObject(new { meta.WorldId, meta.GeneratorVersion, meta.WorldSeed, minLand, probes }, Formatting.Indented));
        await p.Screenshot($"pv-{probes.Count:00}-{label}");
        return outcrop;
    }
    float? SampleTerrain(float x, float z)
    {
        // 共有境界はOutcropSurfacePlacementと同じく原点の辞書順で所有タイルを選ぶ
        // Shared borders pick the owner tile by lexicographic origin, as OutcropSurfacePlacement does
        Terrain selected = null;
        foreach (var terrain in terrains)
        {
            var origin = terrain.transform.position;
            if (x < origin.x || x > origin.x + tileSize || z < origin.z || z > origin.z + tileSize) continue;
            if (selected == null || origin.x < selected.transform.position.x || (origin.x == selected.transform.position.x && origin.z < selected.transform.position.z)) selected = terrain;
        }
        return selected == null ? (float?)null : selected.SampleHeight(new Vector3(x, 0f, z)) + selected.transform.position.y;
    }
    Dictionary<string, object> NearestOutcrop(Vector3 from)
    {
        var nearest = UnityEngine.Object.FindObjectsByType<OutcropGameObject>(FindObjectsSortMode.None)
            .OrderBy(o => Vector2.Distance(new Vector2(o.transform.position.x, o.transform.position.z), new Vector2(from.x, from.z))).FirstOrDefault();
        if (nearest == null) return null;
        var position = nearest.transform.position;
        var bottom = nearest.GetComponentsInChildren<Renderer>().Select(r => r.bounds.min.y).DefaultIfEmpty(float.NaN).Min();
        var ground = SampleTerrain(position.x, position.z) ?? float.NaN;
        return new Dictionary<string, object> { ["name"] = nearest.name, ["x"] = position.x, ["y"] = position.y, ["z"] = position.z, ["meshBottomY"] = bottom, ["terrainY"] = ground, ["bottomGap"] = bottom - ground,
            ["distanceXZ"] = Vector2.Distance(new Vector2(position.x, position.z), new Vector2(from.x, from.z)) };
    }
    Vector3? FindCoast()
    {
        // 全タイルを6m格子で走査する(約25万点)。Terrain.SampleHeightのみで物理は使わない
        // Scan every tile on a 6 m lattice (about 250k points), using Terrain.SampleHeight only
        Vector3? best = null;
        var bestDistance = float.MaxValue;
        var span = side * tileSize;
        for (var x = meta.SceneOrigin.X; x < meta.SceneOrigin.X + span; x += 6f)
        for (var z = meta.SceneOrigin.Y; z < meta.SceneOrigin.Y + span; z += 6f)
        {
            if (!(SampleTerrain(x, z) >= minLand)) continue;
            var ahead = new Vector3(x, 0f, z) + forward * 12f;
            if (!(SampleTerrain(ahead.x, ahead.z) < envelope.SeaY)) continue;
            var distance = Vector2.Distance(new Vector2(x, z), new Vector2(spawn.x, spawn.z));
            if (distance >= bestDistance) continue;
            bestDistance = distance;
            best = new Vector3(x, 0f, z);
        }
        return best;
    }
    Vector3 Center(Server.Protocol.PacketResponse.MapData.VeinLayoutMessagePack v) => new Vector3((v.MinX + v.MaxX + 1) * 0.5f, (v.MinY + v.MaxY + 1) * 0.5f, (v.MinZ + v.MaxZ + 1) * 0.5f);
    async UniTask HideDeadCefOverlay()
    {
        // GameScreen到達後3秒でもCEFが一度も描画しないとき(既知 moorestech-wsnf)だけ、全画面の白RawImageをシナリオ側で隠して実プレイ視点を録る
        // Only when CEF has not painted once 3 s after GameScreen (known moorestech-wsnf), hide the white full-screen RawImage scenario-side
        var (cef, deadline) = (CefUnityBrowserSample.DiagnosticsInstance, Time.realtimeSinceStartup + 3f);
        while (cef != null && cef.DiagnosticsTexturesApplied == 0 && Time.realtimeSinceStartup < deadline) await UniTask.Yield();
        var overlay = cef == null ? null : cef.GetComponentInChildren<RawImage>(true);
        if (overlay == null || 0 < cef.DiagnosticsTexturesApplied || !overlay.isActiveAndEnabled) { p.Note($"[env] CEFは描画中かオーバーレイ無し textures={(cef == null ? 0 : cef.DiagnosticsTexturesApplied)}"); return; }
        overlay.enabled = false;
        p.Note("[env] CEF起動失敗(既知 moorestech-wsnf)のため白オーバーレイを非表示にした");
    }
    async UniTask BringGameViewToFront()
    {
        // Game Viewが裏タブだとRecorderがtimeScale=0で固まるため表へ出す(pause-menu-hierarchy.csと同じ手当て)
        // A Game View behind another tab pins timeScale at 0 under the Recorder, so bring it to front (same fix as pause-menu-hierarchy.cs)
        var (flags, gameView) = (System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public, Resources.FindObjectsOfTypeAll<UnityEditor.EditorWindow>().First(w => w.GetType().Name == "GameView"));
        var dockArea = typeof(UnityEditor.EditorWindow).GetField("m_Parent", flags).GetValue(gameView);
        var panes = (System.Collections.IList)dockArea.GetType().GetField("m_Panes", flags).GetValue(dockArea);
        dockArea.GetType().GetProperty("selected", flags).SetValue(dockArea, panes.IndexOf(gameView));
        await p.Until(() => 0f < Time.timeScale, 10f, "Recorderの初回フレーム取得でtimeScaleが戻る");
    }

    #endregion
});
