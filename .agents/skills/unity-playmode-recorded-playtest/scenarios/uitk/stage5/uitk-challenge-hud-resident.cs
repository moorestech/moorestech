// 進行中チャレンジ HUD の常駐を UITK（GameUi）で通す（Web challenge.spec「常駐HUDをインベントリ・メニュー・操作モードで維持する」の画面を跨ぐ部分。列車の入れ子ポーズは -train の 1 本）: ゲーム画面の位置のまま各画面とブロックインベントリで出続け、上の安全帯のあるメニューの中身より上、配置 HUD と横に分かれ、削除の帯より下、1024×576 でも同じ論理の位置
// Runs the resident current-challenge HUD on UITK (GameUi) (cross-screen part of the Web challenge.spec resident HUD test; the train nested pause is the -train scenario): same position as the game screen everywhere including the block inventory, above menu content with an upper safe area, beside the placement HUD, below the delete band, same logical position at 1024x576
// 実行前に PlayMode を止め、execute-dynamic-code で UiSystemEditorSwitch.Set(UiSystem.GameUi) を立ててから run-scenario.sh を呼ぶ
// Stop PlayMode and call UiSystemEditorSwitch.Set(UiSystem.GameUi) via execute-dynamic-code before run-scenario.sh
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
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

var chestPosition = new Vector3Int(4, 32, 4);
return PlaytestRunner.Run("uitk-challenge-hud-resident", new PlaytestRunOptions { Record = true }, async p =>
{
    p.Note($"全体スイッチ 系={UiInputExclusivity.ActiveUiSystem}");
    p.Assert(UiInputExclusivity.ActiveUiSystem == UiSystem.GameUi, "全体スイッチが UITK（GameUi）");
    // 背景Editorでも注入入力が届くよう焦点無視へ切り替える
    // Ignore focus so injected input reaches a background Editor
    InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
    InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
    SemanticInput.EnsureDevices();
    InputSystem.EnableDevice(Keyboard.current);
    InputSystem.EnableDevice(Mouse.current);
    // 木のチェストは研究で解放されるので無料設置（全解放）の足場で始める（前例 uitk-mode-hud-and-tooltip）
    // The wooden chest is research-locked, so start on the free-placement scaffold (precedent uitk-mode-hud-and-tooltip)
    await p.SetupDebugEnvironment(new PlaytestEnvironmentConfig());
    await p.SkipOpeningSkitIfPlaying();
    await p.ExitToGameScreen();
    await p.Hotbar.AssignHotbar(0, "木のチェスト");
    var root = GameUiRoot.Instance.GetRootElement();
    var world = await HudRect("ゲーム画面");

    // 上の安全帯のあるメニューは中身より上。研究は持ち物の右を上端まで使うので位置だけ見る（Web の ADR 0014）
    // Menus with an upper safe area keep the HUD above their content; research uses the full height right of the inventory, so only the position is checked (Web ADR 0014)
    await CheckMenu(Key.Tab, UIStateEnum.PlayerInventory, "main-grid", true);
    await CheckMenu(Key.T, UIStateEnum.ChallengeList, "challenge-panel", true);
    await CheckMenu(Key.R, UIStateEnum.ResearchTree, "research-tree", false);
    await CheckMenu(Key.B, UIStateEnum.BuildMenu, "build-menu-panel", true);
    await CheckMenu(Key.Escape, UIStateEnum.PauseMenu, "pause-menu", true);

    // ブロックインベントリ: チェストを開いても持ち物の格子より上（Web の SubInventory・main-grid。開き方は前例 uitk-block-inventory）
    // Block inventory: opening a chest keeps the HUD above the inventory grid (Web SubInventory, main-grid; opening follows uitk-block-inventory)
    await p.PlaceBlockDirect("木のチェスト", chestPosition, BlockDirection.North);
    await p.WaitBlockGameObject(chestPosition);
    p.WarpPlayer(new Vector3(chestPosition.x + 0.5f, 32.5f, chestPosition.z - 1.5f));
    await p.Until(() => Mathf.Abs(p.PlayerPosition.y - 32f) < 0.05f, 5f, "着地");
    SemanticInput.KeyDown(Key.LeftAlt);
    await p.AimAt(new Vector3(chestPosition.x + 0.5f, 32.5f, chestPosition.z));
    await p.PressInteract();
    await p.WaitUiState(UIStateEnum.SubInventory, 10f);
    SemanticInput.KeyUp(Key.LeftAlt);
    await WaitShown("main-grid");
    var subInventory = await HudRect("SubInventory");
    AssertSame(world, subInventory, "SubInventory");
    p.Assert(subInventory.yMax <= root.Q("main-grid").worldBound.yMin + 0.5f, "SubInventory で HUD が main-grid より上");
    // チェストは閉じるボタンで閉じる（前例 uitk-block-inventory）
    // Close the chest with its close button (precedent uitk-block-inventory)
    await p.ClickUi("block-inventory-close");
    await p.WaitUiState(UIStateEnum.GameScreen, 10f);

    // 配置モード: 配置 HUD と横に分かれる
    // Placement mode: the HUD and the placement HUD are separated horizontally
    await p.Hotbar.EnterBuildMode(0);
    await p.WaitUiState(UIStateEnum.PlaceBlock, 15f);
    await WaitShown("placement-mode-hud");
    var placement = root.Q("placement-mode-hud").worldBound;
    var placementHud = await HudRect("配置モード");
    p.Assert(placementHud.xMax <= placement.xMin || placement.xMax <= placementHud.xMin, $"HUD {placementHud} と配置 HUD {placement} が横に分かれる");
    AssertSame(world, placementHud, "配置モード");
    await p.ExitToGameScreen();

    // 削除モード: 上の帯が HUD より上
    // Delete mode: the top band is above the HUD
    await p.PressKey(Key.G);
    await p.WaitUiState(UIStateEnum.DeleteBar, 10f);
    await WaitShown("delete-mode-warning");
    var band = root.Q("delete-mode-warning").Q("delete-mode-warning-band").worldBound;
    var deleteHud = await HudRect("削除モード");
    p.Assert(band.yMax <= deleteHud.yMin + 0.5f, $"削除の上の帯 {band} が HUD {deleteHud} より上");
    AssertSame(world, deleteHud, "削除モード");
    await p.Screenshot("01-delete-band");
    await p.PressKey(Key.G);
    await p.WaitUiState(UIStateEnum.GameScreen, 10f);

    // 1024×576: 舞台の倍率に従い論理の位置は同じ、画面内でインベントリより上
    // 1024x576: follows the stage scale with the same logical rect, inside the screen and above the inventory
    await PlaytestGameViewOps.SetResolution(p.CancellationToken, 1024, 576);
    await UniTask.DelayFrame(3);
    var scaledWorld = await HudRect("1024×576 のゲーム画面");
    AssertSame(world, scaledWorld, "1024×576 のゲーム画面");
    await p.PressKey(Key.Tab);
    await p.WaitUiState(UIStateEnum.PlayerInventory, 10f);
    await WaitShown("main-grid");
    var small = await HudRect("1024×576 のインベントリ");
    AssertSame(scaledWorld, small, "1024×576 のインベントリ");
    var screen = root.worldBound;
    p.Assert(screen.Contains(small.min) && screen.Contains(small.max), $"HUD {small} が画面 {screen} の中");
    p.Assert(small.yMax <= root.Q("main-grid").worldBound.yMin + 0.5f, "1024×576 でも HUD がインベントリより上");
    await p.Screenshot("02-1024x576-inventory");
    await PlaytestGameViewOps.SetResolution(p.CancellationToken, 1280, 720);
    await UniTask.DelayFrame(3);
    await p.PressKey(Key.Tab);
    await p.WaitUiState(UIStateEnum.GameScreen, 10f);

    #region Internal

    // HUD が出るまで待って矩形を読む
    // Wait for the HUD and read its rect
    async UniTask<Rect> HudRect(string where)
    {
        // HUD は Pick を拾わない（UiPickingIgnore）ので UntilUiElement（Clickable 待ち）は使わず、存在と幅だけを待つ
        // The HUD ignores picking, so skip UntilUiElement (waits for Clickable) and wait only for existence and width
        await UniTask.WaitUntil(() => root.Q("challenge-hud") is { } shown && shown.worldBound.width > 0f).TimeoutWithoutException(System.TimeSpan.FromSeconds(10));
        var hud = root.Q("challenge-hud");
        p.Assert(hud.resolvedStyle.display == DisplayStyle.Flex && hud.worldBound.width > 0f, $"{where} で HUD が出ている");
        p.Note($"{where} の HUD {hud.worldBound}");
        return hud.worldBound;
    }

    // 中身は存在と幅だけを待つ（格子の中心はスロットの隙間、配置・削除の HUD は Pick を拾わず、Clickable 待ちが通らない）
    // Wait only for the content's existence and width (the grid center is a slot gap and the placement/delete HUDs ignore picking, so a Clickable wait never passes)
    async UniTask WaitShown(string id)
    {
        await UniTask.WaitUntil(() => IsVisible(root.Q(id))).TimeoutWithoutException(System.TimeSpan.FromSeconds(10));
        p.Assert(IsVisible(root.Q(id)), $"{id} が出ている（幅あり・visible・不透明度 > 0）");
    }

    // Web の toBeVisible に寄せ、幅に加えて visibility と不透明度も見る
    // Close to Web toBeVisible: check visibility and opacity in addition to width
    bool IsVisible(VisualElement element)
    {
        return element != null && element.worldBound.width > 0f && element.resolvedStyle.visibility == Visibility.Visible && element.resolvedStyle.opacity > 0f;
    }

    void AssertSame(Rect expected, Rect actual, string where)
    {
        // 許容は物理 1px の論理換算（1280 幅で 1、1024 幅で 1.25）。子要素ごとの物理ピクセル丸めが積み重なり、高さ等が 1 物理 px までずれる
        // Tolerance is one physical pixel in logical units (1 at 1280 wide, 1.25 at 1024); per-child physical pixel rounding accumulates up to one physical pixel
        var tolerance = Mathf.Max(1f, root.worldBound.width / Screen.width) + 0.01f;
        var same = Mathf.Abs(expected.x - actual.x) <= tolerance && Mathf.Abs(expected.y - actual.y) <= tolerance && Mathf.Abs(expected.width - actual.width) <= tolerance && Mathf.Abs(expected.height - actual.height) <= tolerance;
        p.Assert(same, $"{where} の HUD がゲーム画面と同じ位置・寸法（物理 1px 以内={tolerance:0.00}。{actual} / {expected}）");
    }

    // メニューを開き、HUD が同じ位置で（安全帯があれば）中身より上にあることを見て、ゲーム画面へ戻る
    // Open a menu, check the HUD keeps its rect (and sits above the content when there is a safe area), then return
    async UniTask CheckMenu(Key key, UIStateEnum state, string contentId, bool hasUpperSafeArea)
    {
        await p.PressKey(key);
        await p.WaitUiState(state, 10f);
        await WaitShown(contentId);
        var hud = await HudRect(state.ToString());
        AssertSame(world, hud, state.ToString());
        if (hasUpperSafeArea) p.Assert(hud.yMax <= root.Q(contentId).worldBound.yMin + 0.5f, $"{state} で HUD が {contentId} より上");
        // ExitToGameScreen は B を押すだけで各メニューからは戻らないので、開いたキーで閉じる
        // ExitToGameScreen only taps B and does not leave these menus, so close with the key that opened it
        await p.PressKey(key);
        await p.WaitUiState(UIStateEnum.GameScreen, 10f);
    }

    #endregion
});
