// 持ち物パネルと装備 HUD（S4-inventory）を UITK（GameUi）で開き、整理・装備への出し入れ・ホイール・チェストへの Shift 移動を通す
// Opens the inventory panel and equipment HUD (S4-inventory) on UITK (GameUi) and runs sort, equipment moves, the wheel and Shift move into a chest
// 実行前に PlayMode を止め、execute-dynamic-code で UiSystemEditorSwitch.Set(UiSystem.GameUi) を立ててから run-scenario.sh を呼ぶ
// Stop PlayMode and call UiSystemEditorSwitch.Set(UiSystem.GameUi) via execute-dynamic-code before run-scenario.sh
// 実マスタの装備枠は 1（equipmentSlotCount: 1）なので、ホイールは Web と同じく選択を動かさないこと・印の判定が本番の拾いを通ることを主張する
// The real master has one equipment slot (equipmentSlotCount: 1), so the wheel asserts Web-equal no-op selection and that the mark judgment runs on the real pick
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.Inventory.Equipment;
using Client.Game.InGame.UI.UIState;
using Client.Input;
using Client.Input.Pointer;
using Client.Playtest;
using Client.Playtest.Input;
using Client.Playtest.Operations;
using Client.Ui.Foundation.Root;
using Client.Ui.Screens.Inventory.Wheel;
using Client.Ui.State.Inventory.Actions;
using Client.Ui.State.Inventory.Equipment;
using Client.Ui.State.SlotCommands.Block;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using UniRx;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using VContainer;

const string Wood = "原木";
var chestPosition = new Vector3Int(0, 32, 0);
return PlaytestRunner.Run("uitk-inventory-equipment", new PlaytestRunOptions { Record = true, ScenarioTimeoutSeconds = 240f }, async p =>
{
    // 実行中の Error・Exception ログを数え、最後に 0 件を主張する
    // Count Error/Exception logs during the run and assert zero at the end
    var errorCount = 0;
    Application.logMessageReceived += CountErrors;
    var resolver = ClientDIContext.DIContainer.DIContainerResolver;
    var operations = resolver.Resolve<IPlayerInventoryOperations>();
    var equipment = resolver.Resolve<LocalPlayerEquipment>();
    var wheelNotches = new List<float>();
    var wheelSubscription = resolver.Resolve<InputSystemPointerWheelStream>().OnWheelNotchesScrolled.Subscribe(wheelNotches.Add);
    try
    {
        p.Assert(UiInputExclusivity.ActiveUiSystem == UiSystem.GameUi, $"全体スイッチが UITK（UiInputExclusivity.ActiveUiSystem={UiInputExclusivity.ActiveUiSystem}）");
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        SemanticInput.EnsureDevices();
        InputSystem.EnableDevice(Keyboard.current);
        InputSystem.EnableDevice(Mouse.current);
        // 1. 平地で原木 10 を持ち、装備の枠数を実測する
        // 1. Hold 10 wood on flat ground and measure the equipment slot count
        await p.SetupFlatGround();
        await p.SkipOpeningSkitIfPlaying();
        await p.Until(() => p.CurrentUiState == UIStateEnum.GameScreen && Camera.main != null, 15f, "GameScreen");
        await p.GiveItem(Wood, 10);
        var woodSlot = MainIndexOf(10);
        p.Assert(equipment.Slots.Count == 1, $"実マスタの装備の枠は 1（循環の主張は 1 枠用に組む。実測 {equipment.Slots.Count}・原木 10 は main {woodSlot}）");
        // 2. 持ち物を開く → Inv 列に持ち物パネル、原木の枠に 10
        // 2. Open the inventory -> the panel sits in the Inv column and the wood slot shows 10
        await p.PressKey(Key.Tab);
        await p.WaitUiState(UIStateEnum.PlayerInventory, 15f);
        await p.UntilUiElement($"main-slot-{woodSlot}", 10f);
        p.Assert(IsInInvColumn(), "inventory-panel が stage-column-inv の中に表示される");
        await p.Screenshot("01-inventory-open");
        // 3. 整理を押しても Error ログが増えない
        // 3. Pressing sort adds no error logs
        var errorsBeforeSort = errorCount;
        await p.ClickUi("inventory-sort");
        await p.WaitSeconds(0.5f);
        p.Assert(errorCount == errorsBeforeSort, $"整理の前後で Error ログが増えない ({errorsBeforeSort} → {errorCount})");
        woodSlot = MainIndexOf(10);
        // 4. 装備の初期品を空きへ退け、原木 10 を装備 0 番へ置く
        // 4. Move the initial equipment out to an empty slot, then put the 10 wood into equipment 0
        if (!operations.ReadSnapshot().Equipment[0].IsEmpty)
        {
            var parking = Enumerable.Range(0, operations.ReadSnapshot().MainSlots.Count).First(i => operations.ReadSnapshot().MainSlots[i].IsEmpty);
            p.Note($"装備 0 番の初期品 {operations.ReadSnapshot().Equipment[0]} を main {parking} へ退ける");
            await p.ClickUi("equipment-slot-0");
            await p.ClickUi($"main-slot-{parking}");
            await p.Until(() => operations.ReadSnapshot().Equipment[0].IsEmpty && operations.ReadGrab().IsEmpty, 5f, "装備 0 番が空・掴みが空");
        }
        await p.ClickUi($"main-slot-{woodSlot}");
        await p.Until(() => operations.ReadGrab().Count == 10, 5f, "左押下で掴みに 10");
        await p.ClickUi("equipment-slot-0");
        await p.Until(() => operations.ReadSnapshot().Equipment[0].Count == 10 && operations.ReadGrab().IsEmpty, 5f, "装備 0 番に 10・掴みが空");
        // 5. 装備の枠を押すと掴みに移り選択は変わらない → 元へ戻す
        // 5. Pressing the equipment slot grabs it without changing selection -> put it back
        var selectedBefore = equipment.SelectedIndex;
        await p.ClickUi("equipment-slot-0");
        await p.Until(() => operations.ReadGrab().Count == 10 && operations.ReadSnapshot().Equipment[0].IsEmpty, 5f, "装備の枠の左押下で掴みに 10");
        p.Assert(equipment.SelectedIndex == selectedBefore, $"装備の枠の押下で選択が変わらない ({selectedBefore} → {equipment.SelectedIndex})");
        await p.ClickUi("equipment-slot-0");
        await p.Until(() => operations.ReadSnapshot().Equipment[0].Count == 10 && operations.ReadGrab().IsEmpty, 5f, "装備 0 番へ戻す");
        // 6. ゲーム画面でホイール: ノッチは本番の流れに届くが、1 枠では選択も端数も動かない（Web も 1 枠以下は送らない）
        // 6. Wheel on the game screen: notches reach the production stream, but one slot moves neither selection nor remainder (Web sends nothing either)
        await p.PressKey(Key.Tab);
        await p.WaitUiState(UIStateEnum.GameScreen, 15f);
        wheelNotches.Clear();
        await Wheel(1, "ゲーム画面で下へ 1 ノッチ");
        await Wheel(1, "ゲーム画面で下へもう 1 ノッチ（Web の 2 ノッチ目の地点）");
        await Wheel(2, "ゲーム画面でさらに 2 ノッチ（Web なら末尾から先頭へ循環する地点）");
        p.Assert(wheelNotches.Count == 4 && wheelNotches.All(n => n < 0f), $"注入した 4 ノッチが本番の InputSystemPointerWheelStream に下向きで届く [{string.Join(",", wheelNotches)}]");
        // 7. 持ち物を開き、パネルの上は奪う UI・装備 HUD の上は素通し（Task 9）と本番の拾いで判定される
        // 7. Open the inventory; the real pick judges the panel as blocking UI and the equipment HUD as passthrough (Task 9)
        await p.PressKey(Key.Tab);
        await p.WaitUiState(UIStateEnum.PlayerInventory, 15f);
        var picker = resolver.Resolve<IEquipmentWheelPointerPicker>();
        await p.HoverUi("inventory-panel");
        p.Assert(EquipmentWheelPickJudge.IsBlockingUi(picker.PickAt(SemanticInput.CurrentMousePosition())), "パネルの上はホイールを奪う UI");
        await Wheel(2, "パネルの上で 2 ノッチ");
        await p.HoverUi("equipment-slot-0");
        p.Assert(!EquipmentWheelPickJudge.IsBlockingUi(picker.PickAt(SemanticInput.CurrentMousePosition())), "装備 HUD の上は素通し（奪わない）");
        await Wheel(2, "装備 HUD の上で 2 ノッチ");
        await p.PressKey(Key.Tab);
        await p.WaitUiState(UIStateEnum.GameScreen, 15f);
        // 8. alpha D-33: チェストを開いた画面で持ち物の枠を Shift＋左押下 → チェストへ移る
        // 8. alpha D-33: with a chest open, Shift + left press on a main slot moves it into the chest
        await p.PlaceBlockDirect("木のチェスト", chestPosition, BlockDirection.North);
        await p.WaitBlockGameObject(chestPosition);
        await p.GiveItem(Wood, 5);
        var fiveSlot = MainIndexOf(5);
        p.WarpPlayer(new Vector3(chestPosition.x + 0.5f, 32.5f, chestPosition.z - 1.5f));
        await p.Until(() => Mathf.Abs(p.PlayerPosition.y - 32f) < 0.05f, 5f, "着地");
        SemanticInput.KeyDown(Key.LeftAlt);
        await p.AimAt(new Vector3(chestPosition.x + 0.5f, 32.5f, chestPosition.z));
        await p.PressInteract();
        await p.WaitUiState(UIStateEnum.SubInventory, 10f);
        SemanticInput.KeyUp(Key.LeftAlt);
        await p.UntilUiElement("chest-grid-slot-0", 10f);
        p.Assert(IsInInvColumn(), "チェストを開いた画面でも inventory-panel が Inv 列に出る");
        // UITK の shiftKey は PanelEventHandler が IMGUI の Event 列から読み、DSL の Shift 注入では立たない。alpha の uitk-block-inventory と同じく押下の間だけ静的値へ Shift を置く
        // UITK's shiftKey comes from PanelEventHandler's IMGUI queue, not DSL-injected keys; as in alpha's uitk-block-inventory, hold Shift in that static only during the press
        var uitkModifiers = typeof(PanelEventHandler).GetField("s_Modifiers", BindingFlags.NonPublic | BindingFlags.Static);
        if (uitkModifiers == null) throw new System.InvalidOperationException("PanelEventHandler.s_Modifiers が見つからない（Unity の版で内部名が変わった）ので Shift を注入できない");
        SemanticInput.KeyDown(Key.LeftShift);
        uitkModifiers.SetValue(null, EventModifiers.Shift);
        await p.ClickUi($"main-slot-{fiveSlot}");
        uitkModifiers.SetValue(null, EventModifiers.None);
        SemanticInput.KeyUp(Key.LeftShift);
        await p.Until(() => operations.ReadSnapshot().MainSlots[fiveSlot].IsEmpty && ChestSlot(0) == 5, 5f, $"Shift: main {fiveSlot} が空・チェストの枠 0 に 5（クライアント）");
        var serverCount = -1;
        var chestInventory = p.GetBlock(chestPosition).GetComponent<IOpenableBlockInventoryComponent>();
        await p.RunOnServer("サーバーのチェストの枠 0 を読む", () => serverCount = chestInventory.GetItem(0).Count);
        p.Assert(serverCount == 5, $"サーバーのチェストの枠 0 に 5 (実測 {serverCount})");
        await p.Screenshot("02-shift-moved-to-chest");
        await p.ClickUi("block-inventory-close");
        await p.WaitUiState(UIStateEnum.GameScreen, 10f);
    }
    finally
    {
        Application.logMessageReceived -= CountErrors;
        wheelSubscription.Dispose();
    }

    // 9. 実行中の Error ログが 0 件
    // 9. No error logs during the run
    p.Assert(errorCount == 0, $"Error ログ 0 件 (count={errorCount})");

    #region Internal

    void CountErrors(string condition, string stackTrace, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception) errorCount++;
    }
    int MainIndexOf(int count)
    {
        var slots = operations.ReadSnapshot().MainSlots;
        var wood = PlaytestItemOps.ResolveItemId(Wood);
        return Enumerable.Range(0, slots.Count).First(i => slots[i].ItemId == wood && slots[i].Count == count);
    }
    int ChestSlot(int index) => resolver.Resolve<IBlockInventoryOperations>().TryReadBlockSlots(out var slots) && index < slots.Count ? slots[index].Count : -1;
    bool IsInInvColumn()
    {
        var panel = GameUiRoot.Instance.GetRootElement().Q<VisualElement>("inventory-panel");
        var column = GameUiRoot.Instance.GetRootElement().Q<VisualElement>("stage-column-inv");
        return panel != null && column != null && column.Contains(panel) && panel.resolvedStyle.display == DisplayStyle.Flex && panel.worldBound.width > 0f;
    }
    // ノッチを 1 つずつ別フレームで注入し、選択が 0 のまま・セレクタの端数が溜まらないことを見る
    // Inject notches one frame apart and check selection stays 0 and the selector accumulates no remainder
    async UniTask Wheel(int notches, string label)
    {
        var selector = resolver.Resolve<EquipmentWheelSelector>();
        for (var i = 0; i < notches; i++)
        {
            SemanticInput.MouseWheel(-1f);
            await UniTask.DelayFrame(3);
        }
        await p.WaitSeconds(0.5f);
        var remainder = (double)typeof(EquipmentWheelSelector).GetField("_remainder", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(selector);
        p.Note($"{label}: SelectedIndex={equipment.SelectedIndex} 端数={remainder}");
        p.Assert(equipment.SelectedIndex == 0 && remainder == 0d, $"{label}: 選択 0 のまま・端数 0（1 枠では累積もしない）");
    }

    #endregion
});
