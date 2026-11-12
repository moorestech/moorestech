using Game.Context;
// 電線ツールの電柱ゴースト可視性の回帰チェック: 選択中の電柱種のゴーストが描画されることを通常設置と比較して確認する
// ゴースト評価は通るのにプレビューGameObjectがSetActive(true)されない不具合の再発を防ぐ
// 電柱名のゴーストラベルは削除され（437dee26f3）後継のカーソルツールチップにも電柱名の行が無いため、評価の成否は選択中の電柱名で見る
// Regression check that the wire tool draws the selected pole-type ghost, compared against normal placement.
// Guards against the defect where the ghost evaluated but the preview GameObject was never activated.
// The pole-name ghost label was removed (437dee26f3) and the cursor tooltip has no pole-name line, so evaluation is read via the selected pole name.
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
using Client.Playtest.Input;
using Client.Playtest.Operations;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using Game.UnlockState;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;

var options = new PlaytestRunOptions { Record = true };
return PlaytestRunner.Run("electric-wire-tool-pole-ghost-visibility-check", options, async p =>
{
    // 背景EditorではInputSystemがデバイスを無効化し注入が届かないため、焦点無視へ切り替え再有効化する
    // A background Editor disables keyboard/mouse so injection never lands; switch to ignore-focus and re-enable
    InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
    InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
    SemanticInput.EnsureDevices();
    InputSystem.EnableDevice(Keyboard.current);
    InputSystem.EnableDevice(Mouse.current);
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
    await p.RunOnServer("電線ツールを解放する", () => ServerContext.GetService<IGameUnlockStateDataController>().UnlockConnectTool(wireToolGuid));
    await p.GiveItem("銅のワイヤー", 64);

    var resolver = ClientDIContext.DIContainer.DIContainerResolver;
    var wireSystem = resolver.Resolve<ElectricWireConnectSystem>();
    var placeController = resolver.Resolve<PlaceSystemStateController>();
    var contextField = typeof(ElectricWireConnectSystem).GetField("_context", BindingFlags.NonPublic | BindingFlags.Instance);
    var toolContext = (ElectricWireToolContext)contextField.GetValue(wireSystem);
    // 選択中の電柱名はプロダクションと同じ公開APIから引く
    // Read the selected pole name through the same public API production uses
    string SelectedPoleName() => toolContext.PoleSelection.TryGetSelectedPole(out _, out var selectedMaster) ? selectedMaster.Name : null;

    // ===== 電線ツール側 =====
    // ===== Wire tool side =====
    p.Note("電線接続ツールを選び、空間へ照準して電柱ゴーストが出るかを見る");
    for (var attempt = 0; attempt < 3 && p.CurrentUiState != UIStateEnum.BuildMenu; attempt++)
    {
        await p.PressKey(p.CurrentUiState == UIStateEnum.PlaceBlock ? Key.Tab : Key.B);
        var openDeadline = Time.realtimeSinceStartup + 4f;
        while (Time.realtimeSinceStartup < openDeadline && p.CurrentUiState != UIStateEnum.BuildMenu) await UniTask.DelayFrame(5);
    }
    await p.UntilUiElement("build-menu-panel", 15f);
    await p.ClickUi("build-menu-category-d1000000-0000-4000-8000-000000000009");
    await p.ClickUi($"build-menu-entry-connectTool-{wireToolGuid:D}");
    await p.WaitUiState(UIStateEnum.PlaceBlock, 15f);
    p.Assert((placeController.CurrentTarget as ConnectToolPlacementTarget)?.ConnectToolGuid == wireToolGuid, "電線接続ツールが選択される");

    var aimPoint = new Vector3(8.5f, 32f, 6.5f);
    await p.AimAt(aimPoint);
    await UniTask.DelayFrame(10);

    // ゴースト評価の前提: 解放済みの電柱が選ばれている
    // Premise of the ghost evaluation: an unlocked pole is selected
    var poleName = SelectedPoleName();
    p.Assert(poleName == "電柱", $"電線ツール: 解放済みの電柱が選択されている 実際:'{poleName}'");

    // プレビューGameObjectも有効化され、電柱ゴーストが実際に描画される
    // The preview GameObject is activated as well, so the pole ghost is actually drawn
    var wireToolPreviewActive = toolContext.PreviewBlockController.IsActive;
    var hasGhostObject = toolContext.PreviewBlockController.TryGetPreviewBlock(0, out var ghost);
    p.Note($"電線ツール: PreviewBlockController.IsActive={wireToolPreviewActive} / ゴーストオブジェクト取得={hasGhostObject} / ゴーストのactiveInHierarchy={(hasGhostObject ? ghost.gameObject.activeInHierarchy.ToString() : "n/a")}");
    p.Assert(wireToolPreviewActive, "電線ツールでもプレビューGameObjectが有効になり電柱ゴーストが描画される");
    p.Assert(hasGhostObject && ghost.gameObject.activeInHierarchy, "電線ツールの電柱ゴーストが実体として存在し可視である");
    await p.Screenshot("01-wire-tool-ghost-drawn");

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
