// S5-crosscut T19 の撮り直し（HUD・ポーズ系）: トースト（フェード後）・バグ報告・長文 3 件のチャレンジ HUD・ホットバーで物を選んだ状態・照準の強調枠とラベルを日本語で 1280×720 と 2560×1080 に撮る（S5-ladder）
// S5-crosscut T19 retake (HUD and pause): shoot the toast after its fade, bug report, three-long-objective challenge HUD, hotbar with an item selected and the crosshair outline with label in Japanese at 1280x720 and 2560x1080 (S5-ladder)
// 実行は stage5/driver/winunity-scenario-run.sh <branch> normal <この .cs> <name> から。画面が出たことだけを主張し、見た目は主張しない
// Run via stage5/driver/winunity-scenario-run.sh <branch> normal <this .cs> <name>; asserts only that each screen shows, never its looks
using System;
using System.Linq;
using Client.Game.InGame.Context;
using Client.Game.InGame.Tutorial;
using Client.Game.InGame.Tutorial.UIHighlight;
using Client.Game.InGame.UI.UIState;
using Client.Localization;
using Client.Playtest;
using Client.Playtest.Input;
using Client.Playtest.Operations;
using Client.Playtest.Operations.Ui;
using Client.Playtest.Overlay;
using Client.Ui.Foundation.Layers;
using Client.Ui.Foundation.Root;
using Client.Ui.Foundation.Stage;
using Client.Ui.Screens.Challenge.CurrentHud;
using Client.Ui.State.Challenge;
using Client.Ui.State.Text;
using Client.Ui.State.Toast;
using Core.Master;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using VContainer;
using Object = UnityEngine.Object;

var options = new PlaytestRunOptions { Record = false, ScenarioTimeoutSeconds = 600f };
return PlaytestRunner.Run("uitk-s5-retake-hud", options, async p =>
{
    // 1. 入力・ゲーム画面・日本語（Windows の PlayerPrefs は前の録画の言語が残るので撮る前に当てる）
    // 1. Input, game screen and Japanese (Windows PlayerPrefs keep the last run's language, so apply it before shooting)
    InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
    InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
    SemanticInput.EnsureDevices();
    foreach (var device in new InputDevice[] { Keyboard.current, Mouse.current }) InputSystem.EnableDevice(device);
    await p.SetupDebugEnvironment(new PlaytestEnvironmentConfig());
    await p.SkipOpeningSkitIfPlaying();
    await p.ExitToGameScreen();
    var languageBefore = Localize.GetCurrentLanguageCode();
    p.Assert(Localize.TrySetChosenLanguage("japanese"), "撮影の言語 japanese を当てられる");
    var backgroundSkit = Object.FindFirstObjectByType<Client.Game.InGame.BackgroundSkit.BackgroundSkitManager>(FindObjectsInactive.Include);
    await p.Until(() => !backgroundSkit.IsPlayingSkit, 120f, "背景スキットが終わる");
    var overlay = Object.FindFirstObjectByType<PlaytestOverlayView>();
    overlay.enabled = false;
    var resolver = ClientDIContext.DIContainer.DIContainerResolver;
    try
    {
        EndTutorialSessions();

        // 2. トースト: 解像度ごとに 1 件出し、フェードの入りが終わって不透明度 1 になってから撮る（R5）
        // 2. Toast: show one per resolution and shoot once the fade-in ends at opacity 1 (R5)
        var toasts = resolver.Resolve<GameUiToastFeed>();
        foreach (var (w, h) in Resolutions())
        {
            await p.Until(() => ToastItems().Count == 0, 15f, "前のトーストが消える");
            await SetResolutionAsync(w, h);
            toasts.Show("block_inventory.split failed: invalid_slot", GameUiToastVariant.Error);
            await p.Until(() => ToastItems().Count == 1 && ToastItems()[0].resolvedStyle.opacity >= 0.999f, 2f, "トーストの不透明度が 1 になる");
            await UniTask.DelayFrame(2);
            p.Note($"toast {w}x{h} opacity={ToastItems()[0].resolvedStyle.opacity:F3}");
            await p.Screenshot($"uitk-1-toast-{w}x{h}");
        }

        // 3. バグ報告のページ
        // 3. The bug-report page
        await p.PressKey(Key.Escape);
        await p.WaitUiState(UIStateEnum.PauseMenu, 10f);
        await p.ClickUi("pause-menu-open-bug-report");
        await p.UntilUiElement("bug-report-description", 10f);
        await MoveCursorAway();
        await ShootBoth("1-bug-report");
        await p.ClickUi("pause-menu-back");
        await p.UntilUiElement("pause-menu-open-settings", 10f);
        await p.PressKey(Key.Escape);
        await p.WaitUiState(UIStateEnum.GameScreen, 10f);

        // 4. 長文 3 件のチャレンジ HUD（前例 stage2/uitk-hud。マスタに無い文なので View へ直に描く）
        // 4. Three-long-objective challenge HUD (precedent stage2/uitk-hud; not in the masters, so drawn into the View)
        var challengeHud = GameUiRoot.Instance.GetLayer(UiLayer.Chrome, UiFamily.Viewport).Q<CurrentChallengeHudView>("challenge-hud");
        var longObjectives = new[] { "地下深くにある非常に長い名前の鉱床を見つけて必要な石を採掘する", "遠方の森林から建築に必要な木材を伐採して拠点まで運搬する", "VeryLongUnbrokenSecondaryObjectiveTextThatMustAlsoWrapInsideTheHud" };
        challengeHud.Render(CurrentChallengeHudViewModel.Create(longObjectives.Select(UiText.FromVerbatim).ToArray(), false));
        await ShootBoth("2-challenge-hud-multiple-long");

        // 5. ホットバーの 1 番に木のチェストを割り当て、数字キー 1 で選ぶ（R7。選択の枠は設置対象の由来から出る）
        // 5. Assign a wooden chest to hotbar slot 1 and select it with key 1 (R7; the selection frame comes from the placement origin)
        await p.Hotbar.AssignHotbar(0, "木のチェスト");
        await p.PressKey(Key.Digit1);
        await p.Until(() => GameUiRoot.Instance.GetRootElement().Q<VisualElement>("hotbar-slot-0")?.ClassListContains("slot-frame--selected") == true, 10f, "hotbar-slot-0 が選択の枠になる");
        await MoveCursorAway();
        await ShootBoth("3-hotbar-selected");
        await p.PressKey(Key.Escape);
        await p.WaitUiState(UIStateEnum.GameScreen, 10f);

        // 6. 一人称のクロスヘアに強調枠とラベル（参照の mock は game.crosshair）。この組だけ枠を外さない
        // 6. Outline with label on the first-person crosshair (the reference mock targets game.crosshair); only this state keeps the outline
        await p.PressKey(Key.V);
        await p.Until(() => Shown("crosshair"), 10f, "一人称でクロスヘアが出る");
        var sessionGuid = Guid.NewGuid();
        var tutorialGuid = MasterHolder.ChallengeMaster.ChallengeCategoryMasterElements[0].Challenges[0].Tutorials[0].TutorialGuid;
        TutorialPresentationStateStore.Instance.BeginSession(sessionGuid);
        TutorialPresentationStateStore.Instance.AddOutlineHighlight(AnchorIds.GameCrosshair, tutorialGuid);
        await p.Until(() => Shown("tutorial-highlight-label"), 10f, "強調枠のラベルが出る");
        await ShootBoth("5-tutorial-outline-label");
        TutorialPresentationStateStore.Instance.EndSession(sessionGuid);
        await p.PressKey(Key.V);
    }
    finally
    {
        // 7. 撮影の前の言語へ戻す（解像度の復元より先。キャンセルで SetResolution が投げても戻せるように）
        // 7. Restore the pre-shoot language before the resolution so a cancelled SetResolution cannot skip it
        overlay.enabled = true;
        var restored = Localize.TrySetChosenLanguage(languageBefore);
        p.Note($"撮影の前の言語={languageBefore}（戻せた={restored}）");
        p.Assert(restored, $"撮影の前の言語 {languageBefore} へ戻せる");
        await PlaytestGameViewOps.SetResolution(p.CancellationToken, 1280, 720);
    }

    #region Internal

    (int, int)[] Resolutions() => new[] { (1280, 720), (2560, 1080) };

    async UniTask ShootBoth(string stateId)
    {
        foreach (var (w, h) in Resolutions())
        {
            await SetResolutionAsync(w, h);
            await p.WaitSeconds(0.5f);
            await p.Screenshot($"uitk-{stateId}-{w}x{h}");
        }
        await SetResolutionAsync(1280, 720);
    }

    async UniTask SetResolutionAsync(int width, int height)
    {
        await PlaytestGameViewOps.SetResolution(p.CancellationToken, width, height);
        await UniTask.DelayFrame(3);
        p.Assert(Screen.width == width && Screen.height == height, $"Screen が {width}x{height} (実際 {Screen.width}x{Screen.height})");
    }

    // 参照に無いチャレンジのチュートリアルの枠を消す（前例 uitk-challenge-list）
    // Drop the challenge tutorial outlines absent from the reference (precedent uitk-challenge-list)
    void EndTutorialSessions()
    {
        foreach (var challenge in MasterHolder.ChallengeMaster.ChallengeCategoryMasterElements.SelectMany(c => c.Challenges)) TutorialPresentationStateStore.Instance.EndSession(challenge.ChallengeGuid);
    }

    async UniTask MoveCursorAway()
    {
        SemanticInput.MouseMoveTo(new Vector2(Screen.width * 0.03f, Screen.height * 0.5f));
        await p.WaitSeconds(0.3f);
    }

    // 要素があり自分と祖先のどれも隠れていない（中心のヒットは問わない。前例 uitk-block-special-panels）
    // The element exists and neither it nor any ancestor is hidden (no centre-hit requirement; precedent uitk-block-special-panels)
    bool Shown(string name)
    {
        var element = GameUiRoot.Instance.GetRootElement().Q<VisualElement>(name);
        if (element == null || element.panel == null) return false;
        for (var e = element; e != null; e = e.parent) if (e.resolvedStyle.display == DisplayStyle.None) return false;
        return true;
    }

    System.Collections.Generic.List<VisualElement> ToastItems() => GameUiRoot.Instance.GetLayer(UiLayer.Toast, UiFamily.Viewport).Query<VisualElement>(className: "toast-item").ToList();

    #endregion
});
