// 群の淡さ（研究の素材不足の枠・研究ボタンの無効）を Game View の実画面で 1280×720・2560×1080 に撮る。群の filter と旧来の要素の opacity を撮り分ける
// Shoots group fading (research cost slot shortage, disabled research button) on the real Game View at 1280x720 and 2560x1080, with the group filter and the old element opacity
// 実行前に PlayMode を止め、execute-dynamic-code で UiSystemEditorSwitch.Set(UiSystem.GameUi) を立ててから run-scenario.sh を呼ぶ
// Stop PlayMode and call UiSystemEditorSwitch.Set(UiSystem.GameUi) via execute-dynamic-code before run-scenario.sh
using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.UI.UIState;
using Client.Input;
using Client.Localization;
using Client.Playtest;
using Client.Playtest.Input;
using Client.Playtest.Operations.Ui;
using Client.Playtest.Overlay;
using Client.Ui.Foundation.Root;
using Core.Master;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

var options = new PlaytestRunOptions { Record = false, ScenarioTimeoutSeconds = 600f };
return PlaytestRunner.Run("uitk-group-opacity-compare", options, async p =>
{
    p.Assert(UiInputExclusivity.ActiveUiSystem == UiSystem.GameUi, "全体スイッチが UITK（GameUi）");
    InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
    InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
    SemanticInput.EnsureDevices();
    InputSystem.EnableDevice(Keyboard.current);
    InputSystem.EnableDevice(Mouse.current);
    await p.SetupDebugEnvironment(new PlaytestEnvironmentConfig());
    await p.SkipOpeningSkitIfPlaying();
    await p.ExitToGameScreen();
    Localize.TrySetChosenLanguage("japanese");
    await p.WaitSeconds(6f);

    // 前提なしで消費アイテムのある根ノードを、素材を 1 つ足りなくして選ぶ（不足の枠と研究ボタンの無効が出る）
    // Select a root node with a cost while one item short (shows the shortage slot and the disabled research button)
    var node = MasterHolder.ResearchMaster.GetAllResearches().First(r => r.PrevResearchNodeGuids.Length == 0 && r.ConsumeItems.Length > 0);
    var guid = node.ResearchNodeGuid;
    var consume = node.ConsumeItems[0];
    var consumeName = MasterHolder.ItemMaster.GetItemMaster(consume.ItemGuid).Name;
    if (consume.ItemCount > 1) await p.GiveItem(consumeName, consume.ItemCount - 1);
    p.Note($"ノード {node.ResearchNodeName}（{guid}）素材 {consumeName} 必要 {consume.ItemCount} 所持 {consume.ItemCount - 1}");

    await p.PressKey(Key.R);
    await p.WaitUiState(UIStateEnum.ResearchTree, 10f);
    await p.UntilUiElement("research-tree", 5f);
    await p.ClickUi($"research-node-{guid}");
    await p.UntilUiElement("research-detail-pane", 5f);
    var root = GameUiRoot.Instance.GetRootElement();
    var button = root.Q($"research-button-{guid}");
    var slotFrame = root.Q("research-consume-slot-0");
    p.Assert(!button.enabledSelf, "研究ボタンが無効");
    p.Assert(slotFrame.ClassListContains("slot-frame--insufficient"), "消費アイテムの枠が不足");

    foreach (var (w, h) in new[] { (1280, 720), (2560, 1080) })
    {
        await PlaytestGameViewOps.SetResolution(p.CancellationToken, w, h);
        SemanticInput.MouseMoveTo(new Vector2(Screen.width * 0.03f, Screen.height * 0.5f));
        foreach (var overlay in Object.FindObjectsByType<PlaytestOverlayView>(FindObjectsSortMode.None)) overlay.gameObject.SetActive(false);
        await p.WaitSeconds(1.5f);
        p.Note($"{w}x{h} {Rects()}");
        await p.Screenshot($"group-{w}x{h}");

        // 旧来の要素の opacity（子ごとに掛かる）に一時で戻して撮る
        // Temporarily revert to the old element opacity (applied per child) and shoot
        SetElementOpacity(button, 0.6f);
        SetElementOpacity(slotFrame, 0.4f);
        await UniTask.DelayFrame(4);
        await p.WaitSeconds(0.5f);
        await p.Screenshot($"element-{w}x{h}");

        // Web の式（a×不透明の絵＋(1−a)×地）を画素ごとに作るため、不透明の絵と群を隠した地を撮る
        // Shoot the opaque drawing and the ground without the groups to build the Web formula (a*opaque + (1-a)*ground) per pixel
        SetElementOpacity(button, 1f);
        SetElementOpacity(slotFrame, 1f);
        await UniTask.DelayFrame(4);
        await p.WaitSeconds(0.5f);
        await p.Screenshot($"opaque-{w}x{h}");
        button.style.visibility = Visibility.Hidden;
        slotFrame.style.visibility = Visibility.Hidden;
        await UniTask.DelayFrame(4);
        await p.WaitSeconds(0.5f);
        await p.Screenshot($"ground-{w}x{h}");
        foreach (var e in new[] { button, slotFrame })
        {
            e.style.visibility = StyleKeyword.Null;
            e.style.filter = StyleKeyword.Null;
            e.style.opacity = StyleKeyword.Null;
        }
        await UniTask.DelayFrame(4);
    }

    #region Internal

    void SetElementOpacity(VisualElement element, float opacity)
    {
        element.style.filter = new StyleList<FilterFunction>(new List<FilterFunction>());
        element.style.opacity = opacity;
    }

    // 物理 px の矩形（研究ボタン・文言・消費アイテムの枠）
    // Device-px rects (research button, its label, cost slot)
    string Rects()
    {
        var scale = Screen.width / root.layout.width;
        string R(VisualElement e) => e == null ? "-" : $"({e.worldBound.xMin * scale:F2},{e.worldBound.yMin * scale:F2})-({e.worldBound.xMax * scale:F2},{e.worldBound.yMax * scale:F2})";
        return $"scale {scale:F3} button {R(button)} label {R(button.Q<Label>(className: "recipe-action-button__label"))} slot {R(slotFrame)}";
    }

    #endregion
});
