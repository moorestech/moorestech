// 字の周りの欠片と秒数の切れの撮影（I-fix5 Task 4）: GameUi でビルドメニューの題・キー操作ヒント、レシピ画面の一覧の題・先頭セルの個数のバッジ・レシピ行の秒数を、1280×720 と 2560×1080 の実画面で撮る
// 各状態を USS のまま（-uss）・題とキー操作ヒントの影を消した（-noshadow）・その両方に題の旧値 0.3px 0.3px 0 rgb(0 0 0 / 35%) を付けた（-shadow03）の 3 枚で撮り、同じ atlas の状態で影の有無を比べる
// 秒数は参照 4-recipe-selected-machine と同じ文（1 件目 10秒・2 件目 20秒）に差し替える。段組・色・寸法は本番のまま。要素の矩形（物理 px）を Note に残す
// 実行前に PlayMode を止め、execute-dynamic-code で UiSystemEditorSwitch.Set(UiSystem.GameUi) を立ててから run-scenario.sh を呼ぶ
// Glyph-fragment and duration-clip shots (I-fix5 Task 4): on GameUi shoot the build menu title and key hints, plus the recipe screen's list title, first cell count badge and recipe-row durations, on the real screen at 1280x720 and 2560x1080
// Each state is shot as the USS stands (-uss), with the title and key-hint shadows removed (-noshadow), and with both given the old title value 0.3px 0.3px 0 rgb(0 0 0 / 35%) (-shadow03), comparing shadows under one atlas state
// Durations are swapped to the reference 4-recipe-selected-machine text (row 1 10秒, row 2 20秒); layout, colors and sizes stay production; element rects (device px) go to Notes
// Stop PlayMode and call UiSystemEditorSwitch.Set(UiSystem.GameUi) via execute-dynamic-code before run-scenario.sh
using System.Linq;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.UIState;
using Client.Input;
using Client.Localization;
using Client.Playtest;
using Client.Playtest.Input;
using Client.Playtest.Operations.Ui;
using Client.Playtest.Overlay;
using Client.Ui.Foundation.Root;
using Client.Ui.Foundation.Text;
using Client.Ui.State.Recipe.Catalog;
using Client.Ui.State.Recipe.Screen;
using Client.Ui.State.Text;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.Context;
using Game.UnlockState;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using VContainer;

var options = new PlaytestRunOptions { Record = false, ScenarioTimeoutSeconds = 600f };
return PlaytestRunner.Run("uitk-glyph-fragments-compare", options, async p =>
{
    // 1. 系の主張と入力の準備（前例 uitk-stage3-compare）
    // 1. Assert the UI system and prepare input (precedent uitk-stage3-compare)
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
    var resolver = ClientDIContext.DIContainer.DIContainerResolver;
    var badgeCellName = "";

    // 2. ビルドメニュー（題・キー操作ヒント）
    // 2. Build menu (title, key hints)
    await p.PressKey(Key.B);
    await p.WaitUiState(UIStateEnum.BuildMenu, 10f);
    await Shoot("build-menu", "build-menu-frame");
    await p.PressKey(Key.B);
    await p.WaitUiState(UIStateEnum.GameScreen, 10f);

    // 3. レシピ画面: 行の多いアイテムを選び、秒数を参照の文にする
    // 3. Recipe screen: pick the item with the most rows and set the durations to the reference text
    // 行と個数のバッジが出るよう、全アイテム・全機械レシピを解放する
    // Unlock every item and machine recipe so rows and count badges appear
    var unlockState = resolver.Resolve<IGameUnlockStateData>();
    var itemIds = unlockState.ItemUnlockStateInfos.Keys.ToList();
    var machineRecipeGuids = unlockState.MachineRecipeUnlockStateInfos.Keys.ToList();
    await p.RunOnServer("全アイテム・全機械レシピの解放", () =>
    {
        var controller = ServerContext.GetService<IGameUnlockStateDataController>();
        foreach (var id in itemIds) controller.UnlockItem(id);
        foreach (var guid in machineRecipeGuids) controller.UnlockMachineRecipe(guid);
    });
    await p.Until(() => unlockState.ItemUnlockStateInfos.Values.All(i => i.IsUnlocked), 10f, "クライアントへ解放が届く");
    await p.WaitSeconds(1f);
    await p.PressKey(Key.Tab);
    await p.WaitUiState(UIStateEnum.PlayerInventory, 15f);
    await p.UntilUiElement("item-list-panel", 5f);
    var model = resolver.Resolve<RecipeScreenModel>();

    // クラフトのある最初のセルへ素材を 7 回分持たせ、個数のバッジを出す
    // Hand over seven crafts' worth of materials to the first cell that has a craft, showing its count badge
    var crafts = CraftRecipeCatalog.BuildUnlocked(unlockState);
    var badgeCell = model.ItemList.Value.Cells.First(c => crafts.Any(r => r.ResultItemId == c.ItemId));
    var badgeCraft = crafts.First(r => r.ResultItemId == badgeCell.ItemId);
    badgeCellName = $"item-list-cell-{badgeCell.ItemId.AsPrimitive()}";
    foreach (var m in badgeCraft.RequiredItems) await p.GiveItem(MasterHolder.ItemMaster.GetItemMaster(m.ItemId).Name, m.Count * 7);
    await p.Until(() => model.ItemList.Value.Cells.First(c => c.ItemId == badgeCell.ItemId).CraftableCount > 0, 5f, "バッジのセルのクラフト可能数");
    var selection = resolver.Resolve<RecipeItemSelection>();
    var bestItem = model.ItemList.Value.Cells[0].ItemId;
    var bestRows = 0;
    foreach (var cell in model.ItemList.Value.Cells)
    {
        selection.Select(cell.ItemId);
        await UniTask.DelayFrame(2);
        var rows = Root().Query(className: "recipe-row__duration").ToList().Count;
        if (rows > bestRows) (bestItem, bestRows) = (cell.ItemId, rows);
    }
    selection.Select(bestItem);
    await UniTask.DelayFrame(4);
    p.Note($"選んだアイテム {bestItem.AsPrimitive()} 行 {bestRows}");
    var durations = Root().Query<LocalizedLabel>(className: "recipe-row__duration").ToList();
    for (var i = 0; i < durations.Count; i++) durations[i].SetText(UiText.FromVerbatim(i == 1 ? "20秒" : "10秒"));
    await Shoot("recipe", "item-list-panel");

    #region Internal

    VisualElement Root() => GameUiRoot.Instance.GetRootElement();

    // 解像度ごとに USS のまま・影なし・0.3px の影の 3 枚を撮り、矩形を記録する
    // Per resolution shoot the USS state, no shadow and the 0.3px shadow, recording rects
    async UniTask Shoot(string name, string panelName)
    {
        Label title() => Root().Q(panelName).Q<Label>(className: "game-panel__title");
        foreach (var (w, h) in new[] { (1280, 720), (2560, 1080) })
        {
            await PlaytestGameViewOps.SetResolution(p.CancellationToken, w, h);
            SemanticInput.MouseMoveTo(new Vector2(Screen.width * 0.03f, Screen.height * 0.5f));
            foreach (var overlay in Object.FindObjectsByType<PlaytestOverlayView>(FindObjectsSortMode.None)) overlay.gameObject.SetActive(false);
            await p.WaitSeconds(1.5f);
            var shadowed = new VisualElement[] { title() }.Concat(Root().Query(className: "key-hint-text").ToList().SelectMany(e => e.Query<Label>().ToList())).ToList();
            foreach (var e in shadowed) e.style.textShadow = StyleKeyword.Null;
            await UniTask.DelayFrame(4);
            p.Note($"{name} {w}x{h} uss: {Rects(title())}");
            await p.Screenshot($"{name}-{w}x{h}-uss");
            foreach (var e in shadowed) e.style.textShadow = new StyleTextShadow(new TextShadow { offset = Vector2.zero, blurRadius = 0f, color = Color.clear });
            await UniTask.DelayFrame(4);
            await p.Screenshot($"{name}-{w}x{h}-noshadow");
            foreach (var e in shadowed) e.style.textShadow = new StyleTextShadow(new TextShadow { offset = new Vector2(0.3f, 0.3f), blurRadius = 0f, color = new Color(0f, 0f, 0f, 0.35f) });
            await UniTask.DelayFrame(4);
            await p.Screenshot($"{name}-{w}x{h}-shadow03");
            foreach (var e in shadowed) e.style.textShadow = StyleKeyword.Null;
        }
    }

    // 物理 px の矩形（題・キー操作ヒントの文・先頭セルと個数・秒数）
    // Device-px rects (title, key-hint texts, first cell and count, durations)
    string Rects(Label title)
    {
        var scale = Screen.width / Root().layout.width;
        string R(VisualElement e) => e == null ? "-" : $"({e.worldBound.xMin * scale:F2},{e.worldBound.yMin * scale:F2})-({e.worldBound.xMax * scale:F2},{e.worldBound.yMax * scale:F2})";
        var parts = new System.Collections.Generic.List<string> { $"scale {scale:F3}", $"title '{title.text}' {R(title)}" };
        foreach (var t in Root().Query<Label>(className: "key-hint-row__text").ToList()) parts.Add($"hint '{t.text}' {R(t)}");
        var cell = badgeCellName == "" ? null : Root().Q(badgeCellName);
        if (cell != null) parts.Add($"cell {R(cell)} count '{cell.Q<Label>(null, "slot-content__count", "outlined-count-label--glyph")?.text}' {R(cell.Q<Label>(null, "slot-content__count", "outlined-count-label--glyph"))} pad {cell.Q<Label>(null, "slot-content__count", "outlined-count-label--glyph")?.resolvedStyle.paddingRight:F2}");
        foreach (var d in Root().Query<Label>(className: "recipe-row__duration").ToList()) parts.Add($"duration '{d.text}' {R(d)}");
        return string.Join(" | ", parts);
    }

    #endregion
});
