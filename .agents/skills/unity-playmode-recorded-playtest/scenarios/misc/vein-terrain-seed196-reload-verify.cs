// gameplay-saveで保存したseed196 revision5 worldを再ロードし、設置ブロック・地表高・r16・生成版が保存前と同じかを <world>.vtg-expect.json と照合する
// tour/gameplay-saveと同じPLAYTEST_WORLD_DIRECTORYで3回目に起動する。スクショは全てプレイヤー視点(Camera.main)
// Reload the seed-196 revision-5 world saved by gameplay-save and compare placed blocks, terrain heights, r16 files and generator version against <world>.vtg-expect.json.
// Boot third with the same PLAYTEST_WORLD_DIRECTORY as tour/gameplay-save; every screenshot is the player view (Camera.main).
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using CefUnity.Runtime;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.UIState;
using Client.Playtest;
using Cysharp.Threading.Tasks;
using Game.MapGeneration.Surface;
using Game.Paths;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UI;

var options = new PlaytestRunOptions { Record = true, ScenarioTimeoutSeconds = 300f };
return PlaytestRunner.Run("vein-terrain-seed196-reload-verify", options, async p =>
{
    await BringGameViewToFront();
    await p.SkipOpeningSkitIfPlaying();
    await p.Until(() => p.CurrentUiState == UIStateEnum.GameScreen, 20f, "GameScreenに到達");
    await HideDeadCefOverlay();

    // 保存前に書いた期待値を読む。無ければgameplay-saveが完走していないので以降は無意味
    // Read the expectations written before saving; without them gameplay-save never finished and nothing below means anything
    var world = p.ServerService<WorldDataDirectory>();
    var expectPath = world.Root.TrimEnd('/', '\\') + ".vtg-expect.json";
    p.Assert(File.Exists(expectPath), $"期待値ファイルがある {expectPath}");
    var expect = JObject.Parse(File.ReadAllText(expectPath));
    var meta = (await ClientContext.VanillaApi.Response.World.GetMapData(default)).TerrainMeta;
    var worldJson = JObject.Parse(File.ReadAllText(world.WorldMetaFilePath));
    p.Note($"[reload] root={world.Root} id={meta.WorldId} generator={meta.GeneratorVersion} savedAt={expect["savedAtUtc"]} saveMtime={File.GetLastWriteTimeUtc(world.SaveJsonFilePath):O}");
    p.Assert(meta.WorldId == (string)expect["WorldId"], $"同じworldを再ロードした id={meta.WorldId}");
    p.Assert(meta.GeneratorVersion == "5.0.0" && (string)worldJson["generatorVersion"] == "5.0.0", "再ロード後も生成版5.0.0のまま");

    // 1: 設置ブロック。同じ原点に同じGUIDのブロックがサーバーにあり、クライアント表示も出る
    // 1: Placed blocks: the server holds the same GUID at the same origin, and the client view spawns
    var terrains = Terrain.activeTerrains.Where(t => t.name.StartsWith("Terrain_")).ToArray();
    var forward = Vector3.ProjectOnPlane(Camera.main.transform.forward, Vector3.up).normalized;
    if (forward.sqrMagnitude < 0.1f) forward = Vector3.forward;
    var index = 0;
    foreach (var block in expect["blocks"])
    {
        index++;
        var origin = new Vector3Int((int)block["x"], (int)block["y"], (int)block["z"]);
        var actual = p.GetBlock(origin);
        p.Note($"[pv] 再ロード後の{block["name"]} at {origin}: actual={(actual == null ? "none" : actual.BlockGuid.ToString())}");
        p.Assert(actual != null && actual.BlockGuid.ToString() == (string)block["guid"], $"{block["name"]}が保存前と同じ原点{origin}に残っている");
        var stand = new Vector3(origin.x + 1f, 0f, origin.z + 1f) - forward * 8f;
        p.WarpPlayer(new Vector3(stand.x, SampleTerrain(stand.x, stand.z) + 1.5f, stand.z));
        await p.WaitBlockGameObject(origin);
        await p.WaitSeconds(1f);
        await p.Screenshot($"pv-{index:00}-reloaded-{block["name"]}");
    }

    // 2: 地表高。保存前に採取した全点を同じ本番Terrainから取り直して一致を見る(4.9m下限も再確認)
    // 2: Terrain heights: resample every pre-save point from the production Terrain and compare (re-checking the 4.9 m floor)
    var envelope = SurfaceEnvelope.GeneratedV5;
    var minLand = (double)envelope.SeaY + envelope.MaximumWaveRise + envelope.LandClearance;
    var worstDelta = 0f;
    foreach (var point in expect["heights"])
    {
        var x = (float)point["x"];
        var z = (float)point["z"];
        var delta = Mathf.Abs(SampleTerrain(x, z) - (float)point["y"]);
        worstDelta = Mathf.Max(worstDelta, float.IsNaN(delta) ? float.PositiveInfinity : delta);
        p.Note($"[height] ({x:F2},{z:F2}) before={(float)point["y"]:F5} after={SampleTerrain(x, z):F5} >=min={SampleTerrain(x, z) >= minLand - 1e-4}");
    }
    p.Assert(worstDelta <= 1e-5f, $"再ロード前後の地表高が一致 worstDelta={worstDelta:G6}");
    var oldWorst = SampleTerrain(438.5f, -401.5f);
    p.Assert(oldWorst >= minLand - 1e-4, $"再ロード後も旧最悪点の地表{oldWorst:F5}m >= {minLand:F2}m");

    // 3: 保存済みr16。ファイル単位で保存前のSHA256と一致する(再ロードで地形が作り直されていない)
    // 3: Saved r16 files match their pre-save SHA256 one by one (reload did not rebuild the terrain)
    foreach (var pair in (JObject)expect["r16"])
    {
        var tile = pair.Key.Split('_');
        var path = world.TerrainHeightFilePath(int.Parse(tile[0]), int.Parse(tile[1]));
        string actualHash;
        using (var sha = SHA256.Create()) actualHash = BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant();
        p.Assert(actualHash == (string)pair.Value, $"r16 {pair.Key} のSHA256が保存前と一致");
    }
    p.Note("再ロード照合を終了する");

    #region Internal

    float SampleTerrain(float x, float z)
    {
        // 共有境界は原点の辞書順で所有タイルを選ぶ(OutcropSurfacePlacementと同じ)
        // Shared borders pick the owner tile by lexicographic origin (same as OutcropSurfacePlacement)
        var owner = terrains.Where(t => t.transform.position.x <= x && x <= t.transform.position.x + t.terrainData.size.x && t.transform.position.z <= z && z <= t.transform.position.z + t.terrainData.size.z)
            .OrderBy(t => t.transform.position.x).ThenBy(t => t.transform.position.z).FirstOrDefault();
        return owner == null ? float.NaN : owner.SampleHeight(new Vector3(x, 0f, z)) + owner.transform.position.y;
    }

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
