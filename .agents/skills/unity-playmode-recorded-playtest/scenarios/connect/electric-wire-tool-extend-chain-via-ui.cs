// 電線ツール延長モデルE2E(受け入れ1/2/5): 起点プレビュー線が電柱先端から出る・接続成功で起点が接続先へ移る・右クリックで起点解除
// 検証項目: プレビュー端点=ElectricWireConnectionPointマーカー座標(ブロック中心/AABB上面ではない)、接続応答後の起点チェーン、右クリックでの起点解除
// Electric wire tool extend-model E2E (acceptance 1/2/5): preview starts at the pole tip, origin chains to the connected target, right click releases the origin.
// Verifies: preview endpoint equals the ElectricWireConnectionPoint marker (not the block center/AABB top), origin chaining after a successful response, and right-click origin release.
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
return PlaytestRunner.Run("electric-wire-tool-extend-chain-via-ui", options, async p =>
{
    await p.SetupFlatGround();
    // カメラは北(+Z)を向くためプレイヤーを南に置き、電柱は前方(高いZ)へ設置する
    // The camera faces north (+Z), so put the player south and the poles ahead (higher Z)
    p.WarpPlayer(new Vector3(8f, 34f, -6f));
    await p.WaitSeconds(0.5f);

    // 開幕スキットを飛ばしGameScreenへ抜ける
    // Skip the opening skit and reach GameScreen
    p.Note("開幕スキットをSkipインテントで飛ばす");
    var skitStore = Client.Skit.UI.SkitPresentationStateStore.Instance;
    await p.Until(() =>
    {
        var s = skitStore.GetCurrent();
        return s != null && skitStore.TrySkip(s.SessionId, s.SceneRevision).Ok;
    }, 30f, "開幕スキットのSkipインテントが受理される");
    await p.WaitUiState(UIStateEnum.GameScreen, 15f);

    // 電柱の解放・建設コストと、電線ツールの解放・消費素材(銅のワイヤー)を用意する
    // Prepare pole unlock/cost and the wire tool unlock plus its material (銅のワイヤー)
    var wireToolGuid = Guid.Parse("872372d5-2998-4fb7-826c-593ceeafcfb2");
    await p.PrepareBlockForUiPlacement("電柱", 5);
    p.ServerService<IGameUnlockStateDataController>().UnlockConnectTool(wireToolGuid);
    await p.GiveItem("銅のワイヤー", 64);

    // 既存電柱3本を直設置する（直置きは自動接続されないので起点/接続先の素材になる）
    // Place three poles directly; direct placement never auto-connects, so they are clean endpoints
    var poleAPos = new Vector3Int(5, 32, 4);
    var poleBPos = new Vector3Int(11, 32, 4);
    var poleCPos = new Vector3Int(11, 32, 10);
    p.PlaceBlockDirect("電柱", poleAPos, BlockDirection.North);
    p.PlaceBlockDirect("電柱", poleBPos, BlockDirection.North);
    p.PlaceBlockDirect("電柱", poleCPos, BlockDirection.North);
    var poleAGo = await p.WaitBlockGameObject(poleAPos);
    var poleBGo = await p.WaitBlockGameObject(poleBPos);
    await p.WaitBlockGameObject(poleCPos);

    // 電線ツールの内部状態（起点・プレビュー端点）を読むための参照を用意する
    // Resolve references used to read the tool's internal state (origin, preview endpoints)
    var resolver = ClientDIContext.DIContainer.DIContainerResolver;
    var wireSystem = resolver.Resolve<ElectricWireConnectSystem>();
    var placeController = resolver.Resolve<PlaceSystemStateController>();
    var sourceField = typeof(ElectricWireConnectSystem).GetField("_sourceBlock", BindingFlags.NonPublic | BindingFlags.Instance);
    var contextField = typeof(ElectricWireConnectSystem).GetField("_context", BindingFlags.NonPublic | BindingFlags.Instance);
    var toolContext = (ElectricWireToolContext)contextField.GetValue(wireSystem);
    var previewType = typeof(ElectricWireExtendPreviewObject);
    var cachedStartField = previewType.GetField("_cachedStart", BindingFlags.NonPublic | BindingFlags.Instance);
    var cachedEndField = previewType.GetField("_cachedEnd", BindingFlags.NonPublic | BindingFlags.Instance);
    var hasCacheField = previewType.GetField("_hasCache", BindingFlags.NonPublic | BindingFlags.Instance);

    BlockGameObject Origin() => (BlockGameObject)sourceField.GetValue(wireSystem);
    Vector3 PreviewStart() => (Vector3)cachedStartField.GetValue(toolContext.WirePreview);
    Vector3 PreviewEnd() => (Vector3)cachedEndField.GetValue(toolContext.WirePreview);
    bool PreviewHasCache() => (bool)hasCacheField.GetValue(toolContext.WirePreview);
    TextMeshPro FindLabel(string labelName) => UnityEngine.Object
        .FindObjectsByType<TextMeshPro>(FindObjectsInactive.Include, FindObjectsSortMode.None)
        .FirstOrDefault(t => t.gameObject.name == labelName);
    bool PreviewActive()
    {
        var label = FindLabel("WireCostLabel");
        return label != null && label.transform.parent != null && label.transform.parent.gameObject.activeInHierarchy;
    }

    // 電柱先端マーカーとAABB上面中央の座標差を先に記録する（どちらから線が出ているかの判定基準）
    // Record the tip marker and the AABB top center up front; they are the yardstick for where the wire starts
    Vector3 TipOf(BlockGameObject block) => block.GetComponentInChildren<ElectricWireConnectionPoint>(true).transform.position;
    var poleATip = TipOf(poleAGo);
    var poleBTip = TipOf(poleBGo);
    var poleATopCenter = new Vector3(poleAPos.x + 0.5f, poleAPos.y + 5f, poleAPos.z + 0.5f);
    var poleACenter = new Vector3(poleAPos.x + 0.5f, poleAPos.y + 2.5f, poleAPos.z + 0.5f);
    p.Note($"電柱Aの先端マーカー={poleATip} / AABB上面中央={poleATopCenter} / ブロック中心={poleACenter}");
    p.Assert(0.3f < Vector3.Distance(poleATip, poleATopCenter), "先端マーカーはAABB上面中央と別座標（判定が意味を持つ前提）");

    // 電線ツールをビルドメニュー(ツール>接続)から選択する
    // Select the wire tool from the build menu (ツール > 接続)
    p.Note("ビルドメニューのツールカテゴリから電線接続ツールを選ぶ");
    p.Assert(CefScreenMapper.IsWebUiAvailable(), "Web UI(CEF)が利用可能");
    for (var attempt = 0; attempt < 3 && p.CurrentUiState != UIStateEnum.BuildMenu; attempt++)
    {
        await p.PressKey(p.CurrentUiState == UIStateEnum.PlaceBlock ? Key.Tab : Key.B);
        var openDeadline = Time.realtimeSinceStartup + 4f;
        while (Time.realtimeSinceStartup < openDeadline && p.CurrentUiState != UIStateEnum.BuildMenu) await UniTask.DelayFrame(5);
    }
    p.Assert(p.CurrentUiState == UIStateEnum.BuildMenu, "ビルドメニューが開く");
    await p.UntilWebUiElement("build-menu-panel", 15f);
    await p.ClickWebUi("build-menu-category-d1000000-0000-4000-8000-000000000009");
    await p.ClickWebUi($"build-menu-entry-connectTool-{wireToolGuid:D}");
    await p.WaitUiState(UIStateEnum.PlaceBlock, 15f);
    p.Assert((placeController.CurrentTarget as ConnectToolPlacementTarget)?.ConnectToolGuid == wireToolGuid, "電線接続ツールが選択される");
    await p.Screenshot("00-wire-tool-selected");

    // ①電柱Aをクリックして起点にする
    // Click pole A to make it the origin
    p.Note("電柱Aをクリックして起点にする");
    await AimAtPole(poleAPos);
    await p.ClickPlace();
    await p.Until(() => Origin() != null && Origin().BlockPosInfo.OriginalPos == poleAPos, 10f, "電柱Aが起点になる");

    // ②電柱Bへ照準し、プレビュー線の起点端が電柱Aの先端マーカーから出ていることを測る
    // Aim at pole B and measure that the preview starts at pole A's tip marker
    p.Note("電柱Bへ照準してプレビュー線の起点端座標を測る");
    await AimAtPole(poleBPos);
    await p.Until(PreviewHasCache, 10f, "プレビュー線が生成される");
    var startToTip = Vector3.Distance(PreviewStart(), poleATip);
    var startToTopCenter = Vector3.Distance(PreviewStart(), poleATopCenter);
    var startToCenter = Vector3.Distance(PreviewStart(), poleACenter);
    p.Note($"プレビュー起点端={PreviewStart()} 先端マーカーまで{startToTip:F3} / AABB上面中央まで{startToTopCenter:F3} / ブロック中心まで{startToCenter:F3}");
    p.Assert(startToTip < 0.05f, $"受け入れ1: プレビュー線は電柱先端マーカーから出る (差{startToTip:F3})");
    p.Assert(0.3f < startToTopCenter && 0.3f < startToCenter, $"受け入れ1: ブロック中心/上面中央からは出ていない (上面{startToTopCenter:F3} 中心{startToCenter:F3})");
    p.Assert(Vector3.Distance(PreviewEnd(), poleBTip) < 0.05f, "プレビュー線の終点端も接続先電柱の先端マーカー");
    await p.Screenshot("01-preview-from-pole-tip");

    // ③クリックして接続。サーバー結線と、起点が接続先Bへ移ること(チェーン)を確認する
    // Click to connect, then confirm the server-side wiring and the origin chaining to pole B
    p.Note("クリックして電柱A-Bを接続する。成功応答後に起点がBへ移るはず");
    await p.ClickPlace();
    await p.Until(() => Connected(poleAPos, poleBPos), 15f, "電柱A-Bがサーバー側で結線される");
    await p.Until(() => Origin() != null && Origin().BlockPosInfo.OriginalPos == poleBPos, 15f, "受け入れ2: 接続成功後に起点が接続先Bへ移る");
    p.Note("起点が接続先の電柱Bへチェーンした");

    // ④電柱Cへ照準し、プレビュー線が(Aではなく)Bの先端から出ることで起点移動を裏取りする
    // Aim at pole C: the preview now starts at B's tip (not A's), corroborating the origin move
    await AimAtPole(poleCPos);
    await p.Until(PreviewHasCache, 10f, "チェーン後のプレビュー線が生成される");
    var chainStartToB = Vector3.Distance(PreviewStart(), poleBTip);
    var chainStartToA = Vector3.Distance(PreviewStart(), poleATip);
    p.Note($"チェーン後のプレビュー起点端={PreviewStart()} Bの先端まで{chainStartToB:F3} / Aの先端まで{chainStartToA:F3}");
    p.Assert(chainStartToB < 0.05f && 1f < chainStartToA, "受け入れ2: 次のプレビュー起点が接続先Bの先端になる");
    await p.Screenshot("02-origin-chained-to-b");

    // ⑤右クリックで起点を解除し、プレビュー線が消えることを確認する
    // Right click to release the origin and confirm the preview line disappears
    p.Note("右クリックで起点を解除する");
    SemanticInput.MouseButtonDown(1);
    await UniTask.DelayFrame(2);
    SemanticInput.MouseButtonUp(1);
    await UniTask.DelayFrame(2);
    await p.Until(() => Origin() == null, 10f, "受け入れ5: 右クリックで起点が解除される");
    await p.Until(() => !PreviewActive(), 10f, "受け入れ5: 起点解除でプレビュー線が消える");
    await p.WaitSeconds(0.5f);
    await p.Screenshot("03-origin-released-by-right-click");
    p.Note("右クリックで起点が解除され、接続プレビューが消えた");

    #region Internal

    // 対象電柱のClickCollider中心へ照準する（装飾サブメッシュ回避）
    // Aim at the pole's ClickCollider center (avoids decorative sub-meshes)
    async UniTask AimAtPole(Vector3Int blockPos)
    {
        var blockGo = await p.WaitBlockGameObject(blockPos);
        var colliders = blockGo.GetComponentsInChildren<Collider>(true);
        var clickCollider = colliders.FirstOrDefault(c => c.name == "ClickCollider") ?? colliders.First();
        await p.AimAt(clickCollider.bounds.center);
        await UniTask.DelayFrame(3);
    }

    // 2つの電柱コネクタが相互に結線を保持していれば結線済みと判定する
    // Two poles count as connected when either connector holds the other
    bool Connected(Vector3Int a, Vector3Int b)
    {
        var blockA = p.GetBlock(a);
        var blockB = p.GetBlock(b);
        if (blockA == null || blockB == null) return false;
        var ca = blockA.ComponentManager.GetComponent<IElectricWireConnector>();
        var cb = blockB.ComponentManager.GetComponent<IElectricWireConnector>();
        return ca.ContainsWireConnection(cb.BlockInstanceId) || cb.ContainsWireConnection(ca.BlockInstanceId);
    }

    #endregion
});
