// BP 右クリック削除: BP コピーで BP『delbp』を作り、ビルドメニューのエントリを右クリックしてサーバーとメニューから消えるのを見る（GameUi・Web 両系。S3-ladder）
// Blueprint right-click delete: create blueprint "delbp" by copy, right-click its build menu entry, and see it leave the server and the menu (GameUi and Web, S3-ladder)
using System;
using System.Linq;
using Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.Tooltip;
using Client.Game.InGame.UI.UIState;
using Client.Input;
using Client.Playtest;
using Client.Playtest.Input;
using Client.Playtest.Operations;
using Client.Playtest.Operations.Ui;
using Client.Playtest.UiElements;
using Client.Ui.Foundation.Root;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using VContainer;

var options = new PlaytestRunOptions { Record = true, ScenarioTimeoutSeconds = 300f };
return PlaytestRunner.Run("blueprint-right-click-delete-via-ui", options, async p =>
{
    // 1. 系は主張せず記録し、入力の準備をする（前例 uitk-build-bp）
    // 1. Record the UI system without asserting it and prepare input (precedent uitk-build-bp)
    var isGameUi = UiInputExclusivity.ActiveUiSystem == UiSystem.GameUi;
    p.Note($"全体スイッチ 系={UiInputExclusivity.ActiveUiSystem}");
    InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
    InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
    SemanticInput.EnsureDevices();
    InputSystem.EnableDevice(Keyboard.current);
    InputSystem.EnableDevice(Mouse.current);
    var resolver = ClientDIContext.DIContainer.DIContainerResolver;
    var library = resolver.Resolve<ClientBlueprintLibrary>();
    await p.SetupDebugEnvironment(new PlaytestEnvironmentConfig());
    await p.SkipOpeningSkitIfPlaying();
    await p.ExitToGameScreen();

    // 2. 木のチェストを置き、BP コピーで範囲を打って BP『delbp』を作る（前例 uitk-hotbar-and-bp-modal の手順 6）
    // 2. Place a wooden chest, mark the range with blueprint copy and create "delbp" (precedent uitk-hotbar-and-bp-modal step 6)
    await p.Hotbar.UnlockBlueprint();
    await p.PlaceBlockDirect("木のチェスト", new Vector3Int(2, 32, 2), BlockDirection.North);
    await p.WaitBlockGameObject(new Vector3Int(2, 32, 2));
    p.WarpPlayer(new Vector3(6f, 33.5f, -5f));
    var checks = new BlueprintCopyPasteScenarioChecks(p, resolver.Resolve<MouseCursorTooltipState>());
    await checks.SelectEntry("build-menu-category-d1000000-0000-4000-8000-000000000009", "build-menu-entry-blueprintCopy-88dd687d-aceb-4aeb-94e8-44be1e1f5a0d");
    await p.AimAt(new Vector3(0.5f, 32f, 0.5f));
    await p.ClickPlace();
    await p.AimAt(new Vector3(4.5f, 32f, 4.5f));
    await p.ClickPlace();
    await p.UntilUiElement("modal-input", 10f);
    await p.SetText("modal-input", "delbp");
    await p.ClickUi("modal-button");
    await p.WaitForBlueprint("delbp", true, 15f, "BP『delbp』がサーバーに登録される");
    var blueprintGuid = library.Blueprints.Single(blueprint => blueprint.Name == "delbp").BlueprintGuid;
    var entryTestId = $"build-menu-entry-blueprint-{blueprintGuid:D}";
    p.Note($"BP『delbp』の Guid={blueprintGuid:D} エントリ={entryTestId}");

    // 3. B でメニューを開き、保存済み BP のカテゴリで delbp のエントリが出るのを見る
    // 3. Open the menu with B and see the delbp entry under the saved blueprint category
    await p.ExitToGameScreen();
    await p.PressKey(Key.B);
    await p.WaitUiState(UIStateEnum.BuildMenu, 10f);
    await p.UntilUiElement("build-menu-panel", 15f);
    await p.ClickUi("build-menu-category-d1000000-0000-4000-8000-000000000010");
    await PlaytestUiElementOps.ScrollDownUntilUiElementIsClickable(entryTestId, "build-menu-scroll-area", 15f);
    await p.Screenshot("01-menu-bp-entry");

    // 4. エントリの中心で右クリックする（削除に確認の UI は無く、右の押下で即コマンドが出る）
    // 4. Right-click the entry center (no confirmation UI; the right press issues the command at once)
    var entryCenter = (await PlaytestUiElementOps.FindUi(entryTestId)).ScreenRect.center;
    SemanticInput.MouseMoveTo(entryCenter);
    await p.WaitSeconds(0.3f);
    p.Note($"右クリック: {entryTestId} @ {entryCenter}");
    SemanticInput.MouseButtonDown(1);
    await UniTask.DelayFrame(2);
    SemanticInput.MouseButtonUp(1);

    // 5. サーバーから消え、メニューからもエントリが消える
    // 5. It leaves the server and the entry leaves the menu
    await p.WaitForBlueprint("delbp", false, 15f, "BP『delbp』がサーバーから消える");
    if (isGameUi) await p.Until(() => GameUiRoot.Instance.GetRootElement().Q(entryTestId) == null, 5f, "メニューから delbp のエントリが消える（UITK のツリー）");
    else await WaitWebEntryGone();
    p.Assert(p.CurrentUiState == UIStateEnum.BuildMenu, $"削除の後もメニューは開いたまま 実際:{p.CurrentUiState}");
    await p.Screenshot("02-menu-after-delete");
    await p.ExitToGameScreen();

    #region Internal

    // Web の照会は非同期なので Until に渡さず、NotFound が返るまで照会し直す
    // The Web lookup is async, so instead of Until re-query until it answers NotFound
    async UniTask WaitWebEntryGone()
    {
        var wait = p.BeginWait(5f, "メニューから delbp のエントリが消える（Web の DOM）");
        while ((await PlaytestUiElementOps.FindUi(entryTestId)).Status != UiElementLookupStatus.NotFound)
        {
            wait.CheckTimeout("expected=NotFound, actual=still present or renderer unavailable");
            await UniTask.Yield();
        }
        wait.Complete();
    }

    #endregion
});
