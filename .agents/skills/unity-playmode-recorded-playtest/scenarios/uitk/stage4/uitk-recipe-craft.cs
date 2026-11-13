// レシピ画面を UITK（GameUi）で通す: 開く・セルで選ぶ・掴みで選択が変わらない・マウス長押しクラフトと外れで止まる・Esc の選択解除・Tab の選択保持・Enter 長押し・解放と持ち物での組み直し
// Runs the recipe screen on UITK (GameUi): open, select by cell, grab keeps the selection, mouse hold craft and stop on leave, Esc clears, Tab keeps, Enter hold, rebuild on unlock and inventory
// 実行前に PlayMode を止め、execute-dynamic-code で UiSystemEditorSwitch.Set(UiSystem.GameUi) を立ててから run-scenario.sh を呼ぶ
// Stop PlayMode and call UiSystemEditorSwitch.Set(UiSystem.GameUi) via execute-dynamic-code before run-scenario.sh
using System.Linq;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.UIState;
using Client.Input;
using Client.Playtest;
using Client.Playtest.Input;
using Client.Playtest.Operations;
using Client.Ui.Foundation.Root;
using Client.Ui.State.Recipe.Catalog;
using Client.Ui.State.Recipe.Data;
using Client.Ui.State.Recipe.Screen;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.Context;
using Game.UnlockState;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using VContainer;

return PlaytestRunner.Run("uitk-recipe-craft", new PlaytestRunOptions { Record = true }, async p =>
{
    p.Note($"全体スイッチ 系={UiInputExclusivity.ActiveUiSystem}");
    p.Assert(UiInputExclusivity.ActiveUiSystem == UiSystem.GameUi, "全体スイッチが UITK（GameUi）");
    InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
    InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
    SemanticInput.EnsureDevices();
    InputSystem.EnableDevice(Keyboard.current);
    InputSystem.EnableDevice(Mouse.current);
    await p.SetupDebugEnvironment(new PlaytestEnvironmentConfig());
    await p.SkipOpeningSkitIfPlaying();
    await p.ExitToGameScreen();

    // 解放済みで素材を 1 つも持っていないクラフトを選び、ちょうど 2 回分の素材を持たせる
    // Pick an unlocked craft whose materials we own none of, and hand over exactly two crafts' worth
    var resolver = ClientDIContext.DIContainer.DIContainerResolver;
    var unlockState = resolver.Resolve<IGameUnlockStateData>();
    var model = resolver.Resolve<RecipeScreenModel>();
    var selection = resolver.Resolve<RecipeItemSelection>();
    var root = GameUiRoot.Instance.GetRootElement();
    var recipe = CraftRecipeCatalog.BuildUnlocked(unlockState).Where(IsFresh).OrderBy(r => r.CraftTime < 1.0).First();
    var resultName = NameOf(recipe.ResultItemId);
    var craftTime = (float)recipe.CraftTime;
    p.Note($"クラフト {resultName} x{recipe.ResultCount} ← {string.Join("・", recipe.RequiredItems.Select(m => $"{NameOf(m.ItemId)} x{m.Count}"))} 秒数 {craftTime}（{recipe.RecipeGuid}）");
    await GiveCrafts(recipe, 2);

    // Tab で開き、ビューアと一覧と案内が見える
    // Open with Tab; the viewer, the list and the hint show
    await p.PressKey(Key.Tab);
    await p.WaitUiState(UIStateEnum.PlayerInventory, 15f);
    await p.UntilUiElement("item-list-panel", 5f);
    p.Assert((await PlaytestUiElementOps.FindUi("recipe-viewer")).IsFound && (await PlaytestUiElementOps.FindUi("item-list-panel")).IsFound && (await PlaytestUiElementOps.FindUi("recipe-viewer-hint")).Visible, "recipe-viewer と item-list-panel があり未選択の案内が見える");
    await p.Screenshot("01-open");

    // 結果のセルを押すとクラフトボタンが出る
    // Pressing the result cell shows the craft button
    var resultCell = $"item-list-cell-{recipe.ResultItemId.AsPrimitive()}";
    var craftButton = $"craft-button-{recipe.RecipeGuid}";
    await PlaytestUiElementOps.ScrollDownUntilUiElementIsClickable(resultCell, "item-list-scroll", 10f);
    await p.ClickUi(resultCell);
    await p.UntilUiElement(craftButton, 5f);
    await p.Screenshot("02-select");

    // 別のセルの中心から上へ 60px 掴んで動かしても選択は変わらない
    // Grabbing from another cell's center and moving 60px up keeps the selection
    var otherCell = root.Query(className: "item-list-cell").ToList().Select(c => c.name).First(n => n != resultCell);
    var otherRect = (await PlaytestUiElementOps.FindUi(otherCell)).ScreenRect;
    SemanticInput.MouseMoveTo(otherRect.center);
    await p.WaitSeconds(0.5f);
    SemanticInput.MouseButtonDown(0);
    await p.WaitSeconds(0.2f);
    await SemanticInput.MouseGlideTo(otherRect.center + new Vector2(0f, 60f), 0.4f);
    await p.WaitSeconds(0.2f);
    SemanticInput.MouseButtonUp(0);
    await p.WaitSeconds(0.5f);
    p.Assert(selection.Current.Value.SameAs(RecipeSelection.Of(recipe.ResultItemId)) && (await PlaytestUiElementOps.FindUi(craftButton)).Visible, $"掴みでは {otherCell} が選ばれず選択が残る");
    await p.Screenshot("03-drag-keeps");

    // ボタンを押し続けると 2 回作って素材切れで無効になる
    // Holding the button crafts twice, then it disables when materials run out
    var before = p.CountItem(resultName);
    var buttonRect = (await PlaytestUiElementOps.FindUi(craftButton)).ScreenRect;
    SemanticInput.MouseMoveTo(buttonRect.center);
    await p.WaitSeconds(0.5f);
    SemanticInput.MouseButtonDown(0);
    await p.Until(() => p.CountItem(resultName) >= before + 2 * recipe.ResultCount, 3f * craftTime + 2f, "押し続けて 2 回作る");
    await p.Until(() => !root.Q(craftButton).enabledSelf, 3f, "素材切れでボタンが無効");
    SemanticInput.MouseButtonUp(0);
    p.Assert(p.CountItem(resultName) == before + 2 * recipe.ResultCount, "ちょうど 2 回分増えた");
    await p.Screenshot("04-hold-craft");

    // 1 回分足し、押したまま半分でボタンの外へ出ると作らない
    // Add one craft's worth; leaving the button halfway through the hold crafts nothing
    await GiveCrafts(recipe, 1);
    await p.Until(() => root.Q(craftButton).enabledSelf, 5f, "素材を足すとボタンが有効");
    before = p.CountItem(resultName);
    SemanticInput.MouseButtonDown(0);
    await p.WaitSeconds(0.5f * craftTime);
    await SemanticInput.MouseGlideTo(new Vector2(buttonRect.xMin - 40f, buttonRect.center.y), 0.1f);
    await p.WaitSeconds(craftTime);
    SemanticInput.MouseButtonUp(0);
    p.Assert(p.CountItem(resultName) == before, "外れで止まり所持が増えない");
    await p.Screenshot("05-leave-stops");

    // Esc で閉じて開き直すと案内に戻る（選択が外れる）
    // Closing with Esc and reopening shows the hint (selection cleared)
    await p.PressKey(Key.Escape);
    await p.WaitUiState(UIStateEnum.GameScreen, 10f);
    await p.PressKey(Key.Tab);
    await p.WaitUiState(UIStateEnum.PlayerInventory, 10f);
    await p.Until(() => root.Q("recipe-viewer-hint").resolvedStyle.display == DisplayStyle.Flex, 5f, "案内が見える");
    p.Assert(selection.Current.Value is NoRecipeSelection, "Esc で選択が外れた");
    await p.Screenshot("06-esc-clears");

    // 選び直して Tab で閉じ、開いた 1 フレーム後にボタンが見える（選択が残る。D-33）
    // Reselect, close with Tab; one frame after reopening the button shows (selection kept; D-33)
    await p.ClickUi(resultCell);
    await p.UntilUiElement(craftButton, 5f);
    await p.PressKey(Key.Tab);
    await p.WaitUiState(UIStateEnum.GameScreen, 10f);
    await p.PressKey(Key.Tab);
    await p.WaitUiState(UIStateEnum.PlayerInventory, 10f);
    await UniTask.Yield();
    var reopened = await PlaytestUiElementOps.FindUi(craftButton);
    p.Assert(reopened.Visible, $"Tab で開き直すと 1 フレーム後にクラフトボタンが見える（{reopened.Status}）");
    await p.Screenshot("06b-tab-reopen");

    // ボタンをクリックしてフォーカスを置き、Enter を押し続けると 1 回作る（Enter は Game ビューへ IMGUI のキーで送る。D-45）
    // Click to focus the button and hold Enter to craft once (Enter goes to the Game view as an IMGUI key; D-45)
    await p.ClickUi(craftButton);
    p.Assert(root.focusController.focusedElement == root.Q(craftButton), "クリックでボタンにフォーカスが残る");
    before = p.CountItem(resultName);
    await HoldImguiKey("return", craftTime + 1f);
    p.Assert(p.CountItem(resultName) == before + recipe.ResultCount, "Enter の長押しで 1 回分増えた");

    // もう 1 回分足し、Space を押し続けても作らない。Tab はフォーカス移動に食われずインベントリを閉じる（D-31）
    // Add one more; holding Space crafts nothing. Tab is not eaten by focus navigation and closes the inventory (D-31)
    await GiveCrafts(recipe, 1);
    await p.Until(() => root.Q(craftButton).enabledSelf, 5f, "素材を足すとボタンが有効");
    await p.ClickUi(craftButton);
    before = p.CountItem(resultName);
    await HoldImguiKey("space", craftTime + 1f);
    p.Assert(p.CountItem(resultName) == before, "Space の長押しでは作らない");
    await p.Screenshot("07-enter-hold");
    SendImguiKey("tab", EventType.KeyDown);
    await p.PressKey(Key.Tab);
    await p.WaitUiState(UIStateEnum.GameScreen, 10f);
    SendImguiKey("tab", EventType.KeyUp);

    // 閉じたまま一覧に無い結果アイテムを解放しても組まず、開くと入り、素材を持つと数が増える（クラフトは全解放済みなのでアイテムの解放で見る。D-37・D-38）
    // Unlocking a hidden result item while closed builds nothing; opening adds it and owning materials raises its count (all crafts start unlocked, so use an item unlock; D-37, D-38)
    var hidden = CraftRecipeCatalog.BuildUnlocked(unlockState).First(r => CraftableOf(r.ResultItemId) < 0 && !ItemUnlocked(r.ResultItemId));
    var listBefore = model.ItemList.Value;
    await p.RunOnServer($"解放 {NameOf(hidden.ResultItemId)}", () => ServerContext.GetService<IGameUnlockStateDataController>().UnlockItem(hidden.ResultItemId));
    await p.Until(() => ItemUnlocked(hidden.ResultItemId), 10f, "クライアントへ解放が届く");
    await p.WaitSeconds(1f);
    p.Assert(ReferenceEquals(model.ItemList.Value, listBefore), "閉画面では解放で組み直さない（同じ参照）");
    await p.PressKey(Key.Tab);
    await p.WaitUiState(UIStateEnum.PlayerInventory, 10f);
    await UniTask.Yield();
    var countBefore = CraftableOf(hidden.ResultItemId);
    p.Assert(countBefore >= 0, $"開くと解放した {NameOf(hidden.ResultItemId)} のセルが一覧に入る（一覧 {listBefore.Cells.Count}→{model.ItemList.Value.Cells.Count} 件）");
    await GiveCrafts(hidden, 1);
    await p.Until(() => CraftableOf(hidden.ResultItemId) > countBefore, 5f, "素材を持つとクラフト可能数が増える");
    p.Note($"{NameOf(hidden.ResultItemId)} のクラフト可能数 {countBefore}→{CraftableOf(hidden.ResultItemId)}（開いた直後→素材を持った後）");
    await p.Screenshot("08-unlock-rebuild");

    #region Internal

    string NameOf(ItemId itemId) => MasterHolder.ItemMaster.GetItemMaster(itemId).Name;
    int CraftableOf(ItemId itemId) => model.ItemList.Value.Cells.FirstOrDefault(c => c.ItemId == itemId)?.CraftableCount ?? -1;
    bool ItemUnlocked(ItemId itemId) => unlockState.ItemUnlockStateInfos.TryGetValue(itemId, out var info) && info.IsUnlocked;
    // 一覧に結果のセルがあり、素材が結果と別で 1 つも持っていない
    // The result has a list cell and every material differs from the result and is not owned
    bool IsFresh(CraftRecipeData r) => CraftableOf(r.ResultItemId) >= 0 && r.RequiredItems.All(m => m.ItemId != r.ResultItemId && p.CountItem(NameOf(m.ItemId)) == 0);
    async UniTask GiveCrafts(CraftRecipeData r, int times)
    {
        foreach (var m in r.RequiredItems) await p.GiveItem(NameOf(m.ItemId), m.Count * times);
    }

    // Game ビューへ IMGUI のキーを送る（実キーと同じく PanelEventHandler の Event.PopEvent から UITK のキーイベントになる）
    // Send an IMGUI key to the Game view (like a real key it becomes a UITK key event via PanelEventHandler's Event.PopEvent)
    void SendImguiKey(string key, EventType type) => Resources.FindObjectsOfTypeAll<UnityEditor.EditorWindow>().First(w => w.GetType().Name == "GameView").SendEvent(new Event(Event.KeyboardEvent(key)) { type = type });

    async UniTask HoldImguiKey(string key, float seconds)
    {
        p.Note($"IMGUI キー {key} を {seconds:F1} 秒押し続ける");
        SendImguiKey(key, EventType.KeyDown);
        await p.WaitSeconds(seconds);
        SendImguiKey(key, EventType.KeyUp);
        await p.WaitSeconds(0.5f);
    }

    #endregion
});
