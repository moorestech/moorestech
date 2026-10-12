// 既存の生成版4.0.0 world(ユーザーセーブの複製)が本ブランチでもrevision4のまま無変更でロードされるかを確かめる
// 原本は絶対に開かない。複製手順と起動コマンドは .superpowers/sdd/vtg-playtest-scenario-report.md(PLAYTEST_WORLD_DIRECTORY=複製先)
// スクショは全てプレイヤー視点(Camera.main)。独自カメラは使わない
// Check that an existing generator-4.0.0 world (a copy of a user save) still loads unchanged as revision 4 on this branch.
// Never open the original; copy steps and the boot command are in the report (PLAYTEST_WORLD_DIRECTORY=<copy>).
// Every screenshot is the player view (Camera.main); no custom camera is used.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using CefUnity.Runtime;
using Client.Game.InGame.Context;
using Client.Game.InGame.Map.Outcrop;
using Client.Game.InGame.UI.UIState;
using Client.Playtest;
using Cysharp.Threading.Tasks;
using Game.Paths;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UI;

var options = new PlaytestRunOptions { Record = true, ScenarioTimeoutSeconds = 300f };
return PlaytestRunner.Run("vein-terrain-legacy-world-load", options, async p =>
{
    await BringGameViewToFront();
    await p.SkipOpeningSkitIfPlaying();
    await p.Until(() => p.CurrentUiState == UIStateEnum.GameScreen, 20f, "GameScreenに到達");
    await HideDeadCefOverlay();

    // 1: 生成版。配信メタとworld.jsonの両方が4.0.0のまま(現行5.0.0へ置換されていない)
    // 1: Generator version: both the delivered meta and world.json stay 4.0.0 (not replaced by the current 5.0.0)
    var mapData = await ClientContext.VanillaApi.Response.World.GetMapData(default);
    var meta = mapData.TerrainMeta;
    var world = p.ServerService<WorldDataDirectory>();
    var worldJson = JObject.Parse(File.ReadAllText(world.WorldMetaFilePath));
    p.Note($"[legacy] root={world.Root} generator={meta.GeneratorVersion} seed={meta.WorldSeed} id={meta.WorldId} fingerprint={worldJson["generationMasterFingerprint"]} player={p.PlayerPosition}");
    p.Assert(!world.Root.Contains("/Library/Application Support/moorestech/Saves/"), "原本セーブではなく複製を開いている");
    p.Assert(meta.GeneratorVersion == "4.0.0" && (string)worldJson["generatorVersion"] == "4.0.0", $"既存worldが生成版4.0.0のまま (meta={meta.GeneratorVersion})");
    await p.Screenshot("pv-01-legacy-saved-player-position");

    // 2: 保存r16がv4 golden(変更前の本番生成器で採取)とバイト一致する
    // 2: Saved r16 files are byte-identical to the v4 golden captured from the pre-change production generator
    var repository = Path.GetFullPath(Path.Combine(Application.dataPath, "../.."));
    var golden = JObject.Parse(File.ReadAllText(Path.Combine(repository,
        "moorestech_server/Assets/Scripts/Tests/UnitTest/Game/MapGeneration/Surface/Fixtures/legacy-v4-seed196.json")));
    foreach (var tile in golden["tiles"])
    {
        var path = world.TerrainHeightFilePath((int)tile["tileX"], (int)tile["tileZ"]);
        string actualHash;
        using (var sha = SHA256.Create()) actualHash = BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant();
        p.Assert(actualHash == (string)tile["sha256"], $"r16 {tile["fileName"]} がv4 goldenと一致");
    }

    // 3: 表示地形。調査記録で実測したv4の値(旧最悪点19.00077m・海plane低地点5.859733m)がそのまま出る
    // 3: Displayed terrain: the v4 values measured in the evidence notes (19.00077 m and 5.859733 m) come out unchanged
    var terrains = Terrain.activeTerrains.Where(t => t.name.StartsWith("Terrain_")).ToArray();
    var oldWorst = SampleTerrain(438.5f, -401.5f);
    var seaLow = SampleTerrain(-336.43f, 405.76f);
    p.Note($"[legacy] terrain old-worst={oldWorst:F6} (v4実測19.00077) sea-plane-low={seaLow:F6} (v4実測5.859733)");
    p.Assert(Mathf.Abs(oldWorst - 19.00077f) <= 0.001f, $"旧最悪点の地表がv4実測のまま {oldWorst:F6}");
    // 海plane低地点は頂点走査の最小値を小数2桁で記録した座標なので、補間差として0.02mまで許す
    // The sea-plane low point was recorded as a vertex-scan minimum at 2-decimal coordinates, so allow 0.02 m of interpolation difference
    p.Assert(Mathf.Abs(seaLow - 5.859733f) <= 0.02f, $"海plane低地点の地表がv4実測のまま {seaLow:F6}");

    // 4: 露頭。Existing表示は接地補正をしないので、全露頭rootが配信AABBの中心そのもの(旧最悪点の埋没もそのまま)
    // 4: Outcrops: Existing presentation applies no grounding, so every root sits exactly at its delivered AABB center (the old buried one included)
    var centers = new HashSet<Vector3Int>(mapData.MapVeins.Select(v => Vector3Int.RoundToInt(new Vector3(v.MinX + v.MaxX + 1, v.MinY + v.MaxY + 1, v.MinZ + v.MaxZ + 1))));
    var outcrops = UnityEngine.Object.FindObjectsByType<OutcropGameObject>(FindObjectsSortMode.None);
    var shifted = outcrops.Where(o => !centers.Contains(Vector3Int.RoundToInt(o.transform.position * 2f)) || (o.transform.position * 2f - (Vector3)Vector3Int.RoundToInt(o.transform.position * 2f)).sqrMagnitude > 1e-6f).ToList();
    p.Note($"[legacy] outcrops={outcrops.Length} mapVeins={mapData.MapVeins.Count} shiftedFromAabbCenter={shifted.Count} first={(shifted.Count == 0 ? "none" : shifted[0].name + shifted[0].transform.position)}");
    p.Assert(0 < outcrops.Length && shifted.Count == 0, $"全露頭rootがAABB中心のまま(接地補正なし) shifted={shifted.Count}");
    var copper = outcrops.OrderBy(o => Vector3.Distance(o.transform.position, new Vector3(438.5f, 17.5f, -401.5f))).First();
    p.Note($"[legacy] 旧最悪露頭 {copper.name} root={copper.transform.position} terrain={oldWorst:F5} depth={oldWorst - copper.transform.position.y:F6}");
    p.Assert(Vector3.Distance(copper.transform.position, new Vector3(438.5f, 17.5f, -401.5f)) < 0.001f, "旧最悪露頭が(438.5,17.5,-401.5)のまま");

    // 旧最悪点をプレイヤー視点で記録する(v4は埋没したまま見えるのが正しい)
    // Record the old worst point from the player view (on v4 it is correct for it to stay buried)
    var forward = Vector3.ProjectOnPlane(Camera.main.transform.forward, Vector3.up).normalized;
    if (forward.sqrMagnitude < 0.1f) forward = Vector3.forward;
    var stand = new Vector3(438.5f, 0f, -401.5f) - forward * 5f;
    p.Note("[pv] v4の旧最悪点へ移動し、埋没露頭をそのまま撮る");
    p.WarpPlayer(new Vector3(stand.x, SampleTerrain(stand.x, stand.z) + 2f, stand.z));
    await p.WaitSeconds(1.5f);
    await p.Screenshot("pv-02-legacy-old-worst-outcrop");
    p.Note("旧world複製のロード確認を終了する(AutoSave=false・セーブ操作なし)");

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
