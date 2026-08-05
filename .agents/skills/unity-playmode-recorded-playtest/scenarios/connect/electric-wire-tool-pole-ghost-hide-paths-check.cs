// 電線ツールの電柱ゴースト消灯経路チェック: ゴースト表示を修正した後、既存の消灯箇所が点きっぱなしにならないかを確認する
// 検証項目: 空間照準でゴーストが点く→起点選択後に接続先候補へ照準するとConnectToTarget経路で消える→ツール解除で消える
// Hide-path check for the wire tool's pole ghost after fixing its visibility, verifying existing hide sites still turn it off.
// Verifies: ghost lights over empty space -> goes dark when hovering a connectable target with an origin selected (ConnectToTarget path) -> goes dark when the tool is deselected.
using System;
using System.Linq;
using System.Reflection;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem;
using Client.Game.InGame.BlockSystem.PlaceSystem.ElectricWireConnect;
using Client.Game.InGame.BlockSystem.PlaceSystem.ElectricWireConnect.Parts;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.UIState;
using Client.Playtest;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using Game.UnlockState;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;

var options = new PlaytestRunOptions { Record = true };
return PlaytestRunner.Run("electric-wire-tool-pole-ghost-hide-paths-check", options, async p =>
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
    await p.PrepareBlockForUiPlacement("電柱", 3);
    p.ServerService<IGameUnlockStateDataController>().UnlockConnectTool(wireToolGuid);
    await p.GiveItem("銅のワイヤー", 64);

    // 接続先候補として既存電柱を1本、直設置しておく
    // Pre-place one pole directly to serve as a connectable hover target
    var targetPolePos = new Vector3Int(8, 32, 10);
    p.PlaceBlockDirect("電柱", targetPolePos, BlockDirection.North);
    var targetPoleGo = await p.WaitBlockGameObject(targetPolePos);
    var targetCollider = targetPoleGo.GetComponentsInChildren<Collider>(true).First(c => c.name == "ClickCollider");

    var resolver = ClientDIContext.DIContainer.DIContainerResolver;
    var wireSystem = resolver.Resolve<ElectricWireConnectSystem>();
    var placeController = resolver.Resolve<PlaceSystemStateController>();
    var contextField = typeof(ElectricWireConnectSystem).GetField("_context", BindingFlags.NonPublic | BindingFlags.Instance);
    var toolContext = (ElectricWireToolContext)contextField.GetValue(wireSystem);

    p.Note("電線接続ツールを選ぶ");
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

    // ===== 1. 空間照準ではゴーストが点く（修正の再確認） =====
    // ===== 1. Aiming at empty space lights the ghost (re-confirm the fix) =====
    p.Note("空間へ照準してゴーストが点くことを確認する");
    await p.AimAt(new Vector3(8.5f, 32f, 2.5f));
    await UniTask.DelayFrame(10);
    p.Assert(toolContext.PreviewBlockController.IsActive, "空間照準でゴーストが点く");
    await p.Screenshot("01-empty-space-ghost-on");

    // ===== 2. 接続先候補ブロックへ照準すると起点未選択でも一旦電柱として選択できる。起点選択後、別ブロックへ照準するとConnectToTarget経路でゴーストが消える =====
    // ===== 2. Selecting an origin, then aiming at another connectable block enters ConnectToTarget and turns the ghost off =====
    p.Note("接続先候補ブロックをクリックして起点に選ぶ");
    await p.AimAt(targetCollider.bounds.center);
    await UniTask.DelayFrame(5);
    await p.ClickPlace();
    await p.WaitSeconds(0.3f);

    var secondPolePos = new Vector3Int(8, 32, 4);
    p.PlaceBlockDirect("電柱", secondPolePos, BlockDirection.North);
    var secondPoleGo = await p.WaitBlockGameObject(secondPolePos);
    var secondCollider = secondPoleGo.GetComponentsInChildren<Collider>(true).First(c => c.name == "ClickCollider");

    p.Note("起点選択中に別の接続先候補へ照準し、ConnectToTarget経路でゴーストが消えることを確認する");
    await p.AimAt(secondCollider.bounds.center);
    await UniTask.DelayFrame(10);
    p.Assert(!toolContext.PreviewBlockController.IsActive, "起点選択中に接続先候補へ照準するとゴーストが消える（ConnectToTarget経路）");
    await p.Screenshot("02-connect-target-hover-ghost-off");

    // ===== 3. ツールを解除するとゴーストが消える =====
    // ===== 3. Deselecting the tool turns the ghost off =====
    p.Note("ツールを解除してゴーストが消えることを確認する");
    await p.ExitToGameScreen();
    await UniTask.DelayFrame(5);
    p.Assert(!toolContext.PreviewBlockController.IsActive, "ツール解除でゴーストが消える");
    await p.Screenshot("03-tool-deselected-ghost-off");
});
