// Task31 r3-B F01 の独立再現（観測run）。全アサートが通れば「故障状態を再現できた」ことを意味し、受入の合否ではない
// 前提はTier8終盤セーブの複製（PLAYTEST_WORLD_DIRECTORY・template・seed0）。裏口は研究完了扱い・建設費と在庫の付与・ワープ・入力有効化だけ
// 設置・レシピ選択・投入・回収はWeb UI、待機と加工は実時間。受入の赤判定は同ディレクトリの acceptance シナリオが担う
// Independent repro of Task31 r3-B F01 (observation run). All asserts passing means the failure state was reproduced, not acceptance
// Precondition is a copy of the late-Tier8 save (PLAYTEST_WORLD_DIRECTORY, template, seed 0). Backdoors: research completion, cost/stock grants, warps, input enabling
// Placement, recipe choice, insertion and removal go through the Web UI; waiting and processing run in real time. The red acceptance verdict lives in the sibling acceptance scenario
using System;
using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.Inventory.Main;
using Client.Playtest;
using Client.Playtest.Input;
using Client.Playtest.Operations;
using Client.Playtest.WebUi;
using Core.Item;
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

return PlaytestRunner.Run("t31r3b-observe", new PlaytestRunOptions { Record = true, ScenarioTimeoutSeconds = 420f }, async p =>
{
    var recipeGuid = "01a167e9-21eb-7e67-b6ee-1d6503d8af84";
    var origin = new Vector3Int(714, 96, 616);

    // 裏口: Bの観測と同じ日本語表示へ、保存しない一時言語として切り替える
    // Backdoor: switch to Japanese like B's observation, as a temporary language that is not persisted
    var applyLanguage = typeof(Client.Localization.Localize).GetMethod("TryApplyLanguage", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
    var japaneseApplied = (bool)applyLanguage.Invoke(null, new object[] { "japanese", Client.Localization.LanguageOrigin.Unchosen });
    p.Note($"LANGUAGE japaneseApplied={japaneseApplied} current={Client.Localization.Localize.GetCurrentLanguageCode()}");
    await p.WaitSeconds(1f);

    // 裏口: 軌道エレベーター研究と先行研究を完了扱いにし、解放を再適用する
    // Backdoor: mark the orbital-elevator research and its prerequisites complete, then re-apply unlocks
    await p.RunOnServer("軌道エレベーター研究と先行研究を完了扱いにする", () =>
    {
        var store = (ResearchDataStore)ServerContext.GetService<IResearchDataStore>();
        var save = store.GetSaveJsonObject();
        var target = MasterHolder.ResearchMaster.GetAllResearches().Single(r => r.ResearchNodeName == "軌道エレベーターの建設");
        Add(target.ResearchNodeGuid);
        store.LoadResearchData(save);
        void Add(Guid id)
        {
            if (!save.CompletedResearchGuids.Contains(id.ToString())) save.CompletedResearchGuids.Add(id.ToString());
            foreach (var previous in MasterHolder.ResearchMaster.GetResearch(id).PrevResearchNodeGuids) Add(previous);
        }
    });

    // 裏口: 建設費と前段素材を付与する。設置・移動・加工はこの後すべて実UIと実時間
    // Backdoor: grant build costs and upstream materials; placement, moves and processing are real afterwards
    await p.PrepareBlockForUiPlacement("広範囲電柱", 1);
    await p.PrepareBlockForUiPlacement("電気汎用工作装置", 1);
    await p.GiveItemDirect("銅のワイヤー", 64);
    await p.GiveItemDirect("グラフェンケーブル", 998);
    await p.GiveItemDirect("グラフェンシート", 1);
    await p.Hotbar.UnlockConnectTool("電線");

    var cable = PlaytestItemOps.ResolveItemId("グラフェンケーブル");
    var sheet = PlaytestItemOps.ResolveItemId("グラフェンシート");
    var recipe = MasterHolder.MachineRecipesMaster.GetRecipeElement(Guid.Parse(recipeGuid));
    var maxStack = ItemStackLevelDataStore.Instance.GetMaxStack(cable);
    var batch = recipe.OutputItems[0].Count;
    p.Note($"MASTER recipeTime={recipe.Time}s batch={batch} cableMaxStack={maxStack} freeAt998={maxStack - 998}");
    p.Assert(998 <= maxStack && maxStack - 998 < batch, "998個は上限内の合法在庫で、残り空きは次バッチ未満");

    // 既存工場の電源へ電柱をUIで足し、工作装置をUIで建設してFで開く
    // Add a pole to the existing grid via UI, build the machine via UI, and open it with F
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

    // レシピ選択リストに見える数量を記録してから、実ホイールで行まで送ってUI選択する
    // Record the quantities visible in the recipe list, then wheel to the row and select it via UI
    await PlaytestWebUiOps.ScrollDownUntilWebUiElementIsClickable($"machine-recipe-{recipeGuid}", "block-inventory", 30f);
    var listRow = await PlaytestDomQuery.Query($"machine-recipe-{recipeGuid}", 2f);
    p.Note($"RECIPE_LIST_ROW visible={listRow.Visible} text={listRow.Text?.Replace("\n", "|")}");
    await p.ClickWebUi($"machine-recipe-{recipeGuid}");

    // 出口998・入口1をUIで投入する
    // Insert 998 into the outlet and 1 into the inlet via UI
    var local = ClientDIContext.DIContainer.DIContainerResolver.Resolve<ILocalPlayerInventory>();
    await p.ClickWebUi("main-slot-" + FindSlot(cable));
    await p.ClickWebUi("machine-output-slots");
    await p.ClickWebUi("main-slot-" + FindSlot(sheet));
    await p.ClickWebUi("machine-input-slots");
    var block = p.GetBlock(origin);
    var inventory = block.GetComponent<IOpenableBlockInventoryComponent>();
    await p.WaitForItemCount(inventory, cable, 998, 998, 10f, "出口998個をUIで投入");
    await p.WaitForItemCount(inventory, sheet, 1, 1, 10f, "入口シート1個をUIで投入");

    // 実時間10秒以上かつゲーム時間でレシピ1回分以上を1秒刻みで観測し、一度も加工へ進まないことを値で残す
    // Sample at 1 s steps for at least 10 real seconds and one recipe duration of game ticks, recording that processing never starts
    var recipeTicks = GameUpdater.SecondsToTicks(recipe.Time);
    var stallStartTick = GameUpdater.CurrentTick;
    var stallStartReal = Time.realtimeSinceStartup;
    var states = new List<string>();
    while (states.Count < 120 && (Time.realtimeSinceStartup - stallStartReal < 10f || GameUpdater.CurrentTick - stallStartTick < recipeTicks))
    {
        await p.WaitSeconds(1f);
        var s = await p.ReadMachineState(block);
        states.Add($"{GameUpdater.CurrentTick - stallStartTick}t:{s}/out{await p.ReadItemCount(inventory, cable)}/in{await p.ReadItemCount(inventory, sheet)}");
    }
    var stallTicks = GameUpdater.CurrentTick - stallStartTick;
    p.Note($"SAMPLES real={Time.realtimeSinceStartup - stallStartReal:F1}s ticks={stallTicks} recipeTicks={recipeTicks} " + string.Join(" ", states));
    p.Assert(stallTicks >= recipeTicks && states.All(x => x.Contains(":Idle/out998/in1")), "レシピ1回分以上のゲーム時間と実時間10秒以上、Idle・出口998・入口1のまま加工が始まらない");

    // 停止中の機械パネル全文を採り、出力空き不足を示す語・上限・必要数が無いことを観測する
    // Capture the whole stalled panel text and observe that no output-space reason, cap or required count appears
    await p.Screenshot("01-stalled-998-panel");
    var panel = await PlaytestDomQuery.Query("block-inventory", 2f);
    var panelText = panel.Text ?? "";
    p.Note("STALLED_PANEL_TEXT: " + panelText.Replace("\n", "|"));
    var reasonWords = new[] { "出力", "出口", "いっぱい", "満杯", "空き", "不足", "停止中", "Output", "output", "full", "space", "blocked", "Insufficient", "Halted", maxStack.ToString() };
    var shownReasonWords = reasonWords.Where(panelText.Contains).ToArray();
    p.Note("REASON_WORDS_IN_PANEL [" + string.Join(",", shownReasonWords) + "]");
    p.Assert((panelText.Contains("待機中") || panelText.Contains("Standby")) && (panelText.Contains("電力 100%") || panelText.Contains("Power 100%")), "パネルは待機中・電力100%を表示する");
    p.Assert(shownReasonWords.Length == 0, "観測: パネルに出力空き不足の理由語・上限値が無い");

    // 出口スロットのホバーtooltipに上限や空きが出るかを観測する
    // Observe whether the outlet-slot hover tooltip shows a cap or free space
    await p.HoverWebUi("machine-output-slots");
    await p.WaitSeconds(1f);
    var tooltip = await PlaytestDomQuery.Query("cursor-tooltip", 2f);
    p.Note($"OUTPUT_HOVER_TOOLTIP found={tooltip.Found} text={tooltip.Text?.Replace("\n", "|")}");
    await p.Screenshot("02-output-hover-tooltip");

    // 選択中レシピの見出しから選択モードへ戻り、次バッチ数が見えるかを観測して同じ行で閉じる
    // Return to selection mode via the header, observe whether the batch size shows, and close with the same row
    await p.ClickWebUi("machine-selected-recipe");
    await PlaytestWebUiOps.ScrollDownUntilWebUiElementIsClickable($"machine-recipe-{recipeGuid}", "block-inventory", 30f);
    var outputCell = await PlaytestDomQuery.Query($"machine-recipe-{recipeGuid}-output-item-0", 2f);
    p.Note($"RECIPE_LIST_OUTPUT_CELL visible={outputCell.Visible} text={outputCell.Text?.Replace("\n", "|")}");
    await p.Screenshot("03-recipe-list-while-stalled");
    await p.ClickWebUi($"machine-recipe-{recipeGuid}");
    p.Assert(await p.ReadMachineState(block) == ProcessState.Idle && await p.ReadItemCount(inventory, sheet) == 1, "観測中の画面操作で状態は変わっていない");

    // 出口だけをUIで持ち物へ戻し、同じ入力・レシピ・電源で本物の加工が完了するかを見る
    // Move only the outlet back to the player via UI and check real processing completes with the same input, recipe and power
    await p.ClickWebUi("machine-output-slots");
    await p.ClickWebUi("main-slot-" + FindEmpty());
    var clearTick = GameUpdater.CurrentTick;
    await p.WaitForItemCount(inventory, cable, 5, 5, 150f, "出口を空けると実加工でケーブル5個が完成");
    p.Note($"RECOVERY ticks={GameUpdater.CurrentTick - clearTick} recipeTicks={recipeTicks}");
    p.Assert(await p.ReadItemCount(inventory, sheet) == 0, "入口シートが本物の加工で消費された");
    await p.Screenshot("04-produced-after-clearing");
    panel = await PlaytestDomQuery.Query("block-inventory", 2f);
    p.Note("RECOVERED_PANEL_TEXT: " + (panel.Text ?? "").Replace("\n", "|"));

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
