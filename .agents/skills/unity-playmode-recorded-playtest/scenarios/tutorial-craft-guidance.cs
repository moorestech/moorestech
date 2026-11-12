// シナリオ: 2 番目のチャレンジ（木の板）のクラフト誘導を通す。Tab のキーヒント → 木の板の枠 → 選ぶとクラフトボタンの枠 → 長押しで作って完了 → 3 番目のヒント
// Scenario: run the second challenge's (planks) craft guidance: Tab key hint -> plank outline -> craft button outline after selecting -> hold to craft and complete -> third hint
// クラフトボタンのアンカーは木の板を選んだ後にしか付かない（RecipeViewModelFactory・CraftRecipeEntryView。マスタの誘導も「①クリック → ②長押し」）
// The craft button anchor exists only after selecting the plank (RecipeViewModelFactory, CraftRecipeEntryView; the master guidance is also "1 click -> 2 hold")
// 研究の説明文の確認は外した。詳細欄はノードを選ばないと出ず、uitk-research-tree が両系で見る（D-9）
// The research description check moved out: the detail pane needs a selected node and uitk-research-tree covers it on both systems (D-9)
using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.Context;
using Client.Game.InGame.Tutorial;
using Client.Game.InGame.Tutorial.UIHighlight;
using Client.Game.InGame.UI.UIState;
using Client.Input;
using Client.Playtest;
using Client.Playtest.Input;
using Client.Playtest.Operations;
using Client.Ui.Foundation.Root;
using Client.Ui.Foundation.Anchor;
using Core.Master;
using Cysharp.Threading.Tasks;
using Mooresmaster.Model.ChallengesModule;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

var options = new PlaytestRunOptions { Record = true, ScenarioTimeoutSeconds = 300f };
return PlaytestRunner.Run("tutorial-craft-guidance", options, async p =>
{
    // 系の記録と入力の準備（前例 uitk-skit）
    // Record the UI system and prepare input (precedent uitk-skit)
    p.Note($"系={UiInputExclusivity.ActiveUiSystem}");
    var isGameUi = UiInputExclusivity.ActiveUiSystem == UiSystem.GameUi;
    InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
    InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
    SemanticInput.EnsureDevices();
    InputSystem.EnableDevice(Keyboard.current);
    InputSystem.EnableDevice(Mouse.current);

    // マスタから先頭・2 番目・3 番目のチャレンジと木の板のレシピを引く
    // Pull the first, second and third challenges and the plank recipe from the master
    var category = MasterHolder.ChallengeMaster.ChallengeCategoryMasterElements.Single(c => c.InitialUnlocked);
    var first = MasterHolder.ChallengeMaster.GetCategoryInitialChallenges(category.CategoryGuid).Single();
    var firstTask = (InInventoryItemTaskParam)first.TaskParam;
    var logName = MasterHolder.ItemMaster.GetItemMaster(firstTask.ItemGuid).Name;
    var second = MasterHolder.ChallengeMaster.GetNextChallenges(first.ChallengeGuid).Single();
    var secondTask = (InInventoryItemTaskParam)second.TaskParam;
    var third = MasterHolder.ChallengeMaster.GetNextChallenges(second.ChallengeGuid).Single();
    var plankId = MasterHolder.ItemMaster.GetItemId(secondTask.ItemGuid);
    var plankName = MasterHolder.ItemMaster.GetItemMaster(plankId).Name;
    var plankRecipe = MasterHolder.CraftRecipeMaster.GetResultItemCraftRecipes(plankId)[0];
    var plankAnchor = AnchorIds.RecipeItem(plankId.AsPrimitive());
    var plankCell = $"item-list-cell-{plankId.AsPrimitive()}";
    var craftButton = $"craft-button-{plankRecipe.CraftRecipeGuid}";

    // 1. スキットを飛ばし、原木を渡して先頭を終える（木の板 3 枚分を上乗せする）
    // 1. Skip the skit and hand over logs to finish the first challenge (plus three planks' worth)
    await p.SkipOpeningSkit();
    await p.GiveItemDirect(logName, firstTask.ItemCount + secondTask.ItemCount);
    await p.WaitForChallenge(first.ChallengeGuid, true, 30, "原木を渡して先頭チャレンジが完了した");
    await p.WaitForChallenge(second.ChallengeGuid, false, 30, "2 番目（木の板）がカレント");

    // 2. ゲーム画面で 2 番目の keyControl（Tab）のヒント
    // 2. On the game screen, the second challenge's keyControl (Tab) hint
    await AssertKeyHint(second, "01-tab-key-hint");

    // 3. Tab でインベントリを開くと木の板のセルに枠が付く
    // 3. Opening the inventory with Tab puts an outline on the plank cell
    await p.PressKey(Key.Tab);
    await p.WaitUiState(UIStateEnum.PlayerInventory, 15f);
    await p.Until(() => HasOutline(plankAnchor), 15f, $"store に木の板の枠（{plankAnchor}）がある");
    if (isGameUi) await p.Until(() => TutorialAnchorRegistry.Resolve(Root(), plankAnchor) is TutorialAnchorResolution.Unique, 10f, $"UITK で {plankAnchor} のアンカーが一意");
    await p.Screenshot("02-plank-outline");

    // 4. 木の板を押すとクラフトボタンに枠が移る
    // 4. Pressing the plank moves the outline to the craft button
    await p.UntilUiElement(plankCell, 10f);
    await p.ClickUi(plankCell);
    await p.UntilUiElement(craftButton, 10f);
    await p.Until(() => HasOutline(AnchorIds.RecipeCraftButton), 15f, "store にクラフトボタンの枠がある");
    if (isGameUi) await p.Until(() => TutorialAnchorRegistry.Resolve(Root(), AnchorIds.RecipeCraftButton) is TutorialAnchorResolution.Unique, 10f, "UITK で recipe.craft-button のアンカーが一意");
    await p.Screenshot("03-craft-button-outline");

    // 5. クラフトボタンを長押しして 1 枚作る（前例 uitk-recipe-craft）。残りはクラフト要求で作る
    // 5. Hold the craft button to make one plank (precedent uitk-recipe-craft); craft the rest by request
    var before = p.CountItem(plankName);
    var buttonRect = (await PlaytestUiElementOps.FindUi(craftButton)).ScreenRect;
    SemanticInput.MouseMoveTo(buttonRect.center);
    await p.WaitSeconds(0.5f);
    SemanticInput.MouseButtonDown(0);
    await p.Until(() => p.CountItem(plankName) > before, 3f * (float)plankRecipe.CraftTime + 3f, "クラフトボタンの長押しで木の板が増えた");
    SemanticInput.MouseButtonUp(0);
    await p.WaitSeconds(0.5f);
    p.Note($"長押しで {plankName} {before}→{p.CountItem(plankName)}");
    for (var made = p.CountItem(plankName); made < secondTask.ItemCount; made++) ClientContext.VanillaApi.SendOnly.Craft(plankRecipe.CraftRecipeGuid);
    await p.WaitForChallenge(second.ChallengeGuid, true, 30, $"{plankName}を {secondTask.ItemCount} 枚作って 2 番目が完了した");
    await p.WaitForChallenge(third.ChallengeGuid, false, 30, "3 番目のチャレンジがカレント");

    // 6. Tab で閉じると 3 番目の keyControl のヒントに替わる
    // 6. Closing with Tab switches to the third challenge's keyControl hint
    await p.PressKey(Key.Tab);
    await p.WaitUiState(UIStateEnum.GameScreen, 10f);
    await AssertKeyHint(third, "04-third-key-hint");

    #region Internal

    VisualElement Root() => GameUiRoot.Instance.GetRootElement();

    IEnumerable<TutorialOverlayElementData> Elements() => TutorialPresentationStateStore.Instance.GetCurrent().Sessions.SelectMany(s => s.Elements);

    bool HasOutline(string anchorId) => Elements().OfType<TutorialOutlineElementData>().Any(o => o.AnchorId == anchorId);

    // チャレンジの keyControl が store にマスタどおり載り、key-control-hint が出る
    // The challenge's keyControl sits in the store as the master says and key-control-hint shows
    async UniTask AssertKeyHint(ChallengeMasterElement challenge, string shot)
    {
        var tutorial = challenge.Tutorials.Single(t => t.TutorialType == TutorialsElement.TutorialTypeConst.keyControl);
        var param = (KeyControlTutorialParam)tutorial.TutorialParam;
        var guid = tutorial.TutorialGuid.ToString("D");
        await p.Until(() => Elements().OfType<TutorialKeyControlElementData>().Any(k => k.TutorialGuid == guid && k.KeyName == param.KeyName), 15f, $"store に {challenge.Title} の keyControl（{param.KeyName}）がある");
        var deadline = Time.realtimeSinceStartup + 15f;
        var found = (await PlaytestUiElementOps.FindUi("key-control-hint")).IsFound;
        while (!found && Time.realtimeSinceStartup < deadline)
        {
            await UniTask.DelayFrame(1);
            found = (await PlaytestUiElementOps.FindUi("key-control-hint")).IsFound;
        }
        p.Assert(found, $"{challenge.Title} の key-control-hint が出た");
        await p.Screenshot(shot);
    }

    #endregion
});
