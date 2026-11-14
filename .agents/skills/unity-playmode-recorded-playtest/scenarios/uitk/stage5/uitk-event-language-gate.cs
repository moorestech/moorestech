// 出展モードの言語ゲート（UITK）: 出展モードで起動し、DI より前の全画面ゲートを 1280×720 と 2560×1080 で撮り、日本語を選んで初期化まで通す
// Exhibition language gate (UITK): boot in exhibition mode, capture the pre-DI full-screen gate at 1280x720 and 2560x1080, choose Japanese and run through initialization
// 実行は同じディレクトリの uitk-event-language-gate.sh から（全体スイッチ GameUi と出展モードの武装を済ませてから run-scenario.sh を流す）
// Run via uitk-event-language-gate.sh in this directory (it sets the GameUi switch and arms exhibition mode before run-scenario.sh)
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Client.Game.Common;
using Client.Input;
using Client.Localization;
using Client.Playtest;
using Client.Playtest.Operations.Ui;
using Client.Ui.Foundation.Root;
using Client.Ui.Foundation.Text;
using Cysharp.Threading.Tasks;
using Mooresmaster.Localization.Generated;
using UniRx;
using UnityEngine;
using UnityEngine.UIElements;

var options = new PlaytestRunOptions { Record = true };
return PlaytestRunner.Run("uitk-event-language-gate", options, async p =>
{
    p.Note($"系={UiInputExclusivity.ActiveUiSystem}");
    p.Assert(UiInputExclusivity.ActiveUiSystem == UiSystem.GameUi, "全体スイッチが GameUi");
    var initialized = false;
    var initializedSubscription = GameInitializedEvent.OnGameInitialized.Subscribe(_ => initialized = true);

    // ゲートは DI より前に出て、選ぶまで初期化は止まっている
    // The gate shows before DI and initialization holds until a choice
    await p.UntilUiElement("event-language-gate", 60);
    var root = GameUiRoot.Instance.GetRootElement();
    p.Assert(!initialized, "言語を選ぶまで初期化が完了しない");
    p.Assert(UnityEngine.Cursor.visible && UnityEngine.Cursor.lockState == CursorLockMode.None, "待機中は実カーソルが見えてロックされない");
    // 字だけを比べる（I-fix5 の後の text は行の高さの印 <line-height=…px> を前に付けるので、印を外す。S5-gate D-19）
    // Compare only the words (after I-fix5 the text carries a <line-height=...px> tag up front, so drop tags; S5-gate D-19)
    var titleWords = Regex.Replace(root.Q<Label>("event-language-gate-title").text, "<[^>]+>", string.Empty);
    p.Assert(titleWords == "Select Language", $"見出しは英語固定 ({titleWords})");
    var optionCount = root.Query<Button>().ToList().Count(button => button.name.StartsWith("event-language-gate-option-"));
    p.Assert(optionCount == LanguageCatalog.Languages.Length, $"言語のボタンが一覧の数だけ並ぶ ({optionCount})");

    // sRGB 合成は DI より前の土台が立てた 1 本で、ゲートの間もパネルは RT へ描く
    // The sRGB composite is the single one the pre-DI foundation built; the panel draws into the RT during the gate too
    var gatePanelSettings = GameUiRoot.Instance.GetPanelDocument().panelSettings;
    p.Assert(gatePanelSettings.targetTexture != null, "ゲートの間も合成の RT がある");

    // 選ぶ前の言語を撮影の前に控え、途中で落ちても運転役が戻せるようにする
    // Note the pre-choice language before capturing so the driver can restore it even if this dies midway
    var languageBefore = Localize.GetCurrentLanguageCode();
    var languageBeforePath = LanguageBeforePath();
    Directory.CreateDirectory(Path.GetDirectoryName(languageBeforePath));
    File.WriteAllText(languageBeforePath, languageBefore);

    // 撮影の間だけ参照と同じ言語を当てる（Web の参照は mock-host の japanese。CJK のクラスと字形の優先順を揃える。S5-gate D-22）
    // Apply the reference's language only while capturing (the Web reference is mock-host japanese; match the CJK class and glyph order; S5-gate D-22)
    const string referenceCaptureLanguage = "japanese";
    p.Assert(Localize.TrySetChosenLanguage(referenceCaptureLanguage), $"撮影の言語 {referenceCaptureLanguage} を当てられる");
    p.Assert(root.ClassListContains(GameUiLocaleClass.CjkClassName), "撮影の言語で CJK のロケールのクラスが付く");
    p.Note($"撮影の言語={Localize.GetCurrentLanguageCode()}（参照 mock-host と同じ。撮影の前={languageBefore}）");

    // 撮影の間の合成・倍率の Error を控える（DI 前の Tick が解像度の変化で RT を作り直す経路を見る）
    // Note composite/scale errors during capture (watches the pre-DI Tick rebuilding the RT on resolution change)
    var gateResizeErrors = new System.Collections.Generic.List<string>();
    void OnGateLog(string condition, string stackTrace, LogType type)
    {
        if (type != LogType.Error && type != LogType.Exception) return;
        if (condition.Contains("[UiSrgbPanelTarget]") || condition.Contains("[UiSrgbCompositeSource]") || condition.Contains("[StageScale]")) gateResizeErrors.Add(condition);
    }
    Application.logMessageReceived += OnGateLog;

    // 参照と同じ 2 つの解像度で撮る。2560×1080 でもボタンは実画面 60px
    // Capture at the reference's two resolutions; buttons stay 60 real px at 2560x1080
    await PlaytestGameViewOps.SetResolution(p.CancellationToken, 1280, 720);
    await UniTask.DelayFrame(3);
    await p.Screenshot("01-event-language-gate-1280x720");
    await PlaytestGameViewOps.SetResolution(p.CancellationToken, 2560, 1080);
    await UniTask.DelayFrame(3);
    var japanese = root.Q<Button>("event-language-gate-option-japanese");
    var realHeight = japanese.worldBound.height * japanese.panel.scaledPixelsPerPoint;
    p.Assert(Mathf.Abs(realHeight - 60f) < 1f, $"2560×1080 でもボタンは実画面 60px ({realHeight})");
    await p.Screenshot("02-event-language-gate-2560x1080");

    // 選ぶ前に解像度を 1 回変えて戻す。DI 前の Tick が RT を作り直し、ゲートは写り続ける（2560×1080 へ変えた手も同じ経路を通る）
    // Change the resolution once and back before choosing; the pre-DI Tick rebuilds the RT and the gate stays visible (the switch to 2560x1080 takes the same path)
    await PlaytestGameViewOps.SetResolution(p.CancellationToken, 1280, 720);
    await UniTask.DelayFrame(3);
    Application.logMessageReceived -= OnGateLog;
    var gateTexture = gatePanelSettings.targetTexture;
    p.Assert(root.Q("event-language-gate") != null && !initialized, "解像度を戻した後もゲートが出たまま初期化は止まっている");
    p.Assert(gateTexture != null && gateTexture.width == Screen.width && gateTexture.height == Screen.height, $"ゲートの間も合成の RT が解像度に追従する ({gateTexture?.width}x{gateTexture?.height} / {Screen.width}x{Screen.height})");
    p.Note($"ゲートの間の合成・倍率の Error {gateResizeErrors.Count} 件: {string.Join(" | ", gateResizeErrors.Take(5))}");
    p.Assert(gateResizeErrors.Count == 0, "ゲートの間の解像度の変化で [UiSrgbPanelTarget]/[UiSrgbCompositeSource]/[StageScale] の Error が出ない");
    await p.Screenshot("02b-event-language-gate-back-to-1280x720");

    // 撮影の後に撮影の前の言語へ戻し、戻せたかを戻り値で確かめてから押す（押す前の状態は撮影の前と同じ）
    // After capturing, restore the pre-capture language and check the return value before pressing (the pre-press state matches pre-capture)
    var captureLanguageRestored = Localize.TrySetChosenLanguage(languageBefore);
    p.Assert(captureLanguageRestored, $"撮影の後に撮影の前の言語 {languageBefore} へ戻せる");
    p.Note($"撮影の後の言語={Localize.GetCurrentLanguageCode()}（戻せた={captureLanguageRestored}）");

    // DI 前は背景のまま入力が無効になる（既定の背景動作）ので、押す前に注入をゲームへ通す（prepare-editor-input.edc と同じ設定）
    // Before DI an unfocused Editor leaves devices disabled (default background behavior), so route injection to the game before pressing (same as prepare-editor-input.edc)
    UnityEngine.InputSystem.InputSystem.settings.backgroundBehavior = UnityEngine.InputSystem.InputSettings.BackgroundBehavior.IgnoreFocus;
    UnityEngine.InputSystem.InputSystem.settings.editorInputBehaviorInPlayMode = UnityEngine.InputSystem.InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
    Client.Playtest.Input.SemanticInput.EnsureDevices();
    UnityEngine.InputSystem.InputSystem.EnableDevice(UnityEngine.InputSystem.Mouse.current);
    p.Assert(UnityEngine.InputSystem.Mouse.current.enabled, "押す前にマウスが有効");

    // 日本語を押すと、その場でゲートが外れて初期化が進む
    // Pressing Japanese removes the gate at once and initialization proceeds
    await p.ClickUi("event-language-gate-option-japanese");
    await p.Until(() => root.Q("event-language-gate") == null, 5, "押したその場でゲートが外れる");
    await p.Until(() => initialized, 180, "選択の後に初期化が完了する");
    p.Assert(Localize.GetCurrentLanguageCode() == "japanese", "選んだ言語で始まる");

    // DI はゲートの間と同じ合成をそのまま受け取った（作り直していない）
    // DI received the same composite used during the gate (nothing rebuilt)
    var panelSettings = GameUiRoot.Instance.GetPanelDocument().panelSettings;
    p.Assert(ReferenceEquals(panelSettings, gatePanelSettings) && panelSettings.targetTexture != null, "選択の後も同じ合成のパネル設定が RT を持つ");

    // DI へ渡した後の Tick が回っていれば、解像度を変えると合成の RT が画面の大きさに追従する
    // If the Tick handed to DI runs, changing the resolution makes the composite RT follow the screen size
    await PlaytestGameViewOps.SetResolution(p.CancellationToken, 2560, 1080);
    await UniTask.DelayFrame(3);
    var followed = panelSettings.targetTexture.width == Screen.width && panelSettings.targetTexture.height == Screen.height;
    p.Assert(followed, $"選択の後も合成の RT が解像度に追従する ({panelSettings.targetTexture.width}x{panelSettings.targetTexture.height} / {Screen.width}x{Screen.height})");
    await PlaytestGameViewOps.SetResolution(p.CancellationToken, 1280, 720);
    await UniTask.DelayFrame(3);
    await p.SkipOpeningSkitIfPlaying();
    // 非前面の Editor は CPU が絞られ、飛ばした後の Story を抜けるのに ExitToGameScreen の 10 秒を超えうるので先に待つ
    // An unfocused Editor is CPU-throttled and leaving Story after the skip can exceed ExitToGameScreen's 10 s, so wait first
    await p.Until(() => p.CurrentUiState != Client.Game.InGame.UI.UIState.UIStateEnum.Story, 60, "スキットを飛ばした後に Story を抜ける");
    await p.ExitToGameScreen();
    await p.Screenshot("03-after-gate-game-screen-1280x720");

    // 言語を戻せたときだけ控えを消す。戻せなければ控えを残し、運転役がもう一度戻して結果を出す
    // Delete the note only when the language came back; otherwise keep it so the driver retries and reports
    var languageRestored = Localize.TrySetChosenLanguage(languageBefore);
    p.Assert(languageRestored, $"選ぶ前の言語 {languageBefore} へ戻せる");
    if (languageRestored) File.Delete(languageBeforePath);
    initializedSubscription.Dispose();

    #region Internal

    string LanguageBeforePath()
    {
        return Path.Combine(Application.dataPath, "..", "PlaytestResults", "scratch", "event-language-gate", "language-before.txt");
    }

    #endregion
});
