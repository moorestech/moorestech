// 設置・破壊の終了案内を全言語で観測し、入力経由で退出する
// Observe placement and deletion exit hints in every language and exit through real input
using System.Reflection;
using Client.Game.InGame.Control;
using Client.Game.InGame.UI.UIState;
using Client.Localization;
using Client.Playtest;
using Client.Playtest.Input;
using Client.Playtest.Operations;
using Client.Playtest.Operations.Ui;
using Client.Playtest.WebUi;
using Cysharp.Threading.Tasks;
using Mooresmaster.Localization.Generated;
using UnityEngine;
using UnityEngine.InputSystem;

var options = new PlaytestRunOptions { Record = true };
return PlaytestRunner.Run("placement-exit-hints", options, async p =>
{
    // スキットが無いワールドでも正規の前提処理を完了する
    // Complete the supported preparation even in worlds without an opening skit
    await p.SetupDebugEnvironment(new PlaytestEnvironmentConfig());
    await PlaytestSkitOps.SkipOpeningSkitIfPlaying(5f);
    await p.WaitUiState(UIStateEnum.GameScreen, 30f);
    var initialLanguage = Localize.GetCurrentLanguageCode();
    var control = Object.FindFirstObjectByType<UIStateControl>();
    var dictionary = (UIStateDictionary)typeof(UIStateControl)
        .GetField("_uiStateDictionary", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(control);

    // 両モードを実入力で開き、辞書とWeb描画の双方を検証する
    // Enter both modes using real input and verify the dictionary and Web rendering
    foreach (var language in new[] { "japanese", "english", "german", "korean" })
    {
        p.Note(language + ": 終了案内を設置と破壊で確認する");
        p.Assert(Localize.TrySetChosenLanguage(language), language + ": 言語切替成功");
        await p.OpenBuildMenuAndSelectBlock("木のチェスト");
        await ObserveHints(language, UIStateEnum.PlaceBlock, 11, LocalizationKeys.Ui.KeyHint.Key.B.Key);
        await p.PressKey(Key.Escape);
        await p.WaitUiState(UIStateEnum.GameScreen, 5f);
        p.Assert(p.CurrentUiState == UIStateEnum.GameScreen, language + ": 設置Esc終了");

        await p.PressKey(Key.G);
        await p.WaitUiState(UIStateEnum.DeleteBar, 5f);
        await ObserveHints(language, UIStateEnum.DeleteBar, 8, LocalizationKeys.Ui.KeyHint.Key.G.Key);
        await p.PressKey(Key.Escape);
        await p.WaitUiState(UIStateEnum.GameScreen, 5f);
        p.Assert(p.CurrentUiState == UIStateEnum.GameScreen, language + ": 破壊Esc終了");
    }

    // 右短押しはUI外を観測してから注入し、両モードの終了を確認する
    // Observe an outside-UI pointer before injecting a right short press in each mode
    p.Note("UI外の右短押しで設置と破壊を終了する");
    await p.OpenBuildMenuAndSelectBlock("木のチェスト");
    await ExitWithRightShortPress("設置");
    await p.PressKey(Key.G);
    await p.WaitUiState(UIStateEnum.DeleteBar, 5f);
    await ExitWithRightShortPress("破壊");
    p.Assert(Localize.TrySetChosenLanguage(initialLanguage), "開始時の言語へ復帰");
    await p.Screenshot("final-game-screen");

    #region Internal

    async UniTask ObserveHints(string language, UIStateEnum state, int count, string firstKey)
    {
        var hints = dictionary.GetState(state).GetKeyHints();
        p.Assert(hints.Count == count, language + ": " + state + " の件数");
        p.Assert(hints[0].KeyNameKey.Key == firstKey, state + ": 即終了キーが先頭");
        p.Assert(hints[1].KeyNameKey.Key == LocalizationKeys.Ui.KeyHint.Key.RightShortPressOutsideUiOrEsc.Key,
            state + ": 右短押し/Escが2行目");
        p.Assert(hints[1].TextKey.Key == LocalizationKeys.Ui.KeyHint.Text.CancelOperationOrExitMode.Key,
            state + ": 操作キャンセル/終了案内");

        // 翻訳の未解決とWeb描画の取り残しを同時に検出する
        // Detect unresolved translations and stale Web rendering together
        var keyText = Localize.Get(hints[1].KeyNameKey);
        var actionText = Localize.Get(hints[1].TextKey);
        p.Assert(!string.IsNullOrWhiteSpace(keyText) && keyText != hints[1].KeyNameKey.Key, language + ": キー翻訳解決");
        p.Assert(!string.IsNullOrWhiteSpace(actionText) && actionText != hints[1].TextKey.Key, language + ": 案内翻訳解決");
        p.Note(language + ": " + keyText + " / " + actionText);
        var deadline = Time.realtimeSinceStartup + 15f;
        var dom = await PlaytestDomQuery.Query("key-hints", 2f);
        while ((!dom.Found || !dom.Text.Contains(keyText) || !dom.Text.Contains(actionText)) && Time.realtimeSinceStartup < deadline)
        {
            await UniTask.Yield();
            dom = await PlaytestDomQuery.Query("key-hints", 2f);
        }
        p.Assert(dom.Found && dom.Text.Contains(keyText) && dom.Text.Contains(actionText), language + ": Webに翻訳済み案内を表示");
        await p.Screenshot(language + "-" + state);
    }

    async UniTask ExitWithRightShortPress(string label)
    {
        SemanticInput.MouseMoveTo(new Vector2(8f, Screen.height - 8f));
        await UniTask.DelayFrame(3);
        await p.Until(() => !UiPointerHitTest.IsPointerOverAnyUi(), 5f, label + ": ポインタがUI外");
        await p.RightShortClick();
        await p.WaitUiState(UIStateEnum.GameScreen, 5f);
        p.Assert(p.CurrentUiState == UIStateEnum.GameScreen, label + ": 右短押し終了");
    }

    #endregion
});
