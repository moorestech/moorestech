// 電線ツールの電柱ゴースト可視性チェック: 選択中の電柱種のゴーストが実際に描画されるかを通常設置と比較する
// 観測: 電線ツールでは名前ラベルは出るがプレビューGameObjectがSetActive(true)されず不可視。通常設置では可視（対照）
// Pole-ghost visibility check for the wire tool, compared against normal placement.
// Observation: under the wire tool the name label shows but the preview GameObject is never activated, so no ghost is drawn; normal placement draws it (control).
using System;
using System.Linq;
using System.Reflection;
using Client.Game.InGame.BlockSystem.PlaceSystem;
using Client.Game.InGame.BlockSystem.PlaceSystem.ElectricWireConnect;
using Client.Game.InGame.BlockSystem.PlaceSystem.ElectricWireConnect.Parts;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.UIState;
using Client.Playtest;
using Client.Playtest.Operations;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using Game.UnlockState;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;

var options = new PlaytestRunOptions { Record = true };
return PlaytestRunner.Run("electric-wire-tool-pole-ghost-visibility-check", options, async p =>
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

    var resolver = ClientDIContext.DIContainer.DIContainerResolver;
    var wireSystem = resolver.Resolve<ElectricWireConnectSystem>();
    var placeController = resolver.Resolve<PlaceSystemStateController>();
    var contextField = typeof(ElectricWireConnectSystem).GetField("_context", BindingFlags.NonPublic | BindingFlags.Instance);
    var toolContext = (ElectricWireToolContext)contextField.GetValue(wireSystem);
    TextMeshPro PoleNameLabel() => UnityEngine.Object
        .FindObjectsByType<TextMeshPro>(FindObjectsInactive.Include, FindObjectsSortMode.None)
        .FirstOrDefault(t => t.gameObject.name == "ElectricWirePoleNameLabel");

    // ===== 電線ツール側 =====
    // ===== Wire tool side =====
    p.Note("電線接続ツールを選び、空間へ照準して電柱ゴーストが出るかを見る");
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

    var aimPoint = new Vector3(8.5f, 32f, 6.5f);
    await p.AimAt(aimPoint);
    await UniTask.DelayFrame(10);

    // ゴースト評価は成功している（名前ラベルが選択中の電柱名で表示される）
    // The ghost evaluation succeeds: the name label shows the selected pole name
    var label = PoleNameLabel();
    p.Assert(label != null && label.gameObject.activeInHierarchy, "電線ツール: 電柱名ラベルは表示される（ゴースト評価自体は成功している）");
    p.Note($"電線ツールの名前ラベル='{label?.text}'");

    // しかしプレビューGameObjectは非アクティブのまま＝ゴーストが描画されない
    // But the preview GameObject stays inactive, so no ghost is drawn
    var wireToolPreviewActive = toolContext.PreviewBlockController.IsActive;
    var hasGhostObject = toolContext.PreviewBlockController.TryGetPreviewBlock(0, out var ghost);
    p.Note($"電線ツール: PreviewBlockController.IsActive={wireToolPreviewActive} / ゴーストオブジェクト取得={hasGhostObject} / ゴーストのactiveInHierarchy={(hasGhostObject ? ghost.gameObject.activeInHierarchy.ToString() : "n/a")}");
    p.Assert(!wireToolPreviewActive, "【観測された不具合】電線ツールではプレビューGameObjectがSetActive(true)されず電柱ゴーストが描画されない");
    await p.Screenshot("01-wire-tool-ghost-not-drawn");

    // ===== 対照: 通常設置では同じ位置にゴーストが描画される =====
    // ===== Control: normal placement draws the ghost at the same position =====
    p.Note("対照実験: 通常設置(ビルドメニューで電柱を選択)では同じ位置にゴーストが描画される");
    await p.ExitToGameScreen();
    await p.OpenBuildMenuAndSelectBlock("電柱");
    await p.AimAt(aimPoint);
    await UniTask.DelayFrame(10);
    p.Note($"通常設置: PreviewBlockController.IsActive={toolContext.PreviewBlockController.IsActive}");
    p.Assert(toolContext.PreviewBlockController.IsActive, "対照: 通常設置ではプレビューGameObjectが有効になりゴーストが描画される");
    await p.Screenshot("02-normal-place-ghost-drawn");
});
