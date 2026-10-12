// seed196のrevision5 worldで 手掘り→採掘機→ポンプ→範囲表示→保存 を通し、再ロード照合用の期待値を書き出す
// tourと同じPLAYTEST_WORLD_DIRECTORYで2回目に起動する。期待値は <world>.vtg-expect.json(world直下を汚さない兄弟ファイル)へ置く
// 録画・スクショは全てプレイヤー視点(Camera.main)。CEF不調時はWeb UI経路を[env-workaround]の非UI経路へ置き換える
// On the seed-196 revision-5 world, run hand mining -> miner -> pump -> range display -> save, then write reload expectations.
// Boot second with the same PLAYTEST_WORLD_DIRECTORY as the tour; expectations go to the sibling file <world>.vtg-expect.json.
// Every frame is the player view (Camera.main); while CEF is down, Web UI steps are swapped for non-UI paths noted as [env-workaround].
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using CefUnity.Runtime;
using Client.Common;
using Client.Game.InGame.Context;
using Client.Game.InGame.Control;
using Client.Game.InGame.Map.MapVein;
using Client.Game.InGame.Map.Outcrop;
using Client.Game.InGame.Presenter.PauseMenu;
using Client.Game.InGame.UI.UIState;
using Client.Playtest;
using Client.Playtest.Input;
using Client.Playtest.Operations;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.Context;
using Game.MapGeneration.Surface;
using Game.Paths;
using Game.World.Interface.DataStore;
using Newtonsoft.Json;
using Server.Protocol.PacketResponse.MapData;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using VContainer;

var ironVeinGuid = "fe3ccb53-4902-4191-a4bd-c8fe1f8a086d";
var waterVeinGuid = "ca978c1e-4ce9-4096-849b-ffbf277206dc";
var options = new PlaytestRunOptions { Record = true, ScenarioTimeoutSeconds = 380f };
return PlaytestRunner.Run("vein-terrain-seed196-gameplay-save", options, async p =>
{
    await BringGameViewToFront();
    await p.SkipOpeningSkitIfPlaying();
    await p.Until(() => p.CurrentUiState == UIStateEnum.GameScreen, 20f, "GameScreenに到達");
    await HideDeadCefOverlay();
    var inputSettings = InputSystem.settings;
    var (savedBackground, savedEditorInput) = (inputSettings.backgroundBehavior, inputSettings.editorInputBehaviorInPlayMode);
    // 非前面Editorでは既定設定がキーボードを無効化しF/数字キー注入が届かない。シナリオ中だけ全入力をGame Viewへ通す(終了時に戻す)
    // A background Editor disables the keyboard under the defaults, so route all input to the Game View during the scenario (restored at the end)
    inputSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
    inputSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
    foreach (var device in new InputDevice[] { Keyboard.current, Mouse.current }) InputSystem.EnableDevice(device);
    p.Note($"[env-workaround] 非前面Editorでキーボードが無効化されるため入力設定を一時変更 ({savedBackground}/{savedEditorInput} -> IgnoreFocus/AllDeviceInputAlwaysGoesToGameView)");
    var mapData = await ClientContext.VanillaApi.Response.World.GetMapData(default);
    var meta = mapData.TerrainMeta;
    var world = p.ServerService<WorldDataDirectory>();
    p.Note($"[world] root={world.Root} generator={meta.GeneratorVersion} id={meta.WorldId} saveExists={File.Exists(world.SaveJsonFilePath)}");
    p.Assert(meta.GeneratorVersion == "5.0.0" && meta.WorldSeed == 196, "tourで作ったrevision5・seed196 worldを開いている");

    // 地形を上書きしないよう足場は作らず、無料設置(全建築解放)だけ有効にしてスポーンへ留まる
    // No scaffold (it would cover real terrain); enable free placement (all builds unlocked) only and stay at spawn
    var spawn = p.PlayerPosition;
    await p.SetupDebugEnvironment(new PlaytestEnvironmentConfig { CreateFlatGround = false, SpawnPosition = spawn + Vector3.up * 1.5f });
    var terrains = Terrain.activeTerrains.Where(t => t.name.StartsWith("Terrain_")).ToArray();
    var forward = Vector3.ProjectOnPlane(Camera.main.transform.forward, Vector3.up).normalized is var f && f.sqrMagnitude > 0.1f ? f : Vector3.forward;
    var minLand = (double)SurfaceEnvelope.GeneratedV5.SeaY + SurfaceEnvelope.GeneratedV5.MaximumWaveRise + SurfaceEnvelope.GeneratedV5.LandClearance;

    // 1: 手掘り。最寄り鉄鉱石露頭の本体(最大Collider)の手前に立ち、視点を下げて画面中央レイを当ててF長押しで掘る
    // 1: Hand mining: stand before the nearest iron outcrop's main collider, pitch the view down onto it, and hold F
    var ironVein = NearestVein(ironVeinGuid);
    var ironOutcrop = UnityEngine.Object.FindFirstObjectByType<OutcropGameObjectDatastore>().SearchNearestOutcrop(new Guid(ironVeinGuid), Center(ironVein));
    var ironCollider = ironOutcrop.GetComponentsInChildren<Collider>(true).OrderByDescending(c => c.bounds.size.sqrMagnitude).First();
    p.Note($"[pv] 手掘り: 鉄鉱石露頭 {ironOutcrop.name} 本体={ironCollider.name} center={ironCollider.bounds.center} ext={ironCollider.bounds.extents}");
    p.GiveItemDirect("石の斧", 1);
    await p.EquipItem("石の斧", 0);
    var stand = ironCollider.bounds.center - forward * (Mathf.Max(ironCollider.bounds.extents.x, ironCollider.bounds.extents.z) + 1.2f);
    p.WarpPlayer(new Vector3(stand.x, SampleTerrain(stand.x, stand.z) + 1f, stand.z));
    await p.WaitSeconds(1f);
    await PitchViewOnto(ironOutcrop);
    var oreBefore = p.CountItem("鉄鉱石");
    for (var attempt = 0; attempt < 3 && p.CountItem("鉄鉱石") <= oreBefore; attempt++) await p.HoldInteract(2.5f);
    await p.Until(() => oreBefore < p.CountItem("鉄鉱石"), 15f, "手掘りで鉄鉱石が増えた");
    p.Note($"[pv] 手掘り結果: 鉄鉱石 {oreBefore} -> {p.CountItem("鉄鉱石")}");
    await p.Screenshot("pv-01-hand-mined");

    // 2-3: 採掘機を鉄鉱石鉱脈へ、ポンプを水鉱脈へ置き、保持中の範囲表示も検査する
    // 2-3: Place the miner on the iron vein and the pump on the water vein, checking the range display while held
    var miner = await PlaceOnVein("原始的な採掘機", ironVein, "02-miner");
    var pump = await PlaceOnVein("歯車ポンプ", NearestVein(waterVeinGuid), "04-pump");

    // 4: ポーズメニューの「セーブ」と同じGameSaveRequester.Save()を呼び、save.jsonの更新を待つ
    // 4: Call GameSaveRequester.Save(), the very request the pause-menu Save button sends, and wait for save.json
    p.Note("[env-workaround] CEF不調でポーズメニュー(Web UI)が出ないため、セーブボタンと同じGameSaveRequester.Save()を直接呼ぶ");
    var savedBefore = File.Exists(world.SaveJsonFilePath) ? File.GetLastWriteTimeUtc(world.SaveJsonFilePath) : DateTime.MinValue;
    ClientDIContext.DIContainer.DIContainerResolver.Resolve<GameSaveRequester>().Save();
    await p.Until(() => File.Exists(world.SaveJsonFilePath) && savedBefore < File.GetLastWriteTimeUtc(world.SaveJsonFilePath), 60f, "セーブ要求でsave.jsonが書き出された");

    // 5: 再ロード照合の期待値。設置2件・固定点と鉱脈中心とタイル中心の地表高・全r16のSHA256
    // 5: Reload expectations: both placed blocks, terrain heights at fixed points, vein centers and tile centers, and every r16 SHA256
    var points = new List<Vector3> { new Vector3(438.5f, 0f, -401.5f), new Vector3(-336.43f, 0f, 405.76f), Center(ironVein), Center(NearestVein(waterVeinGuid)) };
    points.AddRange(terrains.Select(t => t.transform.position + new Vector3(t.terrainData.size.x * 0.5f, 0f, t.terrainData.size.z * 0.5f)));
    var expect = new { meta.WorldId, meta.GeneratorVersion, savedAtUtc = File.GetLastWriteTimeUtc(world.SaveJsonFilePath).ToString("O"), blocks = new[] { miner, pump },
        heights = points.Select(v => new { x = v.x, z = v.z, y = SampleTerrain(v.x, v.z) }).ToArray(),
        r16 = terrains.Select(t => t.name.Split('_')).ToDictionary(n => $"{n[1]}_{n[2]}", n => Sha256(world.TerrainHeightFilePath(int.Parse(n[1]), int.Parse(n[2])))) };
    var expectPath = world.Root.TrimEnd('/', '\\') + ".vtg-expect.json";
    File.WriteAllText(expectPath, JsonConvert.SerializeObject(expect, Formatting.Indented));
    p.Note($"[expect] {expectPath} blocks={JsonConvert.SerializeObject(expect.blocks)}");
    await p.Screenshot("pv-06-saved");
    (inputSettings.backgroundBehavior, inputSettings.editorInputBehaviorInPlayMode) = (savedBackground, savedEditorInput);
    p.Note("保存完了・入力設定を復元。シナリオ終了後ランナーがPlayModeを止める(=終了)。同じworldでreload-verifyを起動する");

    #region Internal

    async UniTask PitchViewOnto(OutcropGameObject outcrop)
    {
        // GameScreenの照準は画面中央固定(ADR 0008)。低い露頭は実プレイヤー同様、Look(ポインタdelta)で視点を下げて中央レイを2m内に当てる
        // GameScreen aims at screen center (ADR 0008); like a player, pitch down via Look (pointer delta) until the center ray hits within 2 m
        var (mask, hitPoint) = (LayerConst.BlockOnlyLayerMask | LayerConst.MapObjectOnlyLayerMask, Vector3.zero);
        bool Aimed() => BlockClickDetectUtil.TryGetFrontmostSolidHit(mask, 100f, out var hit) && hit.collider.GetComponentInParent<OutcropGameObject>() == outcrop
            && Vector3.Distance(p.PlayerPosition, hitPoint = hit.point) <= 1.9f;
        for (var frame = 0; frame < 600 && !Aimed(); frame++)
        {
            SemanticInput.MouseDragBy(new Vector2(0f, -3f));
            await UniTask.Yield();
        }
        await p.WaitSeconds(0.5f);
        await p.Until(Aimed, 5f, "画面中央レイが手の届く距離で鉄鉱石露頭に当たる");
        p.Note($"[pv] 照準: 露頭ヒット点={hitPoint} プレイヤーから{Vector3.Distance(p.PlayerPosition, hitPoint):F2}m");
    }
    async UniTask<object> PlaceOnVein(string blockName, VeinLayoutMessagePack vein, string label)
    {
        // 鉱脈の手前8mに立ち、ホットバーで持った瞬間の範囲ボックスを数えてから鉱脈中心の地表へ照準して置く
        // Stand 8 m short of the vein, count range boxes once held via the hotbar, then aim at the ground at the vein center and place
        var center = Center(vein);
        var (ground, standPoint) = (SampleTerrain(center.x, center.z), center - forward * 8f);
        p.Note($"[pv] {blockName}を鉱脈{vein.VeinGuid} ({vein.MinX},{vein.MinY},{vein.MinZ})-({vein.MaxX},{vein.MaxY},{vein.MaxZ}) に置く groundY={ground:F4}");
        p.WarpPlayer(new Vector3(standPoint.x, SampleTerrain(standPoint.x, standPoint.z) + 1.5f, standPoint.z));
        await p.WaitSeconds(1f);
        p.Note("[env-workaround] CEF不調でビルドメニュー(Web UI)が出ないため、同じ設置対象カタログのホットバー割当→数字キーで持つ");
        await p.Hotbar.AssignHotbar(0, blockName);
        await p.Hotbar.EnterBuildMode(0);
        await p.Until(() => RangeBoxes().Count > 0, 10f, $"{blockName}保持中に鉱脈範囲ボックスが表示される");
        var gaps = RangeBoxes().Select(b => b.bounds.min.y - SampleTerrain(b.bounds.center.x, b.bounds.center.z)).Where(g => !float.IsNaN(g)).DefaultIfEmpty(float.NaN).ToList();
        p.Note($"[pv] 範囲表示: boxes={gaps.Count} 底面-地表 min={gaps.Min():F4} max={gaps.Max():F4}");
        p.Assert(gaps.Min() >= -0.001f, $"{blockName}の範囲ボックス底面が地表以上 (min gap={gaps.Min():F4})");
        await p.Screenshot($"pv-{label}-range-display");
        await p.AimAt(new Vector3(center.x, ground, center.z));
        await p.ClickPlace();
        var blockGuid = MasterHolder.BlockMaster.GetBlockMaster(PlaytestBlockOps.ResolveBlockId(blockName)).BlockGuid;
        WorldBlockData placed = null;
        await p.Until(() => (placed = FindOnVein(blockGuid, vein)) != null, 15f, $"{blockName}が鉱脈上に設置された");
        var origin = placed.BlockPositionInfo.OriginalPos;
        await p.WaitBlockGameObject(origin);
        await p.Hotbar.ExitBuildMode(0);
        p.Note($"[pv] 設置完了 {blockName} origin={origin} originY={origin.y} veinMinY={vein.MinY} terrainUnder={SampleTerrain(origin.x + 0.5f, origin.z + 0.5f):F4} min={minLand:F2}");
        p.Assert(origin.y == vein.MinY, $"{blockName}の原点Yが鉱脈範囲の底Yと一致しパッド上に載る (originY={origin.y} veinMinY={vein.MinY})");
        await p.Screenshot($"pv-{label}-placed");
        return new { name = blockName, guid = blockGuid.ToString(), x = origin.x, y = origin.y, z = origin.z };
    }
    // 設置YはTerrainの地表探査で決まるのでXZの重なりだけで同定する
    // Placement Y comes from ground probing, so identify the block by XZ overlap alone
    WorldBlockData FindOnVein(Guid blockGuid, VeinLayoutMessagePack vein) => ServerContext.WorldBlockDatastore.BlockMasterDictionary.Values.FirstOrDefault(b => b.Block.BlockGuid == blockGuid
        && b.BlockPositionInfo.MinPos.x <= vein.MaxX && vein.MinX <= b.BlockPositionInfo.MaxPos.x && b.BlockPositionInfo.MinPos.z <= vein.MaxZ && vein.MinZ <= b.BlockPositionInfo.MaxPos.z);
    List<MeshRenderer> RangeBoxes() => GameObject.Find(MapVeinRangeViewService.RootObjectName)?.GetComponentsInChildren<MeshRenderer>(false).ToList() ?? new List<MeshRenderer>();
    VeinLayoutMessagePack NearestVein(string veinGuid) => mapData.MapVeins.Where(v => v.VeinGuid == veinGuid).OrderBy(v => Vector3.Distance(Center(v), spawn)).First();
    Vector3 Center(VeinLayoutMessagePack v) => new Vector3((v.MinX + v.MaxX + 1) * 0.5f, (v.MinY + v.MaxY + 1) * 0.5f, (v.MinZ + v.MaxZ + 1) * 0.5f);
    float SampleTerrain(float x, float z)
    {
        // 共有境界は原点の辞書順で所有タイルを選ぶ(OutcropSurfacePlacementと同じ)
        // Shared borders pick the owner tile by lexicographic origin (same as OutcropSurfacePlacement)
        var owner = terrains.Where(t => t.transform.position.x <= x && x <= t.transform.position.x + t.terrainData.size.x && t.transform.position.z <= z && z <= t.transform.position.z + t.terrainData.size.z)
            .OrderBy(t => t.transform.position.x).ThenBy(t => t.transform.position.z).FirstOrDefault();
        return owner == null ? float.NaN : owner.SampleHeight(new Vector3(x, 0f, z)) + owner.transform.position.y;
    }
    string Sha256(string path) => BitConverter.ToString(SHA256.Create().ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant();
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
