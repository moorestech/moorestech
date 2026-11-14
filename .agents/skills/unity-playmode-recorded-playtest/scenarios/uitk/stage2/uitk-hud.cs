// 常駐 HUD（S2-hud）: Web ホスト無しで、ゲーム画面の HUD・採掘進捗・インベントリとポーズの幕を UITK で出して録画する
// Resident HUDs (S2-hud): with no web host, show the game-screen HUDs, mining progress and the inventory and pause backdrops in UITK and record them
// 実行前に PlayMode を止め、execute-dynamic-code で UiSystemEditorSwitch.Set(UiSystem.GameUi) を立ててから run-scenario.sh を呼ぶ
// Stop PlayMode and call UiSystemEditorSwitch.Set(UiSystem.GameUi) via execute-dynamic-code before run-scenario.sh
using System;
using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.Map.MapObject;
using Client.Game.InGame.Interact.Selection;
using Client.Game.InGame.UI.UIState;
using Client.Playtest;
using Client.Playtest.Input;
using Client.Playtest.Operations;
using Client.Skit.UI;
using Client.Ui.Foundation.Layers;
using Client.Ui.Foundation.Root;
using Client.Ui.Foundation.Stage;
using Client.Ui.Screens.Challenge.CurrentHud;
using Client.Ui.State.Challenge;
using Client.Ui.State.Text;
using Client.WebUiHost.Boot;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

var options = new PlaytestRunOptions { Record = true };
return PlaytestRunner.Run("uitk-hud", options, async p =>
{
    // 背景Editorでも注入入力が届くよう焦点無視へ切り替える
    // Ignore focus so injected input reaches a background Editor
    InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
    InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
    SemanticInput.EnsureDevices();
    InputSystem.EnableDevice(Keyboard.current);
    InputSystem.EnableDevice(Mouse.current);

    await p.SetupDebugEnvironment(new PlaytestEnvironmentConfig());

    // 開幕の blocking スキット中はチャレンジ HUD とキーヒントが退き、飛ばすと戻る
    // During the opening blocking skit the challenge HUD and key hints step back, and return once skipped
    p.Note("開幕 blocking スキット中の HUD");
    await p.Until(() => SkitPresentationStateStore.Instance.GetCurrent()?.PresentationState?.Mode == SkitPresentationData.BlockingMode, 30f, "開幕 blocking スキットが始まった");
    await p.WaitSeconds(0.5f);
    p.Assert(!IsShown(UiLayer.Chrome, UiFamily.Viewport, "challenge-hud"), "blocking スキット中はチャレンジ HUD が出ない");
    p.Assert(!IsShown(UiLayer.Chrome, UiFamily.Viewport, "key-hints"), "blocking スキット中はキーヒントが出ない");
    await p.Screenshot("00-blocking-skit");
    await p.SkipOpeningSkitIfPlaying();
    await p.ExitToGameScreen();
    p.Assert(WebUiHost.Hub == null, "Webホストは起動していない（hostless）");

    // ゲーム画面: 一人称へ切り替え、クロスヘア・進行中チャレンジ・キーヒント・ヴィネットが出て、幕は出ない
    // Game screen: switch to first person; crosshair, current challenge, key hints and vignette show; no backdrop
    p.Note("ゲーム画面の常駐 HUD（一人称でクロスヘア）");
    await p.Until(() => IsShown(UiLayer.Chrome, UiFamily.Viewport, "challenge-hud"), 10f, "challenge-hud が出た");
    await p.Until(() => IsShown(UiLayer.Chrome, UiFamily.Viewport, "key-hints"), 10f, "key-hints が出た");
    // 注入カーソルの印が画面中央のクロスヘアに重ならないよう、カーソルを右上へ退ける
    // Move the injected cursor marker to the upper right so it does not cover the center crosshair
    await SemanticInput.MouseGlideTo(new Vector2(Screen.width * 0.85f, Screen.height * 0.7f), 0.3f);
    await p.PressKey(Key.V);
    await p.Until(() => IsShown(UiLayer.ViewportStage, UiFamily.Stage, "crosshair"), 10f, "一人称でクロスヘアが出た");
    p.Assert(IsShown(UiLayer.Backdrop, UiFamily.Viewport, "screen-vignette"), "ヴィネットが出ている");
    p.Assert(!IsShown(UiLayer.Backdrop, UiFamily.Viewport, "screen-backdrop"), "ゲーム画面では幕が出ない");
    await p.Screenshot("01-game-hud");
    await p.PressKey(Key.V);
    await p.Until(() => !IsShown(UiLayer.ViewportStage, UiFamily.Stage, "crosshair"), 10f, "三人称へ戻すとクロスヘアが消えた");

    // 削除モード: キーヒントの底が警告帯の高さだけ持ち上がる
    // Delete mode: the key hints' bottom rises by the warning band height
    p.Note("削除モードでキーヒントが持ち上がる");
    var gameScreenHintBottom = HintBottom();
    await p.PressKey(Key.G);
    await p.WaitUiState(UIStateEnum.DeleteBar, 10f);
    await p.WaitSeconds(0.5f);
    var deleteModeHintBottom = HintBottom();
    p.Note($"キーヒントの底 game={gameScreenHintBottom:F1} delete={deleteModeHintBottom:F1}");
    p.Assert(deleteModeHintBottom < gameScreenHintBottom - 1f, "削除モードではキーヒントの底が上がる");
    await p.Screenshot("02-delete-mode-key-hints");
    await p.PressKey(Key.G);
    await p.WaitUiState(UIStateEnum.GameScreen, 10f);

    // チャレンジ完了: 原木 3 個で「原木を3個入手する」が終わり、目標が次へ差し替わる
    // Challenge completion: three logs finish the first challenge and the objective is replaced by the next
    p.Note("チャレンジ完了で目標が差し替わる");
    var objectiveBefore = ChallengeHudText();
    await p.GiveItem("原木", 3);
    await p.Until(() => ChallengeHudText() != objectiveBefore && IsShown(UiLayer.Chrome, UiFamily.Viewport, "challenge-hud"), 15f, "チャレンジ HUD の目標が差し替わった");
    p.Note($"目標 '{objectiveBefore}' → '{ChallengeHudText()}'");
    await p.Screenshot("03-challenge-completed");

    // 採掘: 石の斧を装備枠へ移して選び、最寄りの木を照準して F（Interact）を押し続け、進捗のゲージを出す
    // Mining: equip and select the stone axe, aim at the nearest tree and hold F (Interact) to show the progress gauge
    p.Note("木を採掘して採掘進捗を出す");
    await p.GiveItem("石の斧", 1);
    await p.EquipItem("石の斧", 0);
    var datastore = UnityEngine.Object.FindFirstObjectByType<MapObjectGameObjectDatastore>();
    // 生成マップに「木」は無いので、石の斧で切れる針葉樹（Fir1・Pine1・Pine3・Spruce1。map.json）も候補にする
    // The generated map has no "木", so also accept the axe-minable conifers (Fir1, Pine1, Pine3, Spruce1 in map.json)
    var treeGuids = new HashSet<Guid> { new("6a53fef8-2cf5-41fe-9922-21fd7dd4ab6c"), new("5e9f3f62-1b58-5882-b140-e2d49421ed75"), new("f0818469-adca-5745-86b7-13b4b703f01c"), new("63d93caa-3573-59c0-ae20-0ee70030140d"), new("ce5c145a-300d-5e8d-9156-3d6190d65a56") };
    var tree = datastore.SearchNearestMapObject(treeGuids, p.PlayerPosition);
    p.Assert(tree != null, "最寄りの木が見つかった");
    var treeCollider = tree.GetComponentInChildren<Collider>(true);

    // 到達距離（InteractOverlap.InteractDistance = 2m）の内へ寄り、本番の選定器が木を選ぶまで待つ
    // Move within reach (InteractOverlap.InteractDistance = 2m) and wait until the production selector picks the tree
    await p.Until(() => Camera.main != null, 10f, "MainCameraの起動");
    var cameraForward = Vector3.ProjectOnPlane(Camera.main.transform.forward, Vector3.up).normalized;
    if (cameraForward.sqrMagnitude < 0.1f) cameraForward = Vector3.forward;
    p.WarpPlayer(tree.transform.position - cameraForward * 1.4f + Vector3.up * 0.5f);
    await p.WaitSeconds(0.5f);
    await p.AimAt(treeCollider.bounds.center);
    var selector = new InteractTargetSelector();
    await p.Until(() => ReferenceEquals(selector.Scan().Primary, tree), 10f, "本番の選定器が木を選んだ");
    SemanticInput.KeyDown(Key.F);
    await p.Until(() => IsUiFound("progress-bar"), 10f, "progress-bar が出た");
    await p.Screenshot("04-mining");
    SemanticInput.KeyUp(Key.F);

    // Tab でインベントリ: 幕がヴィネットの手前に出る
    // Tab opens the inventory: the backdrop shows in front of the vignette
    p.Note("インベントリの幕");
    await p.PressKey(Key.Tab);
    await p.WaitUiState(UIStateEnum.PlayerInventory, 10f);
    await p.Until(() => IsShown(UiLayer.Backdrop, UiFamily.Viewport, "screen-backdrop"), 10f, "インベントリで幕が出る");
    await p.Screenshot("05-inventory-backdrop");
    await p.PressKey(Key.Tab);
    await p.WaitUiState(UIStateEnum.GameScreen, 10f);

    // Esc でポーズ: 幕が出る
    // Esc opens the pause: the backdrop shows
    p.Note("ポーズの幕");
    await p.PressKey(Key.Escape);
    await p.WaitUiState(UIStateEnum.PauseMenu, 10f);
    await p.Until(() => IsShown(UiLayer.Backdrop, UiFamily.Viewport, "screen-backdrop"), 10f, "ポーズで幕が出る");
    await p.Screenshot("06-pause-backdrop");
    await p.PressKey(Key.Escape);
    await p.WaitUiState(UIStateEnum.GameScreen, 10f);
    p.Assert(!IsShown(UiLayer.Backdrop, UiFamily.Viewport, "screen-backdrop"), "閉じると幕が消える");

    // 長文 3 件のチャレンジ HUD（Web の参照 2-challenge-hud-multiple-long と同じ文言）。マスタに無い文なので View へ直に描く撮影用の状態
    // Challenge HUD with three long objectives (same text as the Web reference 2-challenge-hud-multiple-long); not in the masters, so drawn straight into the View for the capture
    p.Note("長文 3 件のチャレンジ HUD");
    var challengeHud = GameUiRoot.Instance.GetLayer(UiLayer.Chrome, UiFamily.Viewport).Q<CurrentChallengeHudView>("challenge-hud");
    var longObjectives = new[]
    {
        "地下深くにある非常に長い名前の鉱床を見つけて必要な石を採掘する",
        "遠方の森林から建築に必要な木材を伐採して拠点まで運搬する",
        "VeryLongUnbrokenSecondaryObjectiveTextThatMustAlsoWrapInsideTheHud",
    };
    challengeHud.Render(CurrentChallengeHudViewModel.Create(longObjectives.Select(UiText.FromVerbatim).ToArray(), false));
    await p.WaitSeconds(0.5f);
    await p.Screenshot("07-challenge-multiple-long");

    // 祖先まで display が Flex か（testid を持たない要素は FindUi で引けないので層から引く）
    // Whether the element and its ancestors are Flex (elements without a testid cannot be found by FindUi, so read from the layer)
    bool IsShown(UiLayer layer, UiFamily family, string name)
    {
        var element = GameUiRoot.Instance.GetLayer(layer, family).Q(name);
        for (var current = element; current != null; current = current.parent)
        {
            if (current.resolvedStyle.display == DisplayStyle.None) return false;
        }
        return element != null;
    }

    // UITK の照会は同期で終わるので、待ちの条件の中で結果をその場で読む
    // UITK lookups finish synchronously, so read the result in place inside a wait condition
    bool IsUiFound(string testid) => PlaytestUiElementOps.FindUi(testid).GetAwaiter().GetResult().IsFound;

    float HintBottom() => GameUiRoot.Instance.GetLayer(UiLayer.Chrome, UiFamily.Viewport).Q("key-hints").worldBound.yMax;

    string ChallengeHudText() => PlaytestUiElementOps.FindUi("challenge-hud").GetAwaiter().GetResult().Text;
});
