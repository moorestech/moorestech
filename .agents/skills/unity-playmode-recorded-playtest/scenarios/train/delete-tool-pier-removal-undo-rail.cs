// 削除ツールで橋脚を撤去するとレールも巻き込まれて素材が返り、Ctrl+Zで橋脚とレールが同じ種類・同じ往復2辺で戻ることの録画検証（電線・チェーンは connect/delete-tool-cut-and-undo-connection-lines.cs）
// 記録フックは DragDeleteSelection.CommitDelete と BuildUndoService（Ctrl+Z）にあるため、操作は必ず削除ツールUIへの注入で行う
// Recorded check: removing a pier with the delete tool drops its rail with a refund, and Ctrl+Z restores pier and rail with the same type and both directed edges (wires/chains: connect/delete-tool-cut-and-undo-connection-lines.cs)
// Recording hooks live in DragDeleteSelection.CommitDelete and BuildUndoService (Ctrl+Z), so every operation is injected into the delete-tool UI
using System;
using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.BlockSystem.PlaceSystem.Undo.Removal;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.UIState;
using Client.Game.InGame.UI.UIState.State.DragDelete;
using Client.Playtest;
using Client.Playtest.Input;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Server.Event.Notification;
using UniRx;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;

return PlaytestRunner.Run("delete-tool-pier-removal-undo-rail", new PlaytestRunOptions { Record = true }, async p =>
{
    // 拒否・復元失敗・例外のログ（サーバー処理は別スレッドのためThreaded）と拒否通知（サーバー発・クライアント発）を集める
    // Collect refusal/restore-failure/exception logs (Threaded, as server handling runs off the main thread) and denied notifications
    var badLogs = new List<string>(); var denied = new List<string>();
    var badTags = new[] { "denied.", "[RemovalRestore]", "[ConnectionLineDelete]", "[RemovalPreview]", "[RailConnectByDestination] endpoint", "[RailConnectByDestination] connection denied" };
    Application.logMessageReceivedThreaded += (condition, _, type) => { if (!condition.StartsWith("[Playtest]") && (type == LogType.Exception || type == LogType.Error || type == LogType.Warning && badTags.Any(condition.Contains))) lock (badLogs) badLogs.Add($"{type}: {condition.Split('\n')[0]}"); };
    ClientContext.VanillaApi.Event.SubscribeEventResponse(NotificationService.EventTag, payload => { var m = MessagePack.MessagePackSerializer.Deserialize<NotificationMessagePack>(payload); if (m.Category == NotificationCategory.OperationDenied) denied.Add(m.MessageId); });
    ClientDIContext.ClientLocalNotificationSource.OnNotification.Subscribe(m => { if (m.Category == NotificationCategory.OperationDenied) denied.Add(m.MessageId); });
    var resolver = ClientDIContext.DIContainer.DIContainerResolver;
    var restoreSender = resolver.Resolve<IRemovalRestoreSender>();
    p.Assert(ClientDIContext.ConnectionLineRegistry != null && ClientDIContext.BlockAttachedConnectionResolver != null && restoreSender is VanillaRemovalRestoreSender, "DI: 通知発行元・接続線索引・付随線解決・復元送信器がPlayModeで解決される");

    // 在庫の消費と返却を見るため無料設置は切る
    // Disable free placement to observe consumption and refunds
    await p.SetupDebugEnvironment(new PlaytestEnvironmentConfig { FreeBlockPlacement = false, SpawnPosition = new Vector3(8f, 33.5f, -6f) });
    await p.SkipOpeningSkit();

    // GameViewへプレイフォーカスを当て直す（CEF起動等で外れるとInputSystemがキーボード・マウスを無効化し注入が届かない）
    // Re-give the Game view play focus (losing it, e.g. to CEF startup, disables InputSystem devices and drops injection)
    foreach (var windowType in new[] { "SceneHierarchyWindow", "GameView" }) { Resources.FindObjectsOfTypeAll<UnityEditor.EditorWindow>().First(w => w.GetType().Name == windowType).Focus(); await UniTask.DelayFrame(5); }
    foreach (var device in InputSystem.devices) if (!device.enabled) InputSystem.EnableDevice(device);
    await p.Until(() => Application.isFocused && Keyboard.current.enabled, 10f, "環境: GameViewがプレイフォーカスを持ち入力デバイスが有効");
    p.Hotbar.UnlockConnectTool("レール");
    await p.PrepareBlockForUiPlacement("レール橋脚", 5);
    foreach (var item in new[] { "補強棒材", "鉄板" }) await p.GiveItem(item, 64);
    var railTool = ToolGuid("レール");
    var pierE = new Vector3Int(14, 32, 6); var pierF = new Vector3Int(14, 32, 14);

    // 橋脚は直置き不可（生成パラメータ必須）のためホットバーUIで置き、レールもクリック結線で引く
    // Piers cannot be direct-placed (they need create params), so place them via the hotbar UI and click-connect the rail
    await p.Hotbar.AssignHotbar(0, "レール橋脚");
    await p.Hotbar.AssignHotbar(1, "レール");
    foreach (var pos in new[] { pierE, pierF }) await PlacePierViaUi(pos);
    await p.Hotbar.EnterBuildMode(1);
    await p.AimAt(RailAreaCenter(pierE, pierF)); await p.ClickPlace(); await p.WaitSeconds(0.3f);
    await p.AimAt(RailAreaCenter(pierF, pierE)); await p.ClickPlace();
    await p.Until(() => RailSignature().Contains("->"), 15f, "準備: 橋脚E-Fがレールで繋がる");
    await p.Hotbar.ExitBuildMode(1);

    // Step 1: 橋脚Eを撤去するとレールも消えて素材が返る。撤去前に同じ経路でレールの撤去記録を作っておく（Step 2bの切り分け用）
    // Step 1: removing pier E drops its rail with a refund; build the rail removal record beforehand via the same path (for Step 2b)
    await WarpSouthOf(pierE);
    HideMapRocks();
    await p.Screenshot("01-prepared");
    await p.PressKey(Key.G);
    await p.WaitUiState(UIStateEnum.DeleteBar, 10f);
    var railBefore = RailSignature();
    p.Assert(railBefore.Contains(railTool.ToString()), $"Step1: 撤去前のレールはレールツールの種類 {railBefore}");
    var railCache = resolver.Resolve<Client.Game.InGame.Train.RailGraph.RailGraphClientCache>(); var edges = new List<(int, int)>();
    AttachedRailEdgeEnumerator.Collect(railCache, pierE, edges);
    var recordedRail = RemovedRail.Create(railCache, edges[0].Item1, edges[0].Item2).Rail;
    await AimAtBlock(pierE);
    await p.WaitSeconds(0.5f);
    await p.Screenshot("02-hover-pier-rail-red");
    var plateBefore = p.CountItem("鉄板"); var rodBefore = p.CountItem("補強棒材");
    SemanticInput.MouseButtonDown(0); await UniTask.DelayFrame(10); SemanticInput.MouseButtonUp(0);
    await p.Until(() => p.GetBlock(pierE) == null && plateBefore < p.CountItem("鉄板") && rodBefore < p.CountItem("補強棒材"), 15f, "Step1: 橋脚Eが撤去されレール素材が返却される");
    p.Assert(!RailOf(pierF).FrontNode.ConnectedNodes.Any() && !RailOf(pierF).BackNode.ConnectedNodes.Any(), "Step1: 橋脚Fからレールが消える");
    await p.Until(() => !HasClientBlock(pierE), 10f, "Step1: クライアントからも橋脚Eが消える");

    // Step 2: Ctrl+Zで橋脚とレールが同じ種類・同じ往復2辺で戻る
    // Step 2: Ctrl+Z restores the pier and the rail with the same type and both directed edges
    await PressCtrlZ();
    var deadline = Time.realtimeSinceStartup + 20f;
    while (Time.realtimeSinceStartup < deadline && RailSignature() != railBefore) await UniTask.Yield();
    p.Assert(p.GetBlock(pierE) != null, "Step2: Ctrl+Zで橋脚Eが再設置される");
    p.Assert(RailSignature() == railBefore, $"Step2: Ctrl+Zでレールが撤去前と同じ種類・往復2辺で戻る 前={railBefore} 後={RailSignature()}");
    await p.Screenshot("03-after-pier-undo");

    // Step 2b（Undo失敗時の切り分け）: 橋脚Eを手で置き直し、撤去前に作った同じレール記録を本番送信器で張り直して向きを検証する
    // Step 2b (isolation when undo fails): re-place pier E by hand and resend the same rail record via the production sender to verify orientation
    if (p.GetBlock(pierE) == null)
    {
        p.Note("Step 2b: 橋脚Eを手で置き直し、撤去記録のレール区間をRailConnectByDestinationで張り直す");
        await p.PressKey(Key.G);
        await p.WaitUiState(UIStateEnum.GameScreen, 10f);
        await PlacePierViaUi(pierE);
        recordedRail.SendConnectionRestore(restoreSender);
        await p.Until(() => RailSignature().Contains("->"), 15f, "Step2b: 記録区間の張り直しでレールが戻る");
        p.Assert(RailSignature() == railBefore, $"Step2b: 記録区間から撤去前と同じ種類・往復2辺で戻る 前={railBefore} 後={RailSignature()}");
        await p.Screenshot("04-rail-restored-by-record");
    }
    await p.WaitSeconds(1f);
    p.Assert(denied.Count == 0 && badLogs.Count == 0, $"全区間: 拒否通知・復元失敗・例外ログが無い: {string.Join(" | ", denied.Concat(badLogs))}");

    #region Internal

    Guid ToolGuid(string toolName) => Core.Master.MasterHolder.ConnectToolMaster.All.First(t => t.Name == toolName).ConnectToolGuid;
    Game.Block.Blocks.TrainRail.RailComponent RailOf(Vector3Int pos) => p.GetBlock(pos)?.GetComponent<Game.Block.Blocks.TrainRail.RailComponent>();
    bool HasClientBlock(Vector3Int pos) => ClientDIContext.BlockGameObjectDataStore.TryGetBlockGameObject(pos, out _);

    // 橋脚E・Fのノード間の有向辺と区間種類を並べた署名（往復2辺と向きの比較用）
    // Signature of directed edges and segment types between pier E/F nodes (compares both directions and orientation)
    string RailSignature()
    {
        var e = RailOf(pierE); var f = RailOf(pierF); var datastore = p.ServerService<Game.Train.RailGraph.RailGraphDatastore>();
        if (e == null || f == null) return "missing";
        var nodes = new[] { (e.FrontNode, "E.Front"), (e.BackNode, "E.Back"), (f.FrontNode, "F.Front"), (f.BackNode, "F.Back") };
        return string.Join(",", from a in nodes from b in nodes where a.Item1.ConnectedNodes.Any(n => n.NodeGuid == b.Item1.Guid) select $"{a.Item2}->{b.Item2}:{(datastore.TryGetRailSegmentType(a.Item1.NodeId, b.Item1.NodeId, out var type) ? type : Guid.Empty)}");
    }

    // 生成マップの岩（MapObject）が足場を突き抜けて照準と撮影を遮るため、作業範囲の岩を表示・当たりごと隠す
    // Generated-map rocks (MapObject) poke through the scaffold and block aim and shots, so hide them (render and collision) in the work area
    void HideMapRocks()
    {
        var area = new Bounds(new Vector3(8f, 36f, 4f), new Vector3(60f, 30f, 60f)); var layer = LayerMask.NameToLayer("MapObject");
        foreach (var c in UnityEngine.Object.FindObjectsByType<Collider>(FindObjectsSortMode.None)) if (c.gameObject.layer == layer && c.bounds.Intersects(area)) c.enabled = false;
        foreach (var r in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None)) if (r.gameObject.layer == layer && r.bounds.Intersects(area)) r.enabled = false;
    }

    // 遠方の設置照準は岩に遮られるため、対象の手前へ寄ってから操作する
    // Distant placement aim gets blocked by rocks, so step in front of the target before operating
    async UniTask WarpSouthOf(Vector3Int pos) { p.WarpPlayer(new Vector3(pos.x + 1.5f, 33.5f, pos.z - 5f)); await p.WaitSeconds(0.5f); }
    async UniTask PlacePierViaUi(Vector3Int pos)
    {
        await p.Hotbar.EnterBuildMode(0); await WarpSouthOf(pos);
        await p.AimAtPlaceOrigin("レール橋脚", pos); await p.ClickPlace();
        await p.Until(() => p.GetBlock(pos) != null, 15f, $"橋脚をUIで設置 {pos}"); await p.WaitBlockGameObject(pos);
        await p.Hotbar.ExitBuildMode(0);
    }
    Vector3 RailAreaCenter(Vector3Int selfPos, Vector3Int otherPos) => ClientDIContext.BlockGameObjectDataStore.GetBlockGameObject(selfPos).GetComponentsInChildren<Client.Game.InGame.BlockSystem.PlaceSystem.TrainRailConnect.TrainRailConnectAreaCollider>(true).Select(a => a.GetComponent<Collider>().bounds.center).OrderBy(c => Vector3.Distance(c, otherPos)).First();

    // 照準候補（ブロックの各コライダー中心）のうち、削除ツールの実レイキャストが当該対象を返す点を照準する
    // Aim at the candidate (block collider centers) where the real delete-tool raycast returns that target
    async UniTask AimAtBlock(Vector3Int pos)
    {
        var block = ClientDIContext.BlockGameObjectDataStore.GetBlockGameObject(pos);
        await AimUntil(block.GetComponentsInChildren<Collider>().Where(c => c.gameObject.layer != Client.Common.LayerConst.ConnectionLineLayer).Select(c => c.bounds.center).OrderBy(c => Vector3.Distance(c, Camera.main.transform.position)), block);
    }
    async UniTask AimUntil(IEnumerable<Vector3> points, object expected)
    {
        foreach (var point in points.ToList())
        {
            await p.AimAt(point);
            var target = Client.Game.InGame.Control.DeleteTargetRaycaster.AimAt(DeleteAimFilter.Frontmost).Target;
            if (ReferenceEquals(target, expected) || target is Client.Game.InGame.Block.BlockGameObjectChild child && ReferenceEquals(child.BlockGameObject, expected)) return;
        }
        throw new InvalidOperationException($"照準候補のどれも対象に当たらない: {expected}");
    }
    // build-undo-ctrl-z.cs と同じ注入順（LeftCtrl保持中にZ）
    // Same injection order as build-undo-ctrl-z.cs (Z while LeftCtrl is held)
    async UniTask PressCtrlZ()
    {
        p.Note("Ctrl+Z注入");
        foreach (var (key, down) in new[] { (Key.LeftCtrl, true), (Key.Z, true), (Key.Z, false), (Key.LeftCtrl, false) }) { if (down) SemanticInput.KeyDown(key); else SemanticInput.KeyUp(key); await UniTask.DelayFrame(3); }
        await p.WaitSeconds(0.5f);
    }

    #endregion
});
