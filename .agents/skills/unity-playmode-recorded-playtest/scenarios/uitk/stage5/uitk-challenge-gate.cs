// ラダー 6 uitk-challenge-gate: Web ホスト無しの UITK で、出展モードの言語ゲートを日本語で通り、チャレンジ一覧を T で開いて閉じる
// Ladder 6 uitk-challenge-gate: on UITK with no web host, pass the exhibition language gate in Japanese, then open and close the challenge list with T
// 実行は stage5/driver/winunity-scenario-run.sh <branch> GameUi exhibition <この .cs> <name> か、Mac の stage5/exhibition-mode-run.sh <project> GameUi <この .cs> から
// Run via stage5/driver/winunity-scenario-run.sh <branch> GameUi exhibition <this .cs> <name>, or the Mac stage5/exhibition-mode-run.sh <project> GameUi <this .cs>
// ゲートは DI より前の 1 回きりで無操作はアプリ終了になりゲートへ戻らないので、plan §4.4 の語順どおりゲートを先に通る
// The gate shows once before DI and idling quits the app instead of returning to it, so this passes the gate first, as plan §4.4 orders it
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Client.Game.Common;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.UIState;
using Client.Input;
using Client.Localization;
using Client.Playtest;
using Client.Playtest.Input;
using Client.Ui.Foundation.Root;
using Client.Ui.Foundation.Text;
using Client.Ui.State.Challenge;
using Client.WebUiHost.Boot;
using Cysharp.Threading.Tasks;
using UniRx;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using VContainer;

return PlaytestRunner.Run("uitk-challenge-gate", new PlaytestRunOptions { Record = true }, async p =>
{
    // 1. 系の主張と、初期化の完了の購読
    // 1. Assert the UI system and subscribe to initialization completion
    p.Note($"系={UiInputExclusivity.ActiveUiSystem}");
    p.Assert(UiInputExclusivity.ActiveUiSystem == UiSystem.GameUi, "全体スイッチが GameUi");
    var initialized = false;
    var initializedSubscription = GameInitializedEvent.OnGameInitialized.Subscribe(_ => initialized = true);

    // Web ロケータのログは最初から、エラーは初期化の完了から数える（DI の前の毎フレームの NRE は 2026-08-28 裁定で許容）
    // Web-locator logs count from the start and errors from initialization completion (per-frame NREs before DI are allowed by the 2026-08-28 ruling)
    var errorsAfterInitialization = new List<string>();
    var webLocatorLogs = new List<string>();
    Application.logMessageReceived += CountLog;
    try
    {
        // 2. DI より前の言語ゲート。選ぶまで初期化は止まっている
        // 2. The pre-DI language gate; initialization holds until a choice
        await p.UntilUiElement("event-language-gate", 60f);
        var root = GameUiRoot.Instance.GetRootElement();
        p.Assert(!initialized, "言語を選ぶまで初期化が完了しない");
        await p.Screenshot("01-event-language-gate");

        // 選ぶ前の言語を控え、途中で落ちても運転役が戻せるようにする
        // Note the pre-choice language so the driver can restore it even if this dies midway
        var languageBefore = Localize.GetCurrentLanguageCode();
        var languageBeforePath = LanguageBeforePath();
        Directory.CreateDirectory(Path.GetDirectoryName(languageBeforePath));
        File.WriteAllText(languageBeforePath, languageBefore);

        // DI 前は背景のまま入力が無効になるので、押す前に注入をゲームへ通す（uitk-event-language-gate.cs と同じ設定）
        // Before DI an unfocused Editor leaves devices disabled, so route injection to the game before pressing (same as uitk-event-language-gate.cs)
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        SemanticInput.EnsureDevices();
        InputSystem.EnableDevice(Keyboard.current);
        InputSystem.EnableDevice(Mouse.current);
        p.Assert(Mouse.current.enabled, "押す前にマウスが有効");

        // 3. 日本語を押すとその場で外れ、初期化が完了する
        // 3. Pressing Japanese removes the gate at once and initialization completes
        await p.ClickUi("event-language-gate-option-japanese");
        await p.Until(() => root.Q("event-language-gate") == null, 5f, "押したその場でゲートが外れる");
        await p.Until(() => initialized, 180f, "選択の後に初期化が完了する");
        p.Assert(Localize.GetCurrentLanguageCode() == "japanese", "日本語で始まる");

        // 4. ゲーム画面まで進み、Web ホストが起きていないことを見る
        // 4. Reach the game screen and check no web host started
        await p.SetupDebugEnvironment(new PlaytestEnvironmentConfig());
        // Game View を表へ出し、Story のままなら飛ばし直して Story を抜ける（回数は Driver が Note に残す）
        // Bring the Game View front and re-skip while in Story until it leaves (the Driver notes the rounds)
        await p.SkipOpeningSkitUntilLeftStory();
        await p.ExitToGameScreen();
        p.Assert(WebUiHost.Hub == null, "Webホストは起動していない（hostless）");

        // 5. 解放済みの先頭カテゴリと最初の進行中を源から引く（uitk-challenge-list と同じ引き方）
        // 5. Take the first unlocked category and its first current challenge from the source (same as uitk-challenge-list)
        var progress = ClientDIContext.DIContainer.DIContainerResolver.Resolve<ChallengeProgressState>();
        var category = progress.Categories.First(c => c.IsUnlocked);
        var current = category.Category.Challenges.First(c => category.CurrentChallenges.Contains(c));
        p.Note($"カテゴリ {category.Category.CategoryGuid}・進行中 {current.ChallengeGuid}");

        // 6. T で一覧を開き、枠・先頭カテゴリのボタン・進行中のカードが UITK のロケータで解ける
        // 6. Open the list with T; the frame, first category button and current card resolve through the UITK locator
        await p.PressKey(Key.T);
        await p.WaitUiState(UIStateEnum.ChallengeList, 10f);
        await p.UntilUiElement("challenge-panel", 5f);
        await p.UntilUiElement($"challenge-category-{category.Category.CategoryGuid}", 5f);
        await p.UntilUiElement($"challenge-node-{current.ChallengeGuid}", 5f);
        var cardTexts = root.Q($"challenge-node-{current.ChallengeGuid}").Query<Label>().ToList().Select(PlainText).Where(text => !string.IsNullOrEmpty(text)).ToList();
        p.Note($"進行中のカードの文字 {string.Join(" / ", cardTexts)}");
        p.Assert(cardTexts.Count > 0, "進行中のカードに文字が出る");
        await p.WaitSeconds(1f);
        await p.Screenshot("02-challenge-list-japanese");

        // 7. T で閉じてゲーム画面へ戻る
        // 7. Close with T and return to the game screen
        await p.PressKey(Key.T);
        await p.WaitUiState(UIStateEnum.GameScreen, 10f);
        await p.Screenshot("03-after-close");

        // 言語を戻せたときだけ控えを消す。戻せなければ控えを残し、運転役がもう一度戻して結果を出す
        // Delete the note only when the language came back; otherwise keep it so the driver retries and reports
        var languageRestored = Localize.TrySetChosenLanguage(languageBefore);
        p.Note($"選ぶ前の言語={languageBefore}（戻せた={languageRestored}）");
        p.Assert(languageRestored, $"選ぶ前の言語 {languageBefore} へ戻せる");
        if (languageRestored) File.Delete(languageBeforePath);
    }
    finally
    {
        // 途中の時間切れで抜けても購読を残さない（前例 uitk-machine-ui.cs）
        // Never leave the subscriptions behind, even when a timeout exits midway (precedent uitk-machine-ui.cs)
        Application.logMessageReceived -= CountLog;
        initializedSubscription.Dispose();
    }

    // 8. 初期化の後のエラーと Web ロケータのログが 0 件（補助の証拠。主の証拠は locator の行と Hub == null）
    // 8. Zero errors after initialization and zero Web-locator logs (supporting evidence; the main evidence is the locator line and Hub == null)
    p.Note($"初期化の後のエラー {errorsAfterInitialization.Count} 件・Web ロケータのログ {webLocatorLogs.Count} 件");
    p.Assert(errorsAfterInitialization.Count == 0, $"初期化の後の Error/Exception が 0 件（最初: {errorsAfterInitialization.FirstOrDefault()}）");
    p.Assert(webLocatorLogs.Count == 0, $"Web ロケータのログが 0 件（最初: {webLocatorLogs.FirstOrDefault()}）");

    #region Internal

    void CountLog(string condition, string stackTrace, LogType type)
    {
        if (initialized && (type == LogType.Error || type == LogType.Exception)) errorsAfterInitialization.Add(condition);
        if (condition.StartsWith("[PlaytestUi] Web")) webLocatorLogs.Add(condition);
    }

    string LanguageBeforePath()
    {
        return Path.Combine(Application.dataPath, "..", "PlaytestResults", "scratch", "event-language-gate", "language-before.txt");
    }

    // LocalizedLabel の表示文は行送りの印を前に付ける（I-fix5）ので、原文から印を外して読む
    // LocalizedLabel's display text carries a line-height tag (I-fix5), so read the source with tags dropped
    string PlainText(Label label)
    {
        var text = label is LocalizedLabel { SourceText: not null } localized ? localized.SourceText : label.text;
        return Regex.Replace(text ?? string.Empty, "<[^>]+>", string.Empty);
    }

    #endregion
});
