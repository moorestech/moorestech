// 電線ツール孤立設置E2E(受け入れ3): 起点なしの空間クリックで電柱が単体設置され、接続0本のままそれが次の起点になる
// 検証項目: 近く(距離3=範囲内)に既存電柱があっても新設電柱の接続数は0、電線消費0、設置後に新電柱が起点になりプレビュー線がそこから出る
// Isolated pole placement E2E (acceptance 3): clicking empty space with no origin places a standalone pole with zero connections that becomes the next origin.
// Verifies: zero wire connections despite an in-range neighbouring pole, zero wire consumption, and the new pole becoming the origin.
using System;
using System.Linq;
using System.Reflection;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem;
using Client.Game.InGame.BlockSystem.PlaceSystem.ElectricWireConnect;
using Client.Game.InGame.BlockSystem.PlaceSystem.ElectricWireConnect.Parts;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Client.Game.InGame.BlockSystem.StateProcessor.ElectricWire;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.UIState;
using Client.Playtest;
using Client.Playtest.Input;
using Client.Playtest.WebUi;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using Game.EnergySystem;
using Game.UnlockState;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;

var options = new PlaytestRunOptions { Record = true };
return PlaytestRunner.Run("electric-wire-tool-isolated-place-via-ui", options, async p =>
{
    await p.SetupFlatGround();
    p.WarpPlayer(new Vector3(8f, 34f, -6f));
    await p.WaitSeconds(0.5f);

    p.Note("開幕スキットをSkipインテントで飛ばす");
    var skitStore = Client.Skit.UI.SkitPresentationStateStore.Instance;
    await p.Until(() =>
    {
        var s = skitStore.GetCurrent();
        return s != null && skitStore.TrySkip(s.SessionId, s.SceneRevision).Ok;
    }, 30f, "開幕スキットのSkipインテントが受理される");
    await p.WaitUiState(UIStateEnum.GameScreen, 15f);

    var wireToolGuid = Guid.Parse("872372d5-2998-4fb7-826c-593ceeafcfb2");
    await p.PrepareBlockForUiPlacement("電柱", 5);
    p.ServerService<IGameUnlockStateDataController>().UnlockConnectTool(wireToolGuid);
    await p.GiveItem("銅のワイヤー", 64);

    // 既存電柱を1本だけ直設置する（新設位置から距離3＝接続範囲内なので、自動配線があれば必ず繋がる配置）
    // Place a single existing pole; the new pole lands 3 blocks away (in range), so any auto-wiring would connect
    var neighborPolePos = new Vector3Int(5, 32, 4);
    p.PlaceBlockDirect("電柱", neighborPolePos, BlockDirection.North);
    var neighborGo = await p.WaitBlockGameObject(neighborPolePos);

    var resolver = ClientDIContext.DIContainer.DIContainerResolver;
    var wireSystem = resolver.Resolve<ElectricWireConnectSystem>();
    var placeController = resolver.Resolve<PlaceSystemStateController>();
    var sourceField = typeof(ElectricWireConnectSystem).GetField("_sourceBlock", BindingFlags.NonPublic | BindingFlags.Instance);
    var contextField = typeof(ElectricWireConnectSystem).GetField("_context", BindingFlags.NonPublic | BindingFlags.Instance);
    var toolContext = (ElectricWireToolContext)contextField.GetValue(wireSystem);
    var previewType = typeof(ElectricWireExtendPreviewObject);
    var cachedStartField = previewType.GetField("_cachedStart", BindingFlags.NonPublic | BindingFlags.Instance);
    var hasCacheField = previewType.GetField("_hasCache", BindingFlags.NonPublic | BindingFlags.Instance);
    BlockGameObject Origin() => (BlockGameObject)sourceField.GetValue(wireSystem);
    Vector3 PreviewStart() => (Vector3)cachedStartField.GetValue(toolContext.WirePreview);
    bool PreviewHasCache() => (bool)hasCacheField.GetValue(toolContext.WirePreview);

    // 電線ツールをビルドメニュー(ツール>接続)から選択する
    // Select the wire tool from the build menu (ツール > 接続)
    p.Note("ビルドメニューのツールカテゴリから電線接続ツールを選ぶ");
    for (var attempt = 0; attempt < 3 && p.CurrentUiState != UIStateEnum.BuildMenu; attempt++)
    {
        await p.PressKey(p.CurrentUiState == UIStateEnum.PlaceBlock ? Key.Tab : Key.B);
        var openDeadline = Time.realtimeSinceStartup + 4f;
        while (Time.realtimeSinceStartup < openDeadline && p.CurrentUiState != UIStateEnum.BuildMenu) await UniTask.DelayFrame(5);
    }
    await p.UntilWebUiElement("build-menu-panel", 15f);
    await p.ClickWebUi("build-menu-category-d1000000-0000-4000-8000-000000000009");
    await p.ClickWebUi($"build-menu-entry-connectTool-{wireToolGuid:D}");
    await p.WaitUiState(UIStateEnum.PlaceBlock, 15f);
    p.Assert((placeController.CurrentTarget as ConnectToolPlacementTarget)?.ConnectToolGuid == wireToolGuid, "電線接続ツールが選択される");
    p.Assert(Origin() == null, "ツール選択直後は起点未選択");

    // 起点未選択のまま、既存電柱から3ブロック離れた空間へ照準して電柱ゴーストを出す
    // With no origin, aim at empty ground 3 blocks from the existing pole to raise the pole ghost
    var wireBefore = p.CountItem("銅のワイヤー");
    p.Note($"起点なしの状態で空間へ照準する（銅のワイヤー所持 {wireBefore}）");
    await p.AimAt(new Vector3(8.5f, 32f, 4.5f));
    await UniTask.DelayFrame(5);
    await p.Screenshot("01-isolated-pole-ghost");

    // クリックして孤立設置する
    // Click to place the isolated pole
    p.Note("空間をクリックして電柱を孤立設置する");
    await p.ClickPlace();
    await p.Until(() => Origin() != null, 20f, "受け入れ3: 孤立設置した電柱が次の起点になる");

    var placedPos = Origin().BlockPosInfo.OriginalPos;
    p.Note($"孤立設置された電柱の座標={placedPos}（既存電柱は{neighborPolePos}）");
    p.Assert(placedPos != neighborPolePos, "起点になったのは新設された電柱（既存電柱ではない）");
    var placedBlock = p.GetBlock(placedPos);
    p.Assert(placedBlock != null, "サーバー側に新設電柱が存在する");

    // 接続0本であることをサーバー状態で確認する（範囲内の既存電柱があっても配線されない）
    // Confirm zero wire connections server-side even though an in-range pole exists
    var placedConnector = placedBlock.ComponentManager.GetComponent<IElectricWireConnector>();
    var neighborConnector = p.GetBlock(neighborPolePos).ComponentManager.GetComponent<IElectricWireConnector>();
    await p.WaitSeconds(1.5f);
    p.Assert(placedConnector.WireConnections.Count == 0, $"受け入れ3: 新設電柱の接続数は0 実際:{placedConnector.WireConnections.Count}");
    p.Assert(neighborConnector.WireConnections.Count == 0, $"受け入れ3: 範囲内の既存電柱側も接続0のまま 実際:{neighborConnector.WireConnections.Count}");
    p.Assert(!placedConnector.ContainsWireConnection(neighborConnector.BlockInstanceId), "受け入れ3: 近傍の既存電柱へ自動配線されない");
    p.Assert(Vector3Int.Distance(placedPos, neighborPolePos) <= 6f, $"新設電柱と既存電柱は接続範囲内の距離 実際:{Vector3Int.Distance(placedPos, neighborPolePos):F1}");

    // 電線は1本も消費されない（電柱の建設コスト分の銅のワイヤー10のみ減る）
    // No wire is consumed; only the pole's construction cost (銅のワイヤー x10) is spent
    var wireAfter = p.CountItem("銅のワイヤー");
    p.Note($"銅のワイヤー: {wireBefore} → {wireAfter}（電柱の建設コスト10のみ）");
    p.Assert(wireBefore - wireAfter == 10, $"受け入れ3: 配線用の電線消費は0（建設コスト10のみ） 実際の減少:{wireBefore - wireAfter}");
    await p.Screenshot("02-isolated-pole-placed");

    // 起点になっていることを、既存電柱へ照準したときのプレビュー起点端で裏取りする
    // Corroborate the origin by the preview start point when aiming at the existing pole
    var placedGo = await p.WaitBlockGameObject(placedPos);
    var placedTip = placedGo.GetComponentInChildren<ElectricWireConnectionPoint>(true).transform.position;
    var neighborColliders = neighborGo.GetComponentsInChildren<Collider>(true);
    var neighborClick = neighborColliders.FirstOrDefault(c => c.name == "ClickCollider") ?? neighborColliders.First();
    await p.AimAt(neighborClick.bounds.center);
    await p.Until(PreviewHasCache, 10f, "孤立設置した電柱を起点とするプレビュー線が出る");
    p.Note($"プレビュー起点端={PreviewStart()} 新設電柱の先端={placedTip}");
    p.Assert(Vector3.Distance(PreviewStart(), placedTip) < 0.05f, "受け入れ3: プレビュー線が孤立設置した電柱の先端から出る（＝起点になっている）");
    await p.Screenshot("03-isolated-pole-is-origin");
});
