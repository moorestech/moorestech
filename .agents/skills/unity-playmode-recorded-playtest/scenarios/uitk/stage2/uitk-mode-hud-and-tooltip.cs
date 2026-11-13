// 操作モード HUD（配置 HUD・高さ・削除の帯・削除箱の高さ）と CursorTooltip のポインタ追従を UITK（GameUi）で通す
// Runs the operation mode HUD (placement HUD, height, delete bands, delete box height) and CursorTooltip pointer following on UITK (GameUi)
// 実行前に PlayMode を止め、execute-dynamic-code で UiSystemEditorSwitch.Set(UiSystem.GameUi) を立ててから run-scenario.sh を呼ぶ
// Stop PlayMode and call UiSystemEditorSwitch.Set(UiSystem.GameUi) via execute-dynamic-code before run-scenario.sh
using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.Tooltip;
using Client.Game.InGame.UI.UIState;
using Client.Input;
using Client.Localization;
using Client.Playtest;
using Client.Playtest.Input;
using Client.Playtest.Operations;
using Client.Playtest.UiElements;
using Client.Ui.Foundation.Root;
using Cysharp.Threading.Tasks;
using Mooresmaster.Localization.Generated;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using VContainer;

var options = new PlaytestRunOptions { Record = true };
return PlaytestRunner.Run("uitk-mode-hud-and-tooltip", options, async p =>
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

    // 木のチェストで配置モードへ入り、右上の配置 HUD に名前が出る
    // Enter placement mode with a wooden chest; the top-right placement HUD shows its name
    // 木のチェストは研究で解放されるので、無料設置（全解放）の足場で始める（前例 right-short-press-cancel）
    // The wooden chest is research-locked, so start on the free-placement (all unlocked) scaffold (precedent right-short-press-cancel)
    await p.SetupDebugEnvironment(new PlaytestEnvironmentConfig());
    await p.SkipOpeningSkitIfPlaying();
    await p.ExitToGameScreen();
    await p.Hotbar.AssignHotbar(0, "木のチェスト");
    await p.Hotbar.EnterBuildMode(0);
    await p.WaitUiState(UIStateEnum.PlaceBlock, 15f);
    var placementHud = await PollUntilFoundAsync("placement-mode-hud", 10);
    p.Assert(placementHud.IsFound, "配置 HUD（placement-mode-hud）が出る");
    var details = PlacementDetails();
    p.Note($"配置 HUD の詳細: {string.Join(" / ", details)}");
    p.Assert(details.Count == 2 && details[0].Contains("木のチェスト"), "配置 HUD の 1 行目に木のチェスト");
    await p.Screenshot("01-placement");

    // E で高さ +1。LocalizedLabel が頭に line-height のつまみを付けるので末尾一致で見る
    // E raises the height by one; LocalizedLabel prefixes a line-height tag, so compare by suffix
    var expectedHeight = Localize.GetFormatted(LocalizationKeys.Ui.ModeHud.PlacementHeight, new[] { new LocalizationTextParam("height", "1") });
    await p.PressKey(Key.E);
    await p.Until(() => PlacementDetails().Count == 2 && PlacementDetails()[1].EndsWith(expectedHeight), 5f, $"配置 HUD の 2 行目が「{expectedHeight}」");
    await p.Screenshot("02-height");

    // G で削除モード。上下の帯 2 本が出て、配置 HUD は消え、削除箱の高さが出る
    // G enters delete mode; two bands appear, the placement HUD goes away, and the delete box height shows
    await p.PressKey(Key.G);
    await p.WaitUiState(UIStateEnum.DeleteBar, 10f);
    var warning = await PollUntilFoundAsync("delete-mode-warning", 10);
    p.Assert(warning.IsFound, "削除モードの帯の入れ物（delete-mode-warning）が出る");
    p.Assert(GameUiTree().Q<VisualElement>("delete-mode-warning").Query<VisualElement>("delete-mode-warning-band").ToList().Count == 2, "帯が上下 2 本");
    p.Assert(!(await PlaytestUiElementOps.FindUi("placement-mode-hud")).IsFound, "削除モードでは配置 HUD が消える");
    var deleteHeight = await PollUntilFoundAsync("delete-box-height-hud", 10);
    var heightPrefix = Localize.Get(LocalizationKeys.Ui.ModeHud.PlacementHeight).Split('{')[0];
    p.Note($"削除箱の高さ: '{deleteHeight.Text}'（期待の頭 '{heightPrefix}'）");
    p.Assert(deleteHeight.IsFound && deleteHeight.Text.Contains(heightPrefix), "削除箱の高さの文が出る");
    await p.Screenshot("03-delete-bands");

    // もう一度 G で抜けると帯が消える
    // Pressing G again leaves delete mode and the bands disappear
    await p.PressKey(Key.G);
    await p.WaitUiState(UIStateEnum.GameScreen, 10f);
    p.Assert(!(await PlaytestUiElementOps.FindUi("delete-mode-warning")).IsFound, "削除モードを抜けると帯が消える");

    // インベントリでロックを外し、何も拾わない所でもツールチップがポインタの右下 12px に付いて来る
    // Unlock via the inventory; the tooltip follows 12px below-right of the pointer even where nothing is pickable
    await p.PressKey(Key.Tab);
    await p.WaitUiState(UIStateEnum.PlayerInventory, 15f);
    var tooltip = ClientDIContext.DIContainer.DIContainerResolver.Resolve<IMouseCursorTooltip>();
    var owner = new TooltipOwner();
    tooltip.Show(owner, LocalizationKeys.Ui.Tooltip.WorldTarget);
    var pointA = new Vector2(Screen.width / 4f, Screen.height / 4f);
    var pointB = new Vector2(Screen.width * 3f / 4f, Screen.height / 4f);
    p.Assert(await IsTooltipAttachedAsync(pointA, "A"), "ツールチップがポインタ A の右下に付く");
    p.Assert(await IsTooltipAttachedAsync(pointB, "B"), "ツールチップがポインタ B へ付いて来る");
    await p.Screenshot("04-tooltip-follows");
    tooltip.Hide(owner);
    await p.PressKey(Key.Tab);
    await p.WaitUiState(UIStateEnum.GameScreen, 15f);

    #region Internal

    VisualElement GameUiTree()
    {
        return GameUiRoot.Instance.GetRootElement();
    }

    // 同名 Label が 2 つあり FindUi は Ambiguous になるので、配置 HUD の配下を名前で引く
    // Two Labels share the name and FindUi turns Ambiguous, so query under the placement HUD by name
    List<string> PlacementDetails()
    {
        var hud = GameUiTree().Q<VisualElement>("placement-mode-hud");
        if (hud == null) return new List<string>();
        return hud.Query<TextElement>("operation-mode-detail").ToList().Select(label => label.text).ToList();
    }

    // 面の左上（左下原点の画面 px では xMin, yMax）がポインタ +12/-12 の 20px 以内
    // The face's top-left (xMin, yMax in bottom-left-origin screen px) lies within 20px of pointer +12/-12
    async UniTask<bool> IsTooltipAttachedAsync(Vector2 pointer, string label)
    {
        p.Note($"注入カーソルをポインタ {label}{pointer} へ動かす");
        SemanticInput.MouseMoveTo(pointer);
        await p.WaitSeconds(1f);
        var face = await PlaytestUiElementOps.FindUi("cursor-tooltip");
        if (!face.IsFound)
        {
            p.Note($"ツールチップが見つからない（{face.Status}）");
            return false;
        }

        var topLeft = new Vector2(face.ScreenRect.xMin, face.ScreenRect.yMax);
        var expected = new Vector2(pointer.x + 12f, pointer.y - 12f);
        var distance = Vector2.Distance(topLeft, expected);
        p.Note($"面の左上={topLeft} 期待={expected} 距離={distance:F1}px 文字='{face.Text}'");
        return distance <= 20f;
    }

    async UniTask<UiElementSnapshot> PollUntilFoundAsync(string testid, int seconds)
    {
        var result = await PlaytestUiElementOps.FindUi(testid);
        for (var i = 0; i < seconds && !result.IsFound; i++)
        {
            await p.WaitSeconds(1f);
            result = await PlaytestUiElementOps.FindUi(testid);
        }

        return result;
    }

    #endregion
});
