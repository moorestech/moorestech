// C2-b の IME 照合（albof.12・plan §4.9）: Mac の OS 段の合成キーでバグ報告の本文欄に日本語を打ち、全選択・下線・候補窓・確定・変換中の ESC・確定後の ESC を確かめる
// C2-b IME check (albof.12, plan §4.9): type Japanese into the bug-report body with OS-level synthetic keys on Mac and verify select-all, underline, candidate window, commit, ESC while composing and ESC after commit
// 単体では走らせない。uitk-c2b-ime-mac-keys.sh が run-scenario.sh でこれを起動し、step-<n>.keys に書いた key code を osascript で送り step-<n>.done を返す
// Never run alone: uitk-c2b-ime-mac-keys.sh boots this through run-scenario.sh, sends the key codes written to step-<n>.keys via osascript and answers with step-<n>.done
// 前提: 全体スイッチを UITK にする手順は uitk-lifeline.cs の冒頭コメントと同じ。macOS の入力ソースに日本語（ローマ字入力）があること
// Precondition: set the global switch to UITK as in the uitk-lifeline.cs header; macOS must have the Japanese (Romaji) input source
// 不変条件（unity-playmode-recorded-playtest 規則 2 の例外。根拠 plan §4.9・S9・D-10）: OS 段は key code のキーボードだけで、OS のマウスは使わない
// Invariant (exception to unity-playmode-recorded-playtest rule 2; basis plan §4.9, S9, D-10): the OS level sends keyboard key codes only, never the OS mouse
// 不変条件: 最初の SendKeys の後は p.PressKey・p.ClickUi・p.SetText などの InputSystem 注入を呼ばない（注入の前半と OS 段の後半を混ぜない）
// Invariant: after the first SendKeys never call InputSystem injection such as p.PressKey, p.ClickUi or p.SetText (keep the injected first half and the OS second half apart)
using System;
using System.IO;
using System.Linq;
using Client.Game.InGame.UI.UIState;
using Client.Input;
using Client.Playtest;
using Client.Playtest.Input;
using Client.Ui.Components.Controls;
using Client.Ui.Foundation.Root;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

var options = new PlaytestRunOptions { Record = true };
return PlaytestRunner.Run("uitk-c2b-ime-mac", options, async p =>
{
    if (Application.platform != RuntimePlatform.OSXEditor) throw new InvalidOperationException("Mac の Editor でだけ走らせる（osascript の合成キーを使う）");
    var markers = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "PlaytestResults", "scratch", "ime-mac"));
    var pid = System.Diagnostics.Process.GetCurrentProcess().Id;
    var step = 0;

    // 背景 Editor でも注入入力が届くよう焦点無視へ切り替える（ポーズを開く ESC とクリックは InputSystem の注入）
    // Ignore focus so injected input reaches a background Editor (the pause ESC and clicks are InputSystem injection)
    InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
    InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
    SemanticInput.EnsureDevices();
    InputSystem.EnableDevice(Keyboard.current);
    InputSystem.EnableDevice(Mouse.current);
    await p.SetupDebugEnvironment(new PlaytestEnvironmentConfig());
    await p.SkipOpeningSkitIfPlaying();
    await p.ExitToGameScreen();

    // 日本語の表示でバグ報告を開く（Web の 1-bug-report.png と同じ状態）
    // Open the bug report in Japanese (same state as the Web 1-bug-report.png)
    await p.PressKey(Key.Escape);
    await p.WaitUiState(UIStateEnum.PauseMenu, 10f);
    await p.ClickUi("pause-menu-open-settings");
    await p.ClickUi("language-select-option-japanese");
    await p.UntilUiElement("pause-menu-locale-japanese", 15f);
    await p.ClickUi("pause-menu-back");
    await p.ClickUi("pause-menu-open-bug-report");
    await p.UntilUiElement("bug-report-description", 10f);
    await p.Screenshot("01-bug-report-empty");

    // ② 7・2: 既存の本文をクリックでフォーカスしても全選択にならず、IME が On になる
    // (2) items 7 and 2: click-focusing existing text does not select all, and IME turns On
    await p.SetText("bug-report-description", "あいう");
    await p.ClickUi("bug-report-description");
    var field = Field();
    p.Assert(field.textSelection.cursorIndex == field.textSelection.selectIndex, "7 クリックのフォーカスで全選択にならない");
    p.Assert(UiInputExclusivity.IsTextInputFocused, "2 欄のフォーカスで入力排他が立つ");
    p.Assert(UnityEngine.Input.imeCompositionMode == IMECompositionMode.On, "2 欄のフォーカスで IME が On");

    // Mac の Editor は compositionCursorPos を Game ビューの表示倍率を掛けずに読むので、等倍で映してから候補窓を開く（縮小表示だと窓が右上へずれる）
    // The Mac Editor reads compositionCursorPos without the Game view zoom, so show it at 1x before opening the candidate window (a shrunk view shifts the window up-right)
    var layoutBefore = ShowGameViewAtActualSize();
    await p.WaitSeconds(0.5f);
    SnapGameViewZoom(1f);
    await p.WaitSeconds(0.5f);
    p.Assert(Mathf.Approximately(GameViewZoom(), 1f), $"5 Game ビューを等倍で映した（今 {GameViewZoom()} 倍）");

    // ③ 6・5: かなへ切り替えて nihongo と Space（候補窓を開く）。運転役が送った直後の画面全体を step-1.png に撮る
    // (3) items 6 and 5: switch to kana, type nihongo and Space (opens the candidate window); the driver captures the whole screen as step-1.png
    await SendKeys(104, 45, 34, 4, 31, 45, 5, 31, 49);
    p.Assert(UnityEngine.Input.compositionString.Length > 0, "変換中の文字列がある（OS の IME を通った）");
    p.Assert(Underline().resolvedStyle.display == DisplayStyle.Flex, "6 変換中の文字に下線が出る");
    p.Assert(CandidateWindowFollowsCaret(), $"5 候補窓の位置がキャレットのスクリーン座標（{UnityEngine.Input.compositionCursorPos}）");
    await p.Screenshot("02-composing");

    // 候補窓を開いた後は Return 1 回目で窓が閉じ、2 回目で確定する（S9 のネイティブ対照と同じ）。残っていれば 2 回目を送る
    // After opening the window the first Return closes it and the second commits (same as the S9 native baseline); send the second only if needed
    await SendKeys(36);
    if (UnityEngine.Input.compositionString.Length > 0) await SendKeys(36);
    p.Assert(UnityEngine.Input.compositionString.Length == 0, "Return で確定した");
    var committed = field.value;
    p.Assert(committed.StartsWith("あいう") && committed.Length > 3, $"確定した文字が値に入った（{committed}）");
    p.Note($"確定後の値: {committed}");

    // ④ 8・3: kai を打って変換中に ESC。1 回で全部消えるか（Unity）2 段か（ネイティブ）を記録し、値は確定済みのまま・フォーカスは残る・ポーズは閉じない
    // (4) items 8 and 3: type kai and ESC while composing; record whether one press clears all (Unity) or two stages (native); the value stays committed, focus stays and the pause stays open
    await SendKeys(40, 0, 34, 53);
    var afterFirstEscape = UnityEngine.Input.compositionString;
    p.Note($"8 変換中の ESC 1 回目の後の未確定文字列: '{afterFirstEscape}'（空なら 1 回で全部取り消える）");
    if (afterFirstEscape.Length > 0) await SendKeys(53);
    p.Assert(UnityEngine.Input.compositionString.Length == 0, "変換中の ESC で変換が取り消えた");
    p.Assert(field.value == committed, $"変換中の ESC で値が確定済みの値のまま（今 '{field.value}' / 確定 '{committed}'）");
    p.Assert(IsFieldFocused(field), "変換中の ESC ではフォーカスが外れない");
    await p.WaitUiState(UIStateEnum.PauseMenu, 2f);
    await p.Screenshot("03-after-composing-escape");

    // ⑤ 1・2・4: 確定後の ESC は値を保ったままフォーカスだけ外し、戻らない。IME は Off、ポーズは閉じない
    // (5) items 1, 2 and 4: ESC after commit drops only the focus, keeps the value and never refocuses; IME goes Off and the pause stays open
    await SendKeys(53);
    await p.WaitSeconds(0.5f);
    p.Assert(field.value == committed, $"1 確定後の ESC で値が戻らない（今 '{field.value}'）");
    p.Assert(!IsFieldFocused(field), "1 確定後の ESC でフォーカスが外れ、戻らない");
    p.Assert(!UiInputExclusivity.IsTextInputFocused, "2 フォーカスが外れたら入力排他が下りる");
    p.Assert(UnityEngine.Input.imeCompositionMode == IMECompositionMode.Off, "2・4 フォーカスが外れたら IME は Off");
    await p.WaitUiState(UIStateEnum.PauseMenu, 2f);
    await p.Screenshot("04-after-escape");
    RestoreLayout(layoutBefore);
    File.WriteAllText(Path.Combine(markers, "finish"), "ok");

    #region Internal

    TextField Field() => GameUiRoot.Instance.GetRootElement().Q<TextField>("bug-report-description");

    // 根の最初の GameTextField は建築メニューの検索欄なので、本文欄を包む GameTextField から引く
    // The root's first GameTextField is the build menu search box, so look inside the one wrapping the body field
    VisualElement Underline() => Field().GetFirstAncestorOfType<GameTextField>().Q(className: "game-text-field__composition-underline");

    bool IsFieldFocused(TextField target)
    {
        for (var current = target.focusController.focusedElement as VisualElement; current != null; current = current.hierarchy.parent)
        {
            if (current == target) return true;
        }
        return false;
    }

    bool CandidateWindowFollowsCaret()
    {
        var target = Field();
        var caretPanelPoint = target.Q<TextElement>().LocalToWorld(target.textSelection.cursorPosition);
        var expected = ImeCaretScreenPosition.FromPanel(caretPanelPoint, target.panel.visualTree.layout.size, new Vector2(Screen.width, Screen.height), ImeCaretScreenPosition.PlatformOrigin);
        return Vector2.Distance(expected, UnityEngine.Input.compositionCursorPos) <= 1f;
    }

    void FocusGameView()
    {
        // OS のキーは前面の窓のキーボードの焦点へ届くので、Game ビューを選んで焦点を置く（prepare-editor-input.edc と同じ）
        // OS keys reach the key window's keyboard focus, so select the Game view and focus it (same as prepare-editor-input.edc)
        var gameView = GameView();
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public;
        var dock = typeof(UnityEditor.EditorWindow).GetField("m_Parent", flags).GetValue(gameView);
        // 最大化中の Game ビューはタブの無い窓に載るので、タブを選ぶのはドックにいるときだけ
        // A maximized Game view sits in a tabless host, so pick the tab only while docked
        var panesField = dock.GetType().GetField("m_Panes", flags);
        if (panesField != null) dock.GetType().GetProperty("selected", flags).SetValue(dock, ((System.Collections.IList)panesField.GetValue(dock)).IndexOf(gameView));
        gameView.Focus();
    }

    UnityEditor.EditorWindow GameView() => Resources.FindObjectsOfTypeAll<UnityEditor.EditorWindow>().First(w => w.GetType().Name == "GameView");

    (Rect mainWindow, float zoom) ShowGameViewAtActualSize()
    {
        // 主窓を画面いっぱいにして Game ビューを最大化し、1280x720 が等倍で収まる広さにする（終わりに戻す）。途中で落ちても運転役が戻せるよう、拡大の前に戻す値を layout-before.txt へ書く
        // Fill the screen with the main window and maximize the Game view so 1280x720 fits at 1x (restored at the end); write the values to restore to layout-before.txt before enlarging so the driver can restore after a mid-run failure
        var before = (UnityEditor.EditorGUIUtility.GetMainWindowPosition(), GameViewZoom());
        File.WriteAllText(Path.Combine(markers, "layout-before.txt"), FormattableString.Invariant($"{before.Item1.x} {before.Item1.y} {before.Item1.width} {before.Item1.height} {before.Item2}"));
        UnityEditor.EditorGUIUtility.SetMainWindowPosition(new Rect(0, 0, Screen.currentResolution.width, Screen.currentResolution.height));
        GameView().maximized = true;
        return before;
    }

    void RestoreLayout((Rect mainWindow, float zoom) before)
    {
        GameView().maximized = false;
        UnityEditor.EditorGUIUtility.SetMainWindowPosition(before.mainWindow);
        SnapGameViewZoom(before.zoom);
        File.Delete(Path.Combine(markers, "layout-before.txt"));
    }

    float GameViewZoom() => ((Vector2)GameView().GetType().GetProperty("zoomAreaScale", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(GameView())).x;

    void SnapGameViewZoom(float zoom) => GameView().GetType().GetMethod("SnapZoom", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(GameView(), new object[] { zoom });

    async UniTask SendKeys(params int[] keyCodes)
    {
        // 運転役へ key code を渡し、送り終わりの返事を待つ。書きかけを読まれないよう一時ファイルから移す
        // Hand key codes to the driver and wait for its reply; move from a temp file so a half-written list is never read
        FocusGameView();
        step++;
        var keysPath = Path.Combine(markers, $"step-{step}.keys");
        var donePath = Path.Combine(markers, $"step-{step}.done");
        File.WriteAllLines(keysPath + ".tmp", new[] { pid.ToString() }.Concat(keyCodes.Select(code => code.ToString())));
        File.Move(keysPath + ".tmp", keysPath);
        await p.Until(() => File.Exists(donePath), 60f, $"step {step} の合成キーが送られた");
        var outcome = File.ReadAllText(donePath).Trim();
        if (outcome != "sent") throw new InvalidOperationException($"step {step} の合成キーを送れなかった: {outcome}");
        await p.WaitSeconds(0.5f);
    }

    #endregion
});
