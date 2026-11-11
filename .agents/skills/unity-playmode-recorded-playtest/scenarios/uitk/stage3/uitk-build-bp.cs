// ラダー 3: B → 検索 → エントリをホットバーへドラッグ → 選んで設置 → BP をコピーして名前を付ける（GameUi。S3-ladder）
// Ladder 3: B -> search -> drag an entry onto the hotbar -> select and place -> copy a blueprint and name it (GameUi, S3-ladder)
// 実行前に PlayMode を止め、execute-dynamic-code で UiSystemEditorSwitch.Set(UiSystem.GameUi) を立ててから run-scenario.sh を呼ぶ
// Stop PlayMode and call UiSystemEditorSwitch.Set(UiSystem.GameUi) via execute-dynamic-code before run-scenario.sh
using System;
using System.Linq;
using Client.Game.InGame.Context;
using Client.Game.InGame.Hotbar;
using Client.Game.InGame.UI.Tooltip;
using Client.Game.InGame.UI.UIState;
using Client.Input;
using Client.Playtest;
using Client.Playtest.Input;
using Client.Playtest.Operations;
using Client.Playtest.Operations.Ui;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;

var options = new PlaytestRunOptions { Record = true, ScenarioTimeoutSeconds = 300f };
return PlaytestRunner.Run("uitk-build-bp", options, async p =>
{
    // 1. 系の主張と入力の準備（前例 uitk-hotbar-and-bp-modal）
    // 1. Assert the UI system and prepare input (precedent uitk-hotbar-and-bp-modal)
    p.Note($"全体スイッチ 系={UiInputExclusivity.ActiveUiSystem}");
    p.Assert(UiInputExclusivity.ActiveUiSystem == UiSystem.GameUi, "全体スイッチが UITK（GameUi）");
    InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
    InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
    SemanticInput.EnsureDevices();
    InputSystem.EnableDevice(Keyboard.current);
    InputSystem.EnableDevice(Mouse.current);
    var resolver = ClientDIContext.DIContainer.DIContainerResolver;
    var hotbar = resolver.Resolve<ClientHotbarDatastore>();
    await p.SetupDebugEnvironment(new PlaytestEnvironmentConfig());
    await p.SkipOpeningSkitIfPlaying();
    await p.ExitToGameScreen();

    // 2. B で開き、検索欄の入力中は W で動かず、ESC はフォーカスだけ外し、外した後の W では動く
    // 2. Open with B; W does not move while search is focused, ESC only drops focus, then W moves
    await p.PressKey(Key.B);
    await p.WaitUiState(UIStateEnum.BuildMenu, 10f);
    await p.ClickUi("build-menu-search");
    await p.Until(() => UiInputExclusivity.IsTextInputFocused, 3f, "検索欄のクリックで入力中の旗が立つ");
    var before = p.PlayerPosition;
    await HoldKeyAsync(Key.W, "入力中に W を 0.6 秒押す");
    p.Assert(Vector3.Distance(before, p.PlayerPosition) < 0.05f, $"検索欄の入力中は W で動かない {before} → {p.PlayerPosition}");
    await p.PressKey(Key.Escape);
    await p.WaitSeconds(0.3f);
    p.Assert(!UiInputExclusivity.IsTextInputFocused, "ESC 1 回で検索欄のフォーカスが外れる");
    p.Assert(p.CurrentUiState == UIStateEnum.BuildMenu, $"ESC でメニューは閉じない 実際:{p.CurrentUiState}");
    var afterBlur = p.PlayerPosition;
    await HoldKeyAsync(Key.W, "フォーカスを外した後に W を 0.6 秒押す（対照）");
    p.Assert(Vector3.Distance(afterBlur, p.PlayerPosition) > 0.05f, $"フォーカスを外した後の W では動く（入力が届いている対照） {afterBlur} → {p.PlayerPosition}");

    // 3. 検索語を入れ、石窯のエントリを枠 1 へドラッグ（枠 1 だけが石窯になり、建築モードにはならない）
    // 3. Type the query and drag the stone oven entry to slot 1 (only slot 1 becomes the oven; no build mode)
    await p.SetText("build-menu-search", "石窯");
    var entryTestId = PlaytestBuildMenuOps.BuildMenuBlockTestId("石窯");
    await p.UntilUiElement(entryTestId, 5f);
    var ovenId = Guid.Parse(entryTestId.Substring(entryTestId.Length - 36));
    var assignedBefore = hotbar.Assignments.ToArray();
    p.Assert(assignedBefore[1] == Guid.Empty, "ドラッグの前は枠 1 が空き");
    await p.Screenshot("01-search");
    await DragAsync(await CenterOf(entryTestId), await CenterOf("hotbar-slot-1"));
    await p.Until(() => hotbar.Assignments[1] == ovenId, 5f, "枠 1 に石窯が割り当たる");
    p.Assert(Enumerable.Range(0, assignedBefore.Length).Where(i => i != 1).All(i => hotbar.Assignments[i] == assignedBefore[i]), "枠 1 以外の割当は変わらない");
    p.Assert(p.CurrentUiState == UIStateEnum.BuildMenu, $"ドラッグでは選択（建築モード）にならない 実際:{p.CurrentUiState}");
    await p.Screenshot("02-dragged");

    // 4. 検索語は開き直しで戻る（R-9）ので空にしてから閉じ、範囲の外へ退き、数字キー 2 で枠 1 を選んで石窯を建てる
    // 4. The query is restored on reopen (R-9), so clear it, close, step out of the range, pick slot 1 with digit 2 and build the oven
    await p.SetText("build-menu-search", "");
    await p.PressKey(Key.B);
    await p.WaitUiState(UIStateEnum.GameScreen, 10f);
    p.WarpPlayer(new Vector3(6f, 33.5f, -5f));
    await p.PressKey(Key.Digit2);
    await p.WaitUiState(UIStateEnum.PlaceBlock, 10f);
    await p.AimAt(new Vector3(2.5f, 32f, 2.5f));
    await p.ClickPlace();
    // 石窯は 3x2x3 なので中心 (2.5,32,2.5) を狙うと原点は (1,32,1) になる
    // The stone oven is 3x2x3, so aiming at its center (2.5,32,2.5) puts the origin at (1,32,1)
    await p.WaitBlockGameObject(new Vector3Int(1, 32, 1));
    await p.Screenshot("03-placed");
    await p.PressKey(Key.Escape);
    await p.WaitUiState(UIStateEnum.GameScreen, 10f);

    // 5. BP コピーを選び、範囲を打って名前を付ける（名前は SetText）
    // 5. Pick blueprint copy, mark the range and name it (the name via SetText)
    await p.Hotbar.UnlockBlueprint();
    var checks = new BlueprintCopyPasteScenarioChecks(p, resolver.Resolve<MouseCursorTooltipState>());
    await checks.SelectEntry("build-menu-category-d1000000-0000-4000-8000-000000000009", "build-menu-entry-blueprintCopy-88dd687d-aceb-4aeb-94e8-44be1e1f5a0d");
    await p.AimAt(new Vector3(0.5f, 32f, 0.5f));
    await p.ClickPlace();
    await p.AimAt(new Vector3(4.5f, 32f, 4.5f));
    await p.ClickPlace();
    await p.UntilUiElement("modal-input", 10f);
    await p.SetText("modal-input", "ラダーBP");
    await p.ClickUi("modal-button");
    await p.WaitForBlueprint("ラダーBP", true, 15f, "BP『ラダーBP』が登録される");
    await p.Screenshot("04-blueprint");

    #region Internal

    async UniTask<Vector2> CenterOf(string testid) => (await PlaytestUiElementOps.FindUi(testid)).ScreenRect.center;

    async UniTask DragAsync(Vector2 from, Vector2 to)
    {
        p.Note($"ドラッグ {from} → {to}");
        SemanticInput.MouseMoveTo(from);
        await p.WaitSeconds(0.3f);
        SemanticInput.MouseButtonDown(0);
        await UniTask.DelayFrame(2);
        await SemanticInput.MouseGlideTo(to, 0.4f);
        SemanticInput.MouseButtonUp(0);
        await p.WaitSeconds(0.3f);
    }

    async UniTask HoldKeyAsync(Key key, string label)
    {
        p.Note(label);
        SemanticInput.KeyDown(key);
        await p.WaitSeconds(0.6f);
        SemanticInput.KeyUp(key);
    }

    #endregion
});
