using System;
using Client.Game.InGame.UI.Notification;
using Client.Tests.Ui.State.BlockInventory.PanelModel;
using Client.Tests.Ui.State.Inventory;
using Client.Tests.Ui.State.Inventory.Drag;
using Client.Tests.Ui.State.Inventory.Equipment;
using Client.Tests.Ui.State.ItemMove;
using Client.Tests.Ui.State.SlotCommands;
using Client.Ui.State.BlockInventory.Panel;
using Client.Ui.State.BlockInventory.SlotRestriction;
using Client.Ui.State.Inventory.Actions;
using Client.Ui.State.Inventory.Drag;
using Client.Ui.State.Inventory.Panel;
using Client.Ui.State.SlotCommands;
using Client.Ui.State.SlotCommands.Block;
using Client.Ui.State.SlotCommands.Player;
using Client.Ui.State.Toast;
using UniRx;

namespace Client.Tests.Ui.Screens.Inventory.Support
{
    /// <summary>
    /// 持ち物と装備の L1 で使う本物のスロット操作一式（ブロックは閉じている）
    /// Real slot-command set for the inventory and equipment L1 (block closed)
    /// </summary>
    public sealed class InventoryScreenFixture
    {
        public RecordingPlayerInventoryOperations Operations { get; private set; }
        public FakePrimaryPointerButton Button { get; private set; }
        public PlayerSlotDragCommands Drag { get; private set; }
        public PlayerSlotCommands Commands { get; private set; }
        public FakeEquipmentSelectionPort Equipment { get; private set; }
        public PlayerInventoryPanelModel Model { get; private set; }

        public InventoryScreenFixture(int mainSlots, int equipmentSlots, UiCommandResultToaster toaster)
        {
            Operations = new RecordingPlayerInventoryOperations(mainSlots, equipmentSlots);
            var executor = new PlayerItemActionExecutor(Operations);
            Button = new FakePrimaryPointerButton(true);
            Drag = new PlayerSlotDragCommands(Operations, executor, Button, toaster);
            Drag.Initialize();

            // alpha の PlayerSlotCommandsTest と同じ組み方（パネルは閉じたまま）
            // Same build as alpha's PlayerSlotCommandsTest (the panel stays closed)
            var block = new RecordingBlockInventoryOperations();
            var runner = new PlannedItemActionRunner(executor, new BlockItemActionExecutor(block, Operations));
            var reader = new PlayerSlotContextReader(new FakePanelSource(new ReactiveProperty<BlockInventoryPanelState>(BlockInventoryPanelState.Closed)), block, new BlockSlotRestrictionInputBuilder(Array.Empty<IBlockSlotRestrictionContributor>()), new FixedMaxStackLookup(64));
            Commands = new PlayerSlotCommands(Operations, Drag, runner, reader, new ShiftMoveRefusalNotifier(new ClientLocalNotificationSource()));

            // 置く・移すは記録係が合図を流す。装備の選択の Fake は本番と同じく同じ 1 本の合図へ繋ぐ（本番は LocalPlayerEquipment の合図が中身と選択の両方を流す）
            // The recorder signals puts and moves; the selection fake joins the same single signal as in production (LocalPlayerEquipment signals both)
            Equipment = new FakeEquipmentSelectionPort(equipmentSlots);
            Equipment.OnSelectionChanged.Subscribe(_ => Operations.RaiseSnapshotChanged());
            Model = new PlayerInventoryPanelModel(Operations, Equipment);
            Model.Initialize();
        }
    }
}
