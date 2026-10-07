// 削除ツールで電線・歯車チェーンを切断し、ブロック撤去に巻き込まれた電線・チェーンもCtrl+Zで同じ種類のまま復元されることの録画検証（レールは train/delete-tool-pier-removal-undo-rail.cs）
// 記録フックは DragDeleteSelection.CommitDelete と BuildUndoService（Ctrl+Z）にあるため、操作は必ず削除ツールUIへの注入で行う
// Recorded check: the delete tool cuts wires/chains, and Ctrl+Z restores cut and cascaded wires/chains with the same tool kind (rails: train/delete-tool-pier-removal-undo-rail.cs)
// Recording hooks live in DragDeleteSelection.CommitDelete and BuildUndoService (Ctrl+Z), so every operation is injected into the delete-tool UI
using System;
using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.BlockSystem.PlaceSystem.Undo.Removal;
using Client.Game.InGame.BlockSystem.StateProcessor.ConnectionLine;
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

return PlaytestRunner.Run("delete-tool-cut-and-undo-connection-lines", new PlaytestRunOptions { Record = true }, async p =>
{
    // 拒否・復元失敗・例外のログ（サーバー処理は別スレッドのためThreaded）と拒否通知（サーバー発・クライアント発）を集める
    // Collect refusal/restore-failure/exception logs (Threaded, as server handling runs off the main thread) and denied notifications
    var badLogs = new List<string>(); var denied = new List<string>();
    var badTags = new[] { "denied.", "[RemovalRestore]", "[ConnectionLineDelete]", "[RemovalPreview]", "[RailConnectByDestination] endpoint", "[RailConnectByDestination] connection denied" };
    Application.logMessageReceivedThreaded += (condition, _, type) => { if (!condition.StartsWith("[Playtest]") && (type == LogType.Exception || type == LogType.Error || type == LogType.Warning && badTags.Any(condition.Contains))) lock (badLogs) badLogs.Add($"{type}: {condition.Split('\n')[0]}"); };
    ClientContext.VanillaApi.Event.SubscribeEventResponse(NotificationService.EventTag, payload => { var m = MessagePack.MessagePackSerializer.Deserialize<NotificationMessagePack>(payload); if (m.Category == NotificationCategory.OperationDenied) denied.Add(m.MessageId); });
    ClientDIContext.ClientLocalNotificationSource.OnNotification.Subscribe(m => { if (m.Category == NotificationCategory.OperationDenied) denied.Add(m.MessageId); });
    var restoreSender = ClientDIContext.DIContainer.DIContainerResolver.Resolve<IRemovalRestoreSender>();
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
    foreach (var tool in new[] { "電線", "歯車チェーン" }) p.Hotbar.UnlockConnectTool(tool);
    foreach (var block in new[] { "電柱", "歯車チェーンポール" }) await p.PrepareBlockForUiPlacement(block, 5);
    foreach (var item in new[] { "銅のワイヤー", "鉄のワイヤー" }) await p.GiveItem(item, 64);
    var wireTool = ToolGuid("電線"); var chainTool = ToolGuid("歯車チェーン");

    // 電柱・チェーンポールは自動接続の走らないサーバー直置き。poleNearはAの範囲内だが繋がない（自動接続抑止の検出用）
    // Poles go server-side so no auto-connect runs; poleNear is within A's range but left unwired (detects auto-connect suppression)
    var poleA = new Vector3Int(5, 32, 4); var poleB = new Vector3Int(11, 32, 4); var poleNear = new Vector3Int(5, 32, 0);
    var chainC = new Vector3Int(5, 32, 9); var chainD = new Vector3Int(9, 32, 9);
    foreach (var pos in new[] { poleA, poleB, poleNear }) p.PlaceBlockDirect("電柱", pos, BlockDirection.North);
    foreach (var pos in new[] { chainC, chainD }) p.PlaceBlockDirect("歯車チェーンポール", pos, BlockDirection.North);
    foreach (var pos in new[] { poleA, poleB, poleNear, chainC, chainD }) await p.WaitBlockGameObject(pos);
    var playerId = ClientContext.PlayerConnectionSetting.PlayerId;
    p.Assert(Server.Protocol.PacketResponse.Util.ElectricWire.Connection.ElectricWireSystemUtil.TryConnect(poleA, poleB, playerId, wireTool, out var wireFail), $"準備: A-B を電線で接続 {wireFail}");
    p.Assert(Server.Protocol.PacketResponse.Util.GearChain.GearChainSystemUtil.TryConnect(chainC, chainD, playerId, chainTool, out var chainFail), $"準備: C-D をチェーンで接続 {chainFail}");

    await p.Until(() => LinesOf(poleA).Count == 1 && LinesOf(chainC).Count == 1, 15f, "準備: クライアントに電線・チェーンが出る");
    HideMapRocks();
    await p.Screenshot("01-prepared");

    // Step 1: 電柱・チェーンポールのホバーで付随線が赤くなるのを撮り、電線だけを切る
    // Step 1: capture the cascade red preview when hovering pole / chain pole, then cut only the wire
    await p.PressKey(Key.G);
    await p.WaitUiState(UIStateEnum.DeleteBar, 10f);
    p.Note("Step 1: 電柱Aをホバー→付随する電線が赤くなる");
    await AimAtBlock(poleA);
    await p.Until(() => IsRed(LinesOf(poleA)[0]), 5f, "Step1: 電柱Aホバーで電線が赤表示");
    await p.Screenshot("02-hover-pole-wire-red");
    p.Note("Step 1: チェーンポールCをホバー→付随するチェーンが赤くなる（電線は戻る）");
    await AimAtBlock(chainC);
    await p.Until(() => IsRed(LinesOf(chainC)[0]) && !IsRed(LinesOf(poleA)[0]), 5f, "Step1: チェーンポールCホバーでチェーンが赤表示・電線は元へ戻る");
    await p.Screenshot("03-hover-chainpole-chain-red");
    var copperBefore = p.CountItem("銅のワイヤー");
    await ClickLine(LinesOf(poleA)[0]);
    await p.Until(() => !WireConnected(poleA, poleB) && copperBefore < p.CountItem("銅のワイヤー"), 15f, "Step1: A-B電線が切れて銅のワイヤーが返却される");
    p.Assert(p.GetBlock(poleA) != null && p.GetBlock(poleB) != null, "Step1: 電柱は消えない（線だけ切れる）");
    await p.Screenshot("04-wire-cut");

    // Step 2: Ctrl+Zで電線が同じ種類で戻る。続けてチェーンも切ってCtrl+Zで戻す
    // Step 2: Ctrl+Z restores the wire with the same kind; then cut the chain and restore it the same way
    await PressCtrlZ();
    await p.Until(() => WireConnected(poleA, poleB) && WireToolOf(poleA, poleB) == wireTool, 15f, "Step2: A-B電線が同じ電線ツールで復元される");
    await p.Until(() => LinesOf(chainC).Count == 1 && LinesOf(poleA).Count == 1, 10f, "Step2: クライアントに電線・チェーン表示がある");
    var ironWireBefore = p.CountItem("鉄のワイヤー");
    await ClickLine(LinesOf(chainC)[0]);
    await p.Until(() => !ChainConnected(chainC, chainD) && ironWireBefore < p.CountItem("鉄のワイヤー"), 15f, "Step2: C-Dチェーンが切れて鉄のワイヤーが返却される");
    await p.Screenshot("05-chain-cut");
    await PressCtrlZ();
    await p.Until(() => ChainConnected(chainC, chainD) && ChainToolOf(chainC, chainD) == chainTool, 15f, "Step2: C-Dチェーンが同じ歯車チェーンで復元される");
    await p.Until(() => LinesOf(poleA).Count == 1 && LinesOf(chainC).Count == 1, 15f, "Step2: クライアントに電線・チェーンが戻る");

    // Step 3: 電柱A→チェーンポールCを1ドラッグで撤去（付いていた線も消える）
    // Step 3: remove pole A and chain pole C in one drag (their lines vanish with them)
    p.Note("Step 3: 電柱A→チェーンポールCをドラッグして撤去");
    var chainCIdBefore = ChainPole(chainC).BlockInstanceId;
    await AimAtBlock(poleA);
    SemanticInput.MouseButtonDown(0); await UniTask.DelayFrame(10);
    await AimAtBlock(chainC);
    await UniTask.DelayFrame(10); SemanticInput.MouseButtonUp(0);
    await p.Until(() => p.GetBlock(poleA) == null && p.GetBlock(chainC) == null, 15f, "Step3: AとCが撤去される");
    p.Assert(Connector(poleB).WireConnections.Count == 0 && !ChainPole(chainD).ContainsChainConnection(chainCIdBefore), "Step3: 付いていた電線・チェーンも消える");
    await p.Until(() => !HasClientBlock(poleA) && !HasClientBlock(chainC), 10f, "Step3: クライアントからもA・Cが消える");
    await p.Screenshot("06-blocks-removed");

    // Step 4: Ctrl+Zでブロックと線を撤去前どおりに戻す（自動接続は走らない）
    // Step 4: Ctrl+Z restores blocks and lines exactly (no auto-connect)
    await PressCtrlZ();
    await p.Until(() => WireConnected(poleA, poleB) && ChainConnected(chainC, chainD), 20f, "Step4: A-B電線とC-Dチェーンが戻る");
    p.Assert(WireToolOf(poleA, poleB) == wireTool && ChainToolOf(chainC, chainD) == chainTool, "Step4: 電線・チェーンの種類が保たれる");
    p.Assert(!WireConnected(poleA, poleNear) && Connector(poleA).WireConnections.Count == 1, "Step4: 範囲内の電柱へ自動接続されずAの接続は1本だけ");
    await p.Until(() => LinesOf(poleA).Count == 1 && LinesOf(chainC).Count == 1, 15f, "Step4: クライアント表示も戻る");
    await p.Screenshot("07-blocks-and-lines-restored");
    await p.WaitSeconds(1f);
    p.Assert(denied.Count == 0 && badLogs.Count == 0, $"全区間: 拒否通知・復元失敗・例外ログが無い: {string.Join(" | ", denied.Concat(badLogs))}");

    #region Internal

    Guid ToolGuid(string toolName) => Core.Master.MasterHolder.ConnectToolMaster.All.First(t => t.Name == toolName).ConnectToolGuid;
    bool HasClientBlock(Vector3Int pos) => ClientDIContext.BlockGameObjectDataStore.TryGetBlockGameObject(pos, out _);
    List<ConnectionLineDeleteTarget> LinesOf(Vector3Int pos) => HasClientBlock(pos) ? ClientDIContext.ConnectionLineRegistry.GetLinesAttachedTo(ClientDIContext.BlockGameObjectDataStore.GetBlockGameObject(pos).BlockInstanceId).Where(l => l != null).ToList() : new List<ConnectionLineDeleteTarget>();
    Game.EnergySystem.IElectricWireConnector Connector(Vector3Int pos) => p.GetBlock(pos)?.GetComponent<Game.EnergySystem.IElectricWireConnector>();
    bool WireConnected(Vector3Int a, Vector3Int b) => Connector(a) != null && Connector(b) != null && Connector(a).ContainsWireConnection(Connector(b).BlockInstanceId);
    Guid WireToolOf(Vector3Int a, Vector3Int b) => Connector(a).WireConnections[Connector(b).BlockInstanceId].Record.ConnectToolGuid;
    Game.Block.Interface.Component.IGearChainPole ChainPole(Vector3Int pos) => p.GetBlock(pos)?.GetComponent<Game.Block.Interface.Component.IGearChainPole>();
    bool ChainConnected(Vector3Int a, Vector3Int b) => ChainPole(a) != null && ChainPole(b) != null && ChainPole(a).ContainsChainConnection(ChainPole(b).BlockInstanceId);
    Guid ChainToolOf(Vector3Int a, Vector3Int b) => ChainPole(a).TryGetChainConnectionRecord(ChainPole(b).BlockInstanceId, out var record) ? record.ConnectToolGuid : Guid.Empty;
    bool IsRed(ConnectionLineDeleteTarget line) => line != null && line.GetComponentsInChildren<Renderer>().Any(r => r.sharedMaterial != null && r.sharedMaterial.HasProperty("_PreviewColor") && r.sharedMaterial.GetColor("_PreviewColor") == Client.Common.MaterialConst.NotPlaceableColor);

    // 生成マップの岩（MapObject）が足場を突き抜けて照準と撮影を遮るため、作業範囲の岩を表示・当たりごと隠す
    // Generated-map rocks (MapObject) poke through the scaffold and block aim and shots, so hide them (render and collision) in the work area
    void HideMapRocks()
    {
        var area = new Bounds(new Vector3(8f, 36f, 4f), new Vector3(60f, 30f, 60f)); var layer = LayerMask.NameToLayer("MapObject");
        foreach (var c in UnityEngine.Object.FindObjectsByType<Collider>(FindObjectsSortMode.None)) if (c.gameObject.layer == layer && c.bounds.Intersects(area)) c.enabled = false;
        foreach (var r in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None)) if (r.gameObject.layer == layer && r.bounds.Intersects(area)) r.enabled = false;
    }

    // 照準候補（ブロックの各コライダー中心・線のカプセル列）のうち、削除ツールの実レイキャストが当該対象を返す点を照準する
    // Aim at the candidate (block collider centers / line capsules) where the real delete-tool raycast returns that target
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
    async UniTask ClickLine(ConnectionLineDeleteTarget line)
    {
        var capsules = line.GetComponentsInChildren<CapsuleCollider>();
        await AimUntil(capsules.OrderBy(c => Math.Abs(Array.IndexOf(capsules, c) - capsules.Length / 2)).Select(c => c.transform.position), line);
        SemanticInput.MouseButtonDown(0); await UniTask.DelayFrame(10); SemanticInput.MouseButtonUp(0);
        await UniTask.DelayFrame(3); await p.WaitSeconds(0.5f);
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
