// ホットバー（枠のクリックで建築・入替・枠外で解除）と BP 名のモーダル（確定・幕・ESC 1 回）を UITK（GameUi）で通す（S3-hotbar-modal）
// Runs the hotbar (click-to-build, swap, clear outside) and the blueprint name modal (confirm, backdrop, one ESC) on UITK (GameUi) (S3-hotbar-modal)
// 実行前に PlayMode を止め、execute-dynamic-code で UiSystemEditorSwitch.Set(UiSystem.GameUi) を立ててから run-scenario.sh を呼ぶ
// Stop PlayMode and call UiSystemEditorSwitch.Set(UiSystem.GameUi) via execute-dynamic-code before run-scenario.sh
using System;
using Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common;
using Client.Game.InGame.Context;
using Client.Game.InGame.Hotbar;
using Client.Game.InGame.UI.Tooltip;
using Client.Game.InGame.UI.UIState;
using Client.Input;
using Client.Playtest;
using Client.Playtest.Input;
using Client.Playtest.Operations;
using Client.Playtest.Operations.Ui;
using Client.Ui.Foundation.Root;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using VContainer;

var options = new PlaytestRunOptions { Record = true, ScenarioTimeoutSeconds = 300f };
return PlaytestRunner.Run("uitk-hotbar-and-bp-modal", options, async p =>
{
    // 1. 系の主張と入力の準備（前例 uitk-mode-hud-and-tooltip）
    // 1. Assert the UI system and prepare input (precedent uitk-mode-hud-and-tooltip)
    p.Note($"全体スイッチ 系={UiInputExclusivity.ActiveUiSystem}");
    p.Assert(UiInputExclusivity.ActiveUiSystem == UiSystem.GameUi, "全体スイッチが UITK（GameUi）");
    InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
    InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
    SemanticInput.EnsureDevices();
    InputSystem.EnableDevice(Keyboard.current);
    InputSystem.EnableDevice(Mouse.current);
    var resolver = ClientDIContext.DIContainer.DIContainerResolver;
    var hotbar = resolver.Resolve<ClientHotbarDatastore>();
    var library = resolver.Resolve<ClientBlueprintLibrary>();
    var heightOffset = resolver.Resolve<PlacementHeightOffset>();

    // 2. 木のチェスト・石窯を割り当て、列と枠 0 の埋まりを見る
    // 2. Assign the wooden chest and stone oven; check the row and slot 0 is filled
    await p.SetupDebugEnvironment(new PlaytestEnvironmentConfig());
    await p.SkipOpeningSkitIfPlaying();
    await p.ExitToGameScreen();
    await p.Hotbar.AssignHotbar(0, "木のチェスト");
    await p.Hotbar.AssignHotbar(1, "石窯");
    var chestId = hotbar.Assignments[0];
    p.Assert((await PlaytestUiElementOps.FindUi("hotbar-grid")).IsFound, "列 hotbar-grid が出る");
    var slot0 = await PlaytestUiElementOps.FindUi("hotbar-slot-0");
    p.Assert(slot0.HasStateFlag("slot-frame--filled"), $"枠 0 が埋まりの状態 [{string.Join(",", slot0.StateFlags)}]");
    await p.Screenshot("01-hotbar");

    // 3. カーソルのロックを左 Alt で解き、既定の選択でない枠 1 をクリック → 建築モード → ESC で戻る
    // 3. Free the locked cursor with left Alt, click slot 1 (not the default selection) -> build mode -> ESC returns
    await HoldAltAsync(true);
    await p.ClickUi("hotbar-slot-1");
    await p.WaitUiState(UIStateEnum.PlaceBlock, 10f);
    await HoldAltAsync(false);
    await p.Until(() => UiTree().Q<VisualElement>("hotbar-slot-1").ClassListContains("slot-frame--selected") && !UiTree().Q<VisualElement>("hotbar-slot-0").ClassListContains("slot-frame--selected"), 5f, "クリックした枠 1 が選択の状態（枠 0 は外れる）");
    await p.Screenshot("02-slot-click-build");
    await p.PressKey(Key.Escape);
    await p.WaitUiState(UIStateEnum.GameScreen, 10f);

    // 4. 枠 0 → 枠 2 のドラッグで入替、5. 枠 2 → 画面上 1/3 で解除
    // 4. Drag slot 0 to slot 2 to swap; 5. drag slot 2 to the upper third to clear
    await DragAsync(await CenterOf("hotbar-slot-0"), await CenterOf("hotbar-slot-2"));
    await p.Until(() => hotbar.Assignments[2] == chestId && hotbar.Assignments[0] == Guid.Empty, 5f, "入替: 枠 2 が木のチェスト・枠 0 が空き");
    await p.Screenshot("03-swap");
    await DragAsync(await CenterOf("hotbar-slot-2"), new Vector2(Screen.width / 2f, Screen.height * 2f / 3f));
    await p.Until(() => hotbar.Assignments[2] == Guid.Empty, 5f, "枠外への落としで枠 2 が解除");
    await p.Screenshot("04-clear");

    // 6. BP コピーで名前のモーダル: 空では押せず、前後の空白を除いた名で登録される
    // 6. Blueprint copy name modal: disabled while empty; registers the trimmed name
    await p.Hotbar.UnlockBlueprint();
    await p.PlaceBlockDirect("木のチェスト", new Vector3Int(2, 32, 2), BlockDirection.North);
    await p.WaitBlockGameObject(new Vector3Int(2, 32, 2));
    p.WarpPlayer(new Vector3(6f, 33.5f, -5f));
    var checks = new BlueprintCopyPasteScenarioChecks(p, resolver.Resolve<MouseCursorTooltipState>());
    await checks.SelectEntry("build-menu-category-d1000000-0000-4000-8000-000000000009", "build-menu-entry-blueprintCopy-88dd687d-aceb-4aeb-94e8-44be1e1f5a0d");
    await OpenNameModalFromStart();
    // 無効は SetEnabled の擬似状態（unity-disabled）で「--」の状態名に出ないので要素から読む
    // Disabled is SetEnabled's pseudo state (unity-disabled), not a "--" state name, so read the element
    var button = UiTree().Q<VisualElement>("modal-button");
    p.Assert(!button.enabledSelf && button.ClassListContains("unity-disabled"), $"空の名前では確定ボタンが無効 enabledSelf={button.enabledSelf}");
    p.Assert(UiInputExclusivity.IsTextInputFocused, "モーダルが開くと入力欄にフォーカス（入力中の旗が立つ）");
    var beforeTyping = p.PlayerPosition;
    await HoldKeyAsync(Key.W, "入力中に W を 0.6 秒押す");
    p.Note($"入力中の W の前後でプレイヤーの位置 {beforeTyping} → {p.PlayerPosition}");
    p.Assert(Vector3.Distance(beforeTyping, p.PlayerPosition) < 0.05f, "入力中は W でプレイヤーが動かない");
    await p.SetText("modal-input", "  uitk-bp  ");
    await p.Screenshot("05a-bp-modal-typed");
    await p.ClickUi("modal-button");
    await p.WaitForBlueprint("uitk-bp", true, 15f, "BP『uitk-bp』が前後の空白を除いた名前で登録される");
    await p.Until(() => UiTree().Q<VisualElement>("modal") == null, 5f, "確定でモーダルが閉じる");
    await p.Screenshot("05-bp-modal");

    // 7. 幕の左下のクリックで取消。PlaceBlock のまま・BP は増えず、クリックは世界へ抜けない（始点が打ち直されない）
    // 7. A click on the backdrop's bottom-left cancels; stays in PlaceBlock, adds no blueprint, and does not reach the world (start not re-placed)
    var blueprintCount = library.Blueprints.Count;
    await OpenNameModalFromStart();
    var backdrop = await PlaytestUiElementOps.FindUi("modal-backdrop");
    SemanticInput.MouseMoveTo(new Vector2(backdrop.ScreenRect.xMin + 5f, backdrop.ScreenRect.yMax - 5f));
    await p.WaitSeconds(0.5f);
    await SemanticInput.Click();
    await AssertCancelledAsync("幕のクリック", blueprintCount);
    checks.AssertMarker("BlueprintCopyStartMarker", new Vector3Int(0, 32, 0), "幕のクリックで始点 (0,32,0) が打ち直されない");
    await p.Screenshot("06-backdrop-cancel");

    // 7b. 入力欄にフォーカスがあるときの ESC 1 回で取消、閉じた後に旗が落ちゲームの E が効く
    // 7b. One ESC with the field focused cancels; after closing the flag drops and the game's E works
    await OpenNameModalAtEnd();
    await p.SetText("modal-input", "途中");
    p.Assert(UiInputExclusivity.IsTextInputFocused, "ESC の前は入力中の旗が立っている");
    await p.PressKey(Key.Escape);
    await AssertCancelledAsync("入力欄にフォーカスがある ESC 1 回", blueprintCount);
    await p.Screenshot("07b-esc-in-field");
    var heightBefore = heightOffset.Value;
    await p.PressKey(Key.E);
    p.Assert(heightOffset.Value == heightBefore + 1, $"閉じた後にゲームの E で高さが 1 上がる（{heightBefore} → {heightOffset.Value}）");
    await p.PressKey(Key.Q);

    // 7b'. 本文のクリックでフォーカスを外し（閉じない）、フォーカスの無い ESC 1 回でも取消
    // 7b'. Click the message to drop focus (does not close), then one focus-less ESC cancels too
    await OpenNameModalAtEnd();
    await p.ClickUi("modal-message");
    await p.WaitSeconds(0.5f);
    p.Assert((await PlaytestUiElementOps.FindUi("modal")).IsFound, "本文のクリックではモーダルが閉じない");
    p.Assert(!UiInputExclusivity.IsTextInputFocused, "本文のクリックで入力中の旗が落ちる");
    await p.PressKey(Key.Escape);
    await AssertCancelledAsync("フォーカスの無い ESC 1 回", blueprintCount);
    await p.Screenshot("07b-esc-unfocused");

    #region Internal

    VisualElement UiTree() => GameUiRoot.Instance.GetRootElement();

    async UniTask<Vector2> CenterOf(string testid) => (await PlaytestUiElementOps.FindUi(testid)).ScreenRect.center;

    async UniTask DragAsync(Vector2 from, Vector2 to)
    {
        p.Note($"ドラッグ {from} → {to}");
        await HoldAltAsync(true);
        SemanticInput.MouseMoveTo(from);
        await p.WaitSeconds(0.3f);
        SemanticInput.MouseButtonDown(0);
        await UniTask.DelayFrame(2);
        await SemanticInput.MouseGlideTo(to, 0.4f);
        SemanticInput.MouseButtonUp(0);
        await HoldAltAsync(false);
    }

    async UniTask HoldAltAsync(bool down)
    {
        if (down) SemanticInput.KeyDown(Key.LeftAlt); else SemanticInput.KeyUp(Key.LeftAlt);
        await p.WaitSeconds(0.5f);
    }

    async UniTask HoldKeyAsync(Key key, string label)
    {
        p.Note(label);
        SemanticInput.KeyDown(key);
        await p.WaitSeconds(0.6f);
        SemanticInput.KeyUp(key);
    }

    // 始点 (0,32,0)・終点 (4,32,4) をクリックし、範囲にチェスト 1 個で名前のモーダルを開く
    // Click start (0,32,0) and end (4,32,4); the range holds one chest, opening the name modal
    async UniTask OpenNameModalFromStart()
    {
        await p.AimAt(new Vector3(0.5f, 32f, 0.5f));
        await p.ClickPlace();
        await OpenNameModalAtEnd();
    }

    // 取消の後は始点が残るので終点のクリックだけで開き直す
    // The start survives a cancel, so clicking the end alone reopens it
    async UniTask OpenNameModalAtEnd()
    {
        await p.AimAt(new Vector3(4.5f, 32f, 4.5f));
        await p.ClickPlace();
        await p.UntilUiElement("modal-input", 10f);
        await p.Until(() => UiInputExclusivity.IsTextInputFocused, 3f, "入力欄にフォーカス（入力中の旗）");
        p.Assert(EventSystem.current.currentSelectedGameObject?.GetComponent<PanelEventHandler>()?.panel == UiTree().panel, $"EventSystem の選択が GameUi のパネル＝実キーの KeyDown が入力欄へ届く条件（選択 {EventSystem.current.currentSelectedGameObject?.name}）");
    }

    async UniTask AssertCancelledAsync(string label, int blueprintCountBefore)
    {
        await UniTask.DelayFrame(2);
        p.Assert(!(await PlaytestUiElementOps.FindUi("modal")).IsFound, $"{label}でモーダルが閉じる");
        p.Assert(p.CurrentUiState == UIStateEnum.PlaceBlock, $"{label}の後も PlaceBlock（ポーズも建築解除も無い） 実際:{p.CurrentUiState}");
        p.Assert(!UiInputExclusivity.IsTextInputFocused, $"{label}の後は入力中の旗が落ちる");
        p.Assert(library.Blueprints.Count == blueprintCountBefore, $"{label}で BP が増えない（{blueprintCountBefore} → {library.Blueprints.Count}）");
    }

    #endregion
});
