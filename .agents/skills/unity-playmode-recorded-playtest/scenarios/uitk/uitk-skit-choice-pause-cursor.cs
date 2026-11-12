// スキットの選択肢（S2-skit）: 選択肢を持つ sample_short を UITK で出し、乗ると指・乗ったまま Esc でポーズに入ると指が外れること、ポーズを閉じて UITK の選択肢で選べることを録画する
// Skit choices (S2-skit): show sample_short's choices in UITK, record the pointer finger on hover, its removal on entering the pause with Esc while hovering, and picking a choice after closing the pause
// 実行前に PlayMode を止め、execute-dynamic-code で UiSystemEditorSwitch.Set(UiSystem.GameUi) を立ててから run-scenario.sh を呼ぶ
// Stop PlayMode and call UiSystemEditorSwitch.Set(UiSystem.GameUi) via execute-dynamic-code before run-scenario.sh
using System.Reflection;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.UIState;
using Client.Game.InGame.UI.UIState.State;
using Client.Game.InGame.UI.UIState.State.NestedPause;
using Client.Input;
using Client.Playtest;
using Client.Playtest.Input;
using Client.Playtest.Operations;
using Client.Skit.UI;
using Client.Ui.Foundation.Input.OsCursor;
using Client.Ui.State.Skit;
using Client.Ui.State.WholeUiVisibility;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;
using Object = UnityEngine.Object;

var options = new PlaytestRunOptions { Record = true };
return PlaytestRunner.Run("uitk-skit-choice-pause-cursor", options, async p =>
{
    // 背景Editorでも注入入力が届くよう焦点無視へ切り替える
    // Ignore focus so injected input reaches a background Editor
    InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
    InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
    SemanticInput.EnsureDevices();
    InputSystem.EnableDevice(Keyboard.current);
    InputSystem.EnableDevice(Mouse.current);

    p.Note($"全体スイッチ 系={UiInputExclusivity.ActiveUiSystem}");
    p.Assert(UiInputExclusivity.ActiveUiSystem == UiSystem.GameUi, "全体スイッチが UITK（GameUi）");
    // Game View が裏タブだと描画されず、Recorder が初回フレーム待ちで timeScale=0 に固定してスキットの scaled Delay が返らないため表へ出す（vq8w・iefh）
    // A Game View behind another tab never renders, so the Recorder pins timeScale=0 and the skit's scaled delays never return; bring it to front (vq8w, iefh)
    Client.Playtest.Operations.Ui.PlaytestGameViewOps.BringToFront();
    await p.Until(() => 0f < Time.timeScale, 10f, "Recorderの初回フレーム取得でtimeScaleが戻る");
    await p.SetupDebugEnvironment(new PlaytestEnvironmentConfig());

    // VContainer がスキットの口・提示・全体非表示の型を解決できる
    // VContainer resolves the skit commands, the presentation and the whole-UI hiding types
    var resolver = ClientDIContext.DIContainer.DIContainerResolver;
    p.Note($"解決: {resolver.Resolve<ISkitCommands>().GetType().Name} / {resolver.Resolve<SkitPresentationModel>().GetType().Name} / {resolver.Resolve<WholeUiVisibilityModel>().GetType().Name} / {resolver.Resolve<IWholeUiVisibilitySource>().GetType().Name}");

    var store = SkitPresentationStateStore.Instance;
    var skitManager = Object.FindFirstObjectByType<Client.Game.Skit.SkitManager>(FindObjectsInactive.Include);
    var backgroundSkit = Object.FindFirstObjectByType<Client.Game.InGame.BackgroundSkit.BackgroundSkitManager>(FindObjectsInactive.Include);
    var control = Object.FindFirstObjectByType<UIStateControl>();
    var dictionary = (UIStateDictionary)typeof(UIStateControl).GetField("_uiStateDictionary", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(control);
    var skitState = (SkitState)dictionary.GetState(UIStateEnum.Story);
    bool MenuOpen() => skitState.SubState == NestedPauseSubStateEnum.PauseMenuScreen;

    // 開幕スキットと続く背景スキットを終わらせてから直接起動する（uitk-nested-pause-skit と同じ）
    // Finish the opening skit and the background skit that follows before starting one directly (same as uitk-nested-pause-skit)
    await p.SkipOpeningSkitIfPlaying();
    p.Note($"飛ばした直後 timeScale={Time.timeScale} 再生中={skitManager.IsPlayingSkit} UI={p.CurrentUiState}");
    await p.Until(() => !skitManager.IsPlayingSkit && p.CurrentUiState == UIStateEnum.GameScreen, 60f, "開幕スキットが終わりゲーム画面");
    await p.WaitSeconds(1f);
    await p.Until(() => !backgroundSkit.IsPlayingSkit && !skitManager.IsPlayingSkit, 120f, "背景スキットも終わる");

    // 100_start_game に選択肢は無いので、選択肢を持つ sample_short を起動して選択肢まで送る
    // 100_start_game has no choices, so start sample_short, which has one, and advance to it
    p.Note("選択肢を持つ sample_short を直接起動する");
    skitManager.StartSkit("Vanilla/Skit/skits/sample_short").Forget();
    await p.Until(() => store.GetCurrent().PresentationState.Mode == SkitPresentationData.BlockingMode, 60f, "スキット(blocking)開始待ち");
    await p.UntilUiElement("blocking-skit", 10f);
    for (var i = 0; i < 20 && store.GetCurrent().PresentationState.Choices.Length == 0 && store.GetCurrent().PresentationState.Mode == SkitPresentationData.BlockingMode; i++)
    {
        var current = store.GetCurrent();
        store.TryAdvance(current.SessionId, current.SceneRevision);
        await p.WaitSeconds(0.5f);
    }
    var choices = store.GetCurrent().PresentationState.Choices;
    p.Assert(choices.Length > 0, $"sample_short で選択肢が出る（実測 {choices.Length} 件）");
    var choiceTestid = $"skit-choice-{choices[0].ChoiceId}";
    await p.UntilUiElement(choiceTestid, 10f);
    await p.Screenshot("01-skit-choices");

    // 選択肢に乗ると指になり、乗ったまま Esc でポーズに入ると指が外れる（Z-7）
    // Hovering a choice shows the finger, and entering the pause with Esc while hovering removes it (Z-7)
    p.Note($"乗る前のカーソル={AppliedCursorShape()}");
    await p.HoverUi(choiceTestid);
    await p.WaitSeconds(0.5f);
    p.Assert(AppliedCursorShape() == UiOsCursorShape.Pointer.ToString(), $"選択肢に乗ると指（実測 {AppliedCursorShape()}）");
    await p.PressKey(Key.Escape);
    await p.Until(MenuOpen, 5f, "入れ子ポーズが開く");
    await p.UntilUiElement("pause-menu", 10f);
    await p.WaitSeconds(0.5f);
    p.Assert(AppliedCursorShape() == UiOsCursorShape.Default.ToString(), $"乗ったままポーズに入ると指が外れる（実測 {AppliedCursorShape()}）");
    var choiceBehindPause = await PlaytestUiElementOps.FindUi(choiceTestid);
    // ポーズ中は許可が空になり select が外れるので、Web（SkitPresentation.tsx の allowedIntents.has("select")）と同じく選択肢は隠れる
    // While paused the allowed intents empty out and drop select, so the choices hide as on the Web (allowedIntents.has("select") in SkitPresentation.tsx)
    p.Assert(!choiceBehindPause.IsFound || !choiceBehindPause.Visible, $"ポーズ中は選択肢が隠れる（実測 {choiceBehindPause.Status}）");
    await p.Screenshot("02-pause-over-choice");

    // ポーズを閉じ、UITK の選択肢で選ぶ
    // Close the pause and pick with the UITK choice
    await p.PressKey(Key.Escape);
    await p.Until(() => !MenuOpen(), 5f, "入れ子ポーズが閉じる");
    await p.UntilUiElement(choiceTestid, 5f);
    await p.ClickUi(choiceTestid);
    await p.Until(() => store.GetCurrent().PresentationState.Choices.Length == 0, 5f, "UITK の選択肢で選んだ");
    await p.Screenshot("03-after-choice");

    // 後始末: UITK のスキップでゲーム画面へ
    // Cleanup: skip in UITK back to the game screen
    if (store.GetCurrent().PresentationState.Mode == SkitPresentationData.BlockingMode)
    {
        await p.ClickUi("skit-tool-skip");
    }
    await p.Until(() => p.CurrentUiState == UIStateEnum.GameScreen, 60f, "スキット終了でGameScreen");

    #region Internal

    // プロセスで 1 本の OS カーソルの要求スタックが今当てている形を読む
    // Reads the shape the process-wide OS cursor request stack currently applies
    string AppliedCursorShape()
    {
        var stack = typeof(ProcessUiOsCursor).GetField("_processStack", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        if (stack == null) return "スタック無し";
        return typeof(UiOsCursorRequestStack).GetField("_applied", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(stack).ToString();
    }

    #endregion
});
