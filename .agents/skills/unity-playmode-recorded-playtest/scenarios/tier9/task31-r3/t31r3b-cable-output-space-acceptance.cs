// Task31 r3-B F01 の受入判定（赤い確認）。直る前は Success=false になるのが正しい。観測の成功は同ディレクトリの observe シナリオが担う
// 判定1: 出口空き不足で止まった機械パネルが、同じ機械の入力切れ待機と数字を除いて同じなら、原因を画面で区別できない（文言に依存しない）
// 判定2: 停止中の機械パネルに出力側の空き不足を示す語がある（評価役Bの期待。文言は日英の候補語で見る）
// 前提はTier8終盤セーブの複製（PLAYTEST_WORLD_DIRECTORY・template・seed0）。裏口は研究完了扱い・建設費と在庫の付与・ワープ・入力有効化・一時言語だけ
// Acceptance check for Task31 r3-B F01 (red check). Before a fix, Success=false is the correct outcome; the observe scenario owns the diagnostic success
// Check 1: if the panel stalled on output space equals the same machine idling on missing input (digits removed), the cause is indistinguishable on screen (wording-independent)
// Check 2: the stalled panel contains a word for output-side space shortage (evaluator B's expectation; JA/EN candidate words)
// Precondition is a copy of the late-Tier8 save (PLAYTEST_WORLD_DIRECTORY, template, seed 0). Backdoors: research completion, cost/stock grants, warps, input enabling, temporary language
using System;
using System.Linq;
using System.Text.RegularExpressions;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.Inventory.Main;
using Client.Playtest;
using Client.Playtest.Input;
using Client.Playtest.Operations;
using Client.Playtest.WebUi;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.Machine;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.Research;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;

// 非前面Editorでも注入入力が届くよう、ハーネス限定で入力デバイスを有効化する
// Enable input devices for this harness only so injected input reaches an unfocused Editor
InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
SemanticInput.EnsureDevices();
InputSystem.EnableDevice(Keyboard.current);
InputSystem.EnableDevice(Mouse.current);
UnityEditor.EditorWindow.GetWindow(typeof(UnityEditor.EditorWindow).Assembly.GetType("UnityEditor.GameView")).Focus();

return PlaytestRunner.Run("t31r3b-acceptance", new PlaytestRunOptions { Record = true, ScenarioTimeoutSeconds = 420f }, async p =>
{
    var recipeGuid = "01a167e9-21eb-7e67-b6ee-1d6503d8af84";
    var origin = new Vector3Int(714, 96, 616);

    // 裏口: 評価役Bと同じ日本語表示へ、保存しない一時言語として切り替える
    // Backdoor: switch to Japanese like evaluator B, as a temporary language that is not persisted
    var applyLanguage = typeof(Client.Localization.Localize).GetMethod("TryApplyLanguage", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
    p.Note($"LANGUAGE japaneseApplied={applyLanguage.Invoke(null, new object[] { "japanese", Client.Localization.LanguageOrigin.Unchosen })}");

    // 裏口: 軌道エレベーター研究と先行研究を完了扱いにし、建設費と前段素材を付与する
    // Backdoor: mark the orbital-elevator research chain complete and grant build costs and upstream materials
    await p.RunOnServer("軌道エレベーター研究と先行研究を完了扱いにする", () =>
    {
        var store = (ResearchDataStore)ServerContext.GetService<IResearchDataStore>();
        var save = store.GetSaveJsonObject();
        Add(MasterHolder.ResearchMaster.GetAllResearches().Single(r => r.ResearchNodeName == "軌道エレベーターの建設").ResearchNodeGuid);
        store.LoadResearchData(save);
        void Add(Guid id)
        {
            if (!save.CompletedResearchGuids.Contains(id.ToString())) save.CompletedResearchGuids.Add(id.ToString());
            foreach (var previous in MasterHolder.ResearchMaster.GetResearch(id).PrevResearchNodeGuids) Add(previous);
        }
    });
    await p.PrepareBlockForUiPlacement("広範囲電柱", 1);
    await p.PrepareBlockForUiPlacement("電気汎用工作装置", 1);
    await p.GiveItemDirect("銅のワイヤー", 64);
    await p.GiveItemDirect("グラフェンケーブル", 998);
    await p.GiveItemDirect("グラフェンシート", 1);
    await p.Hotbar.UnlockConnectTool("電線");
    var cable = PlaytestItemOps.ResolveItemId("グラフェンケーブル");
    var sheet = PlaytestItemOps.ResolveItemId("グラフェンシート");
    var recipeTicks = GameUpdater.SecondsToTicks(MasterHolder.MachineRecipesMaster.GetRecipeElement(Guid.Parse(recipeGuid)).Time);

    // 既存工場の電源へ電柱と工作装置をUIで建て、Fで開いてレシピをUI選択する
    // Build a pole and the machine on the existing grid via UI, open it with F, and select the recipe via UI
    p.WarpPlayer(new Vector3(715, 97.5f, 608));
    await p.PlaceBlockViaUi("広範囲電柱", new Vector3Int(715, 96, 612), BlockDirection.North);
    await p.ExitToGameScreen();
    p.WarpPlayer(new Vector3(715, 97.5f, 612));
    await p.PlaceBlockViaUi("電気汎用工作装置", origin, BlockDirection.North);
    await p.WaitBlockGameObject(origin);
    await p.ExitToGameScreen();
    p.WarpPlayer(new Vector3(715.5f, 97.5f, 615.8f));
    await p.WaitSeconds(1f);
    await p.PressInteract();
    await p.UntilWebUiElement("block-inventory", 15f);
    await PlaytestWebUiOps.ScrollDownUntilWebUiElementIsClickable($"machine-recipe-{recipeGuid}", "block-inventory", 30f);
    await p.ClickWebUi($"machine-recipe-{recipeGuid}");

    // 出口998・入口1をUIで投入し、レシピ1回分以上のゲーム時間と実10秒以上を待つ
    // Insert 998 into the outlet and 1 into the inlet via UI, then wait at least one recipe of game time and 10 real seconds
    var local = ClientDIContext.DIContainer.DIContainerResolver.Resolve<ILocalPlayerInventory>();
    await p.ClickWebUi("main-slot-" + FindSlot(cable));
    await p.ClickWebUi("machine-output-slots");
    await p.ClickWebUi("main-slot-" + FindSlot(sheet));
    await p.ClickWebUi("machine-input-slots");
    var block = p.GetBlock(origin);
    var inventory = block.GetComponent<IOpenableBlockInventoryComponent>();
    await p.WaitForItemCount(inventory, cable, 998, 998, 10f, "出口998個をUIで投入");
    await p.WaitForItemCount(inventory, sheet, 1, 1, 10f, "入口シート1個をUIで投入");
    var stallStartTick = GameUpdater.CurrentTick;
    var stallStartReal = Time.realtimeSinceStartup;
    while (Time.realtimeSinceStartup - stallStartReal < 10f || GameUpdater.CurrentTick - stallStartTick < recipeTicks) await p.WaitSeconds(1f);
    var stalledState = await p.ReadMachineState(block);
    p.Note($"STALLED state={stalledState} out={await p.ReadItemCount(inventory, cable)} in={await p.ReadItemCount(inventory, sheet)} ticks={GameUpdater.CurrentTick - stallStartTick}");
    p.Assert(stalledState == ProcessState.Idle, "前提: 出口空き不足で加工が始まらずIdleのまま");

    // 停止中のパネルを採って判定2を見る
    // Capture the stalled panel and evaluate check 2
    await p.Screenshot("01-stalled-output-space");
    var stalledPanel = (await PlaytestDomQuery.Query("block-inventory", 2f)).Text ?? "";
    p.Note("STALLED_PANEL_TEXT: " + stalledPanel.Replace("\n", "|"));
    var outputReasonWords = new[] { "出力", "出口", "いっぱい", "満杯", "空き", "Output", "output", "full", "space" };
    p.Assert(outputReasonWords.Any(stalledPanel.Contains), "受入2: 停止中の機械パネルに出力側の空き不足が示される");

    // 出口だけUIで空けて本物の加工を完了させ、入力切れで待機する同じ機械のパネルを採る
    // Clear only the outlet via UI, let real processing finish, then capture the same machine idling on missing input
    await p.ClickWebUi("machine-output-slots");
    await p.ClickWebUi("main-slot-" + FindEmpty());
    await p.WaitForItemCount(inventory, cable, 5, 5, 150f, "出口を空けると実加工でケーブル5個が完成");
    await p.WaitForItemCount(inventory, sheet, 0, 0, 10f, "入口シートが本物の加工で消費された");
    await p.WaitSeconds(1f);
    p.Assert(await p.ReadMachineState(block) == ProcessState.Idle, "前提: 加工後は入力切れでIdle");
    await p.Screenshot("02-idle-missing-input");
    var starvedPanel = (await PlaytestDomQuery.Query("block-inventory", 2f)).Text ?? "";
    p.Note("INPUT_STARVED_PANEL_TEXT: " + starvedPanel.Replace("\n", "|"));

    // 判定1: 数字を除いた2つのパネルが同じなら、出口空き不足と入力切れを画面で区別できない
    // Check 1: if both panels match with digits removed, output-space shortage and missing input are indistinguishable on screen
    var stalledShape = Regex.Replace(stalledPanel, "[0-9]", "");
    var starvedShape = Regex.Replace(starvedPanel, "[0-9]", "");
    p.Note($"SHAPE_EQUAL={stalledShape == starvedShape} stalled=[{stalledShape.Replace("\n", "|")}]");
    p.Assert(stalledShape != starvedShape, "受入1: 出口空き不足の停止が、入力切れの待機と機械パネル上で区別できる");

    int FindSlot(ItemId id)
    {
        for (var i = 0; i < local.MainSlotCount; i++) if (local[i].Id == id && local[i].Count > 0) return i;
        throw new InvalidOperationException("持ち物にない: " + id);
    }
    int FindEmpty()
    {
        for (var i = 0; i < local.MainSlotCount; i++) if (local[i].Count == 0) return i;
        throw new InvalidOperationException("持ち物に空き枠がない");
    }
});
