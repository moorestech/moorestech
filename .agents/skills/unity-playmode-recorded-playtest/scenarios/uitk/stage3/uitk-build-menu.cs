// UITK のビルドメニュー: 末尾ジャンプ・検索・閉じて開き直す復元・選択・閉じるを全体スイッチ GameUi で通す
// UITK build menu: trailing jump, search, close-and-reopen restore, selection and close under the GameUi global switch
// 実行前に PlayMode を止め、execute-dynamic-code で UiSystemEditorSwitch.Set(UiSystem.GameUi) を立ててから run-scenario.sh を呼ぶ
// Stop PlayMode and call UiSystemEditorSwitch.Set(UiSystem.GameUi) via execute-dynamic-code before run-scenario.sh
using System;
using System.Linq;
using Client.Game.InGame.UI.UIState;
using Client.Input;
using Client.Playtest;
using Client.Playtest.Input;
using Client.Playtest.Operations;
using Client.Ui.Components.Controls;
using Client.Ui.Foundation.Layers;
using Client.Ui.Foundation.Root;
using Client.Ui.Foundation.Stage;
using Core.Master;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

var options = new PlaytestRunOptions { Record = true };
return PlaytestRunner.Run("uitk-build-menu", options, async p =>
{
    // 背景 Editor でも注入入力が届くよう焦点無視へ切り替える（前例 uitk-lifeline）
    // Ignore focus so injected input reaches a background Editor (precedent uitk-lifeline)
    InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
    InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
    SemanticInput.EnsureDevices();
    InputSystem.EnableDevice(Keyboard.current);
    InputSystem.EnableDevice(Mouse.current);
    p.Note($"全体スイッチ 系={UiInputExclusivity.ActiveUiSystem}");
    p.Assert(UiInputExclusivity.ActiveUiSystem == UiSystem.GameUi, "全体スイッチが UITK（GameUi）");

    await p.SetupDebugEnvironment(new PlaytestEnvironmentConfig());
    await p.SkipOpeningSkitIfPlaying();
    await p.ExitToGameScreen();

    // B で開き、UITK のパネルが拾えるまで待つ
    // Open with B and wait until the UITK panel is clickable
    p.Note("B でビルドメニューを開く");
    await p.PressKey(Key.B);
    await p.UntilUiElement("build-menu-panel", 15f);
    await p.Screenshot("01-opened");

    // 末尾カテゴリへジャンプし、見出しが視口の上端に来る（L-1）
    // Jump to the last category; its heading reaches the viewport top (L-1)
    var lastButton = Layer().Q<VisualElement>("build-menu-sidebar").Query<Button>().ToList().Last();
    var lastGuid = Guid.Parse(lastButton.name.Replace("build-menu-category-", ""));
    p.Note($"末尾のカテゴリ {lastButton.name} へジャンプする");
    await p.ClickUi(lastButton.name);
    await p.Until(() => Math.Abs(Scroll().scrollOffset.y - HeadingTop(lastGuid)) <= 1f, 5f, "末尾の見出しが視口の上端に来る");
    await p.Screenshot("02-trailing-jump");

    // 末尾ジャンプの後に手でホイールを回すと一覧だけが動き、カメラのズームは動かない
    // A hand wheel after the trailing jump moves only the list; the camera zoom stays put
    var jumpedOffset = Scroll().scrollOffset.y;
    var cameraBefore = Camera.main.transform.position;
    SemanticInput.MouseMoveTo((await PlaytestUiElementOps.FindUi("build-menu-scroll-area")).ScreenRect.center);
    await p.WaitSeconds(0.5f);
    p.Note("一覧の上でホイールを上へ 3 ノッチ回す");
    for (var i = 0; i < 3; i++)
    {
        SemanticInput.MouseWheel(1f);
        await p.WaitSeconds(0.2f);
    }
    await p.WaitSeconds(0.5f);
    var cameraMoved = Vector3.Distance(cameraBefore, Camera.main.transform.position);
    p.Note($"手のスクロール 一覧 {jumpedOffset} → {Scroll().scrollOffset.y} カメラの移動 {cameraMoved:F3}");
    p.Assert(Scroll().scrollOffset.y < jumpedOffset - 1f, "手のホイールで一覧が上へ動いた");
    p.Assert(cameraMoved < 0.01f, "手のホイールでカメラが動かない");
    await p.Screenshot("02b-hand-scroll");

    // 検索で絞り込み、閉じて開き直すと検索語と位置が戻る（R-9）
    // Filter by search; closing and reopening restores the query and the position (R-9)
    p.Note("検索欄に「ベルト」を入れ、閉じて開き直す");
    await p.SetText("build-menu-search", "ベルト");
    await p.WaitSeconds(0.5f);
    var filteredOffset = Scroll().scrollOffset.y;
    await p.Screenshot("03-search");
    await p.PressKey(Key.B);
    await p.WaitUiState(UIStateEnum.GameScreen, 10f);
    await p.PressKey(Key.B);
    await p.UntilUiElement("build-menu-panel", 15f);
    await p.WaitSeconds(1f);
    p.Assert(Layer().Q<TextField>("build-menu-search").value == "ベルト", "検索語が開き直しても戻る");
    p.Assert(Math.Abs(Scroll().scrollOffset.y - filteredOffset) <= 1f, $"スクロール位置が戻る（{filteredOffset} → {Scroll().scrollOffset.y}）");
    await p.Screenshot("04-restored");

    // 検索を空に戻すと末尾付近に居るので、ベルトのカテゴリへ跳んでから選ぶ（前例 webui-buildmenu-click）
    // Clearing the search leaves us near the end, so jump to the belt's category before selecting (precedent webui-buildmenu-click)
    await p.SetText("build-menu-search", "");
    var beltMaster = MasterHolder.BlockMaster.GetBlockMaster(PlaytestBlockOps.ResolveBlockId("ベルトコンベア"));
    var (beltCategoryGuid, _) = MasterHolder.BuildMenuCategoryMaster.GetGuidPair(beltMaster.Category, beltMaster.SubCategory);
    await p.ClickUi($"build-menu-category-{beltCategoryGuid:D}");
    p.Note("ベルトコンベアを選ぶ");
    await p.ClickBuildMenuBlock("ベルトコンベア");
    await p.WaitUiState(UIStateEnum.PlaceBlock, 15f);
    p.Assert(p.CurrentUiState == UIStateEnum.PlaceBlock, "UITK のエントリのタップで PlaceBlock へ移った");

    // 閉じるボタンで GameScreen へ戻り、パネルが隠れる（D-27）
    // The close button returns to GameScreen and the panel hides (D-27)
    await p.PressKey(Key.Escape);
    await p.WaitUiState(UIStateEnum.GameScreen, 10f);
    await p.PressKey(Key.B);
    await p.UntilUiElement("build-menu-panel", 15f);
    await p.ClickUi("build-menu-close");
    await p.WaitUiState(UIStateEnum.GameScreen, 10f);
    p.Assert(Layer().Q<VisualElement>("build-menu-panel").resolvedStyle.display == DisplayStyle.Flex && !IsMenuShown(), "閉じるでパネルが隠れた");
    await p.Screenshot("05-closed");

    #region Internal

    VisualElement Layer() => GameUiRoot.Instance.GetLayer(UiLayer.Screen, UiFamily.Stage);

    GameScrollView Scroll() => Layer().Q<GameScrollView>("build-menu-scroll-area");

    bool IsMenuShown() => Layer().Q<VisualElement>(className: "build-menu").resolvedStyle.display == DisplayStyle.Flex;

    float HeadingTop(Guid categoryGuid)
    {
        Scroll().TryGetContentTop(Layer().Q<VisualElement>($"build-menu-category-heading-{categoryGuid:D}"), out var top);
        return top;
    }

    #endregion
});
