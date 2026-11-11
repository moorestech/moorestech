// スキット（S2-skit）: Web ホスト無しで、会話窓のクリック・Enter・自動送り・UI 非表示と復帰・選択肢・Ctrl+U とカットシーンの全体非表示を UITK で動かして録画する
// Skit (S2-skit): with no web host, drive the window click, Enter, auto-advance, hide/restore UI, choices and Ctrl+U/cutscene whole-UI hiding in UITK and record them
// 実行前に PlayMode を止め、execute-dynamic-code で UiSystemEditorSwitch.Set(UiSystem.GameUi) を立ててから run-scenario.sh を呼ぶ
// Stop PlayMode and call UiSystemEditorSwitch.Set(UiSystem.GameUi) via execute-dynamic-code before run-scenario.sh
using Client.Game.InGame.UI.UIState;
using Client.Input;
using Client.Playtest;
using Client.Playtest.Input;
using Client.Playtest.Operations;
using Client.Skit.UI;
using Client.Ui.Foundation.Root;
using Client.WebUiHost.Boot;
using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using UniRx;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Playables;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

var options = new PlaytestRunOptions { Record = true };
return PlaytestRunner.Run("uitk-skit", options, async p =>
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
    var tabFlags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public;
    var gameView = System.Array.Find(Resources.FindObjectsOfTypeAll<UnityEditor.EditorWindow>(), w => w.GetType().Name == "GameView");
    var dockArea = typeof(UnityEditor.EditorWindow).GetField("m_Parent", tabFlags).GetValue(gameView);
    dockArea.GetType().GetProperty("selected", tabFlags).SetValue(dockArea, ((System.Collections.IList)dockArea.GetType().GetField("m_Panes", tabFlags).GetValue(dockArea)).IndexOf(gameView));
    await p.Until(() => 0f < Time.timeScale, 10f, "Recorderの初回フレーム取得でtimeScaleが戻る");
    await p.SetupDebugEnvironment(new PlaytestEnvironmentConfig());
    p.Assert(WebUiHost.Hub == null, "Webホストは起動していない（hostless）");
    var store = SkitPresentationStateStore.Instance;
    var skitManager = Object.FindFirstObjectByType<Client.Game.Skit.SkitManager>(FindObjectsInactive.Include);
    var backgroundSkit = Object.FindFirstObjectByType<Client.Game.InGame.BackgroundSkit.BackgroundSkitManager>(FindObjectsInactive.Include);

    // 開幕スキットと続く背景スキットを終わらせてから直接起動する（xcvd の回避。uitk-nested-pause-skit と同じ）
    // Finish the opening skit and the background skit that follows before starting one directly (xcvd workaround, same as uitk-nested-pause-skit)
    await p.SkipOpeningSkitIfPlaying();
    p.Note($"飛ばした直後 timeScale={Time.timeScale} 再生中={skitManager.IsPlayingSkit} UI={p.CurrentUiState}");
    await p.Until(() => !skitManager.IsPlayingSkit && p.CurrentUiState == UIStateEnum.GameScreen, 60f, "開幕スキットが終わりゲーム画面");
    await p.WaitSeconds(1f);
    await p.Until(() => !backgroundSkit.IsPlayingSkit && !skitManager.IsPlayingSkit, 120f, "背景スキットも終わる");

    p.Note("開幕スキットを直接起動する");
    skitManager.StartSkit("Vanilla/Skit/skits/100_start_game").Forget();
    await p.Until(() => store.GetCurrent().PresentationState.Mode == SkitPresentationData.BlockingMode, 60f, "スキット(blocking)開始待ち");
    await p.Until(() => store.GetCurrent().SceneRevision >= 3 && store.GetCurrent().PresentationState.TextAreaVisible, 30f, "会話の文が出て送り待ちになる");
    await p.UntilUiElement("blocking-skit", 10f);

    // 自動送りは UITK のツールで切る（自動だと録画中に進む）
    // Turn auto-advance off with the UITK tool (auto would advance during recording)
    if (store.GetCurrent().PresentationState.AutoEnabled)
    {
        await p.ClickUi("skit-tool-auto");
        await p.Until(() => !store.GetCurrent().PresentationState.AutoEnabled, 5f, "UITK の自動ボタンで自動送りが切れる");
    }
    await p.WaitSeconds(1.5f);
    await p.Screenshot("01-skit-text");

    // 窓のクリックで送る（打ち出し中なら 1 回目は全文表示）
    // Advance by clicking the window (the first click reveals everything if still typing)
    var beforeClick = store.GetCurrent().SceneRevision;
    for (var i = 0; i < 2 && store.GetCurrent().SceneRevision == beforeClick; i++)
    {
        await p.ClickUi("blocking-skit");
        await p.WaitSeconds(0.5f);
    }
    p.Assert(store.GetCurrent().SceneRevision > beforeClick, "窓のクリックで送られた");

    // Enter で送る。Space では送らない（Web skit.spec の 2 件目）
    // Advance with Enter; Space does not (the second Web skit.spec case)
    await p.Until(() => store.GetCurrent().AllowedIntents.Length > 0 && store.GetCurrent().PresentationState.TextAreaVisible, 30f, "次の文の送り待ち");
    await p.WaitSeconds(2f);
    var beforeSpace = store.GetCurrent().SceneRevision;
    await p.PressKey(Key.Space);
    await p.WaitSeconds(0.5f);
    p.Assert(store.GetCurrent().SceneRevision == beforeSpace, "Space では送らない");
    for (var i = 0; i < 2 && store.GetCurrent().SceneRevision == beforeSpace; i++)
    {
        await p.PressKey(Key.Enter);
        await p.WaitSeconds(0.5f);
    }
    p.Assert(store.GetCurrent().SceneRevision > beforeSpace, "Enter で送られた（Input System の Enter が UITK の会話窓へ届く）");

    // UI 非表示 → 復帰ボタンだけ → 復帰
    // Hide UI -> restore button only -> restore
    await p.Until(() => store.GetCurrent().PresentationState.TextAreaVisible, 30f, "窓が出ている");
    await p.ClickUi("skit-tool-hide-ui");
    await p.Until(() => store.GetCurrent().PresentationState.UiHidden, 5f, "UI 非表示になる");
    await p.UntilUiElement("skit-show-ui", 5f);
    var hiddenWindow = await PlaytestUiElementOps.FindUi("blocking-skit");
    p.Assert(!hiddenWindow.IsFound || !hiddenWindow.Visible, "UI 非表示の間は会話窓が見えない");
    await p.Screenshot("02-skit-ui-hidden");
    await p.ClickUi("skit-show-ui");
    await p.Until(() => !store.GetCurrent().PresentationState.UiHidden, 5f, "復帰ボタンで UI が戻る");

    // Ctrl+U で GameUi のルートごと隠れ、もう一度で戻る（R-10）
    // Ctrl+U hides the GameUi root and pressing it again restores it (R-10)
    var root = GameUiRoot.Instance.GetRootElement();
    await PressCtrlU();
    await p.Until(() => root.resolvedStyle.display == DisplayStyle.None, 5f, "Ctrl+U でルートが隠れる");
    await p.Screenshot("03-whole-ui-hidden");
    await PressCtrlU();
    await p.Until(() => root.resolvedStyle.display == DisplayStyle.Flex, 5f, "Ctrl+U でルートが戻る");

    // 100_start_game に選択肢は無い（実測）。選択肢・乗ったままのポーズと指は uitk-skit-choice-pause-cursor が sample_short で見る
    // 100_start_game has no choices (observed); uitk-skit-choice-pause-cursor covers choices and the pause-over-choice cursor with sample_short

    // 後始末: UITK のスキップでゲーム画面へ
    // Cleanup: skip in UITK back to the game screen
    if (store.GetCurrent().PresentationState.Mode == SkitPresentationData.BlockingMode)
    {
        await p.ClickUi("skit-tool-skip");
    }
    await p.Until(() => p.CurrentUiState == UIStateEnum.GameScreen, 120f, "スキット終了でGameScreen");

    // カットシーン（Timeline の再生）の間も全部隠し（HideAll）でルートごと隠れ、終われば戻る（R-10 の実機の経路。D-46・D-47）
    // A cutscene (Timeline playback) also hides the whole root via HideAll and restores it when it ends (R-10 real path; D-46, D-47)
    var timelinePlayer = Object.FindFirstObjectByType<Client.CutScene.TimelinePlayer>(FindObjectsInactive.Include);
    p.Assert(timelinePlayer != null, "シーンに TimelinePlayer がある（GameSystem.prefab の CutSceneManager）");
    var director = timelinePlayer.GetComponent<PlayableDirector>();
    p.Assert(director != null, "TimelinePlayer と同じ GameObject に PlayableDirector がある");
    // MainGame.unity が CutSceneManager の PlayableDirector を無効で置いていて Play しても再生しない（本番に Play の呼び手は無い）。全体非表示の経路を見るため有効にする
    // MainGame.unity ships CutSceneManager's PlayableDirector disabled so Play does nothing (no production caller of Play); enable it to observe the whole-UI hiding path
    p.Note($"シーンの PlayableDirector.enabled={director.enabled}。有効にしてから再生する");
    director.enabled = true;
    var playingNotices = new List<bool>();
    var noticeSubscription = Client.CutScene.TimelinePlayer.OnPlayingChanged.Subscribe(notice => playingNotices.Add(notice));
    var timeline = ScriptableObject.CreateInstance<UnityEngine.Timeline.TimelineAsset>();
    timeline.durationMode = UnityEngine.Timeline.TimelineAsset.DurationMode.FixedLength;
    timeline.fixedDuration = 2;
    timelinePlayer.Play(timeline).Forget();

    // 4 段（Timeline・再生の通知・ゲーム状態・ルート）を別々に観測し、落ちた段で原因を分ける（D-47）
    // Observe the four stages (Timeline, playing notice, game state, root) separately so the failing stage names the cause (D-47)
    await p.WaitSeconds(0.5f);
    p.Note(CutSceneStages("再生の 0.5 秒後"));
    await p.Until(() => director.state == PlayState.Playing, 5f, "段1 Timeline が再生している");
    await p.Until(() => playingNotices.Contains(true), 5f, "段2 TimelinePlayer が再生中を通知した");
    await p.Until(() => Client.Game.Common.GameStateController.CurrentState == Client.Game.Common.GameStateType.CutScene, 5f, "段3 ゲーム状態がカットシーンになる");
    await p.Until(() => root.resolvedStyle.display == DisplayStyle.None, 5f, "段4 カットシーンでルートが隠れる");
    await p.Screenshot("05-cutscene-hidden");

    // 終わりも同じ 4 段で見る（Timeline の長さは 2 秒）
    // Observe the end through the same four stages (the Timeline lasts 2 seconds)
    await p.WaitSeconds(3f);
    p.Note(CutSceneStages("再生の 3.5 秒後"));
    await p.Until(() => director.state != PlayState.Playing, 15f, "段1 Timeline が止まる");
    await p.Until(() => playingNotices.Count >= 2 && !playingNotices[playingNotices.Count - 1], 5f, "段2 TimelinePlayer が停止を通知した");
    await p.Until(() => Client.Game.Common.GameStateController.CurrentState == Client.Game.Common.GameStateType.InGame, 5f, "段3 ゲーム状態がカットシーンを抜ける");
    await p.Until(() => root.resolvedStyle.display == DisplayStyle.Flex, 5f, "段4 カットシーンが終わるとルートが戻る");
    noticeSubscription.Dispose();

    #region Internal

    async UniTask PressCtrlU()
    {
        SemanticInput.KeyDown(Key.LeftCtrl);
        await SemanticInput.TapKey(Key.U);
        SemanticInput.KeyUp(Key.LeftCtrl);
        await p.WaitSeconds(0.3f);
    }

    // 4 段の今の値を 1 行にする。Until が時間切れで止まっても、どの段まで進んだかが結果に残る
    // Puts the four stages' current values on one line; even if an Until times out, the result shows how far it got
    string CutSceneStages(string when)
    {
        return $"{when}: director={director.state} 通知=[{string.Join(",", playingNotices)}] 状態={Client.Game.Common.GameStateController.CurrentState} ルート={root.resolvedStyle.display} timeScale={Time.timeScale}";
    }

    #endregion
});
