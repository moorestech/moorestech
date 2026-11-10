using System;
using System.Linq;
using Client.Game.InGame.UI.UIState;
using Client.Tests.Ui.Content;
using Client.Tests.Ui.Screens.Inventory.Support;
using Client.Tests.Ui.State.Inventory;
using Client.Tests.Ui.State.Inventory.Equipment;
using Client.Tests.Ui.State.Inventory.Panel;
using Client.Ui.Content;
using Client.Ui.Foundation.Stage.Columns;
using Client.Ui.Screens.Inventory.Equipment;
using Client.Ui.Screens.Inventory.Panel;
using Client.Ui.State.Inventory.Panel;
using Client.Ui.State.Items;
using Core.Master;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.Inventory
{
    public class InventoryScreenVisibilityTest : ScreenTestHost
    {
        private static readonly Guid WoodGuid = Guid.Parse("00000000-0000-0000-1234-000000000001");
        private GameObject _host;
        private Texture2D _texture;
        private RecordingPlayerInventoryOperations _operations;
        private RecordingPlayerSlotCommands _commands;
        private PlayerInventoryPanelModel _model;

        [SetUp]
        public void SetUp()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            _host = new GameObject("inventory-visibility");
            _texture = new Texture2D(4, 4) { hideFlags = HideFlags.DontSave };
            _operations = new RecordingPlayerInventoryOperations(36, 3);
            _commands = new RecordingPlayerSlotCommands();
            _model = new PlayerInventoryPanelModel(_operations, new FakeEquipmentSelectionPort(3));
            _model.Initialize();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_host);
            UnityEngine.Object.DestroyImmediate(_texture);
        }

        [TestCase(UIStateEnum.PlayerInventory, true)]
        [TestCase(UIStateEnum.ResearchTree, true)]
        [TestCase(UIStateEnum.BuildMenu, false)]
        public void 持ち物パネルは掴める画面だけに出て出ている間だけ描く(UIStateEnum state, bool shown)
        {
            var columns = new StageColumnsLayout();
            rootVisualElement.Add(columns);
            new InventoryScreenPresenter(columns, UiRoutingStateTestFactory.Create(_host, state), _model, _commands, new RecordingInventorySortCommands(), Content(), Toaster).Initialize();
            _operations.SetMain(0, new SlotItem(MasterHolder.ItemMaster.GetItemId(WoodGuid), 7));
            simulate.FrameUpdate();

            // 出ている画面では最新の値で描き、隠れた画面では購読しないので枠も作らない（§4.2）
            // Shown screens draw the latest value; hidden screens never subscribe, so no slots are built (§4.2)
            var view = columns.Q<InventoryView>();
            Assert.AreEqual(shown, IsShown(view), $"{state} の出し入れ");
            Assert.AreEqual(shown ? 36 : 0, view.Query(className: "slot-frame").ToList().Count, $"{state} の描き");
        }

        [TestCase(UIStateEnum.GameScreen, true)]
        [TestCase(UIStateEnum.ResearchTree, false)]
        [TestCase(UIStateEnum.BuildMenu, true)]
        public void 装備HUDは常駐HUDの画面だけに出る(UIStateEnum state, bool shown)
        {
            var hud = MountEquipmentHud(state);
            Assert.AreEqual(shown, IsShown(hud), $"{state}");
        }

        [TestCase(UIStateEnum.GameScreen, 0)]
        [TestCase(UIStateEnum.PlayerInventory, 1)]
        public void 装備の枠は掴める画面だけクリックを受ける(UIStateEnum state, int expectedCalls)
        {
            var frame = Find<VisualElement>(MountEquipmentHud(state), "equipment-slot-0");
            simulate.MouseDown(frame.worldBound.center, MouseButton.LeftMouse);
            simulate.MouseUp(frame.worldBound.center, MouseButton.LeftMouse);
            Assert.AreEqual(expectedCalls, _commands.Calls.Count, string.Join(",", _commands.Calls));
        }

        private EquipmentHudView MountEquipmentHud(UIStateEnum state)
        {
            var root = GameUiRootTestFactory.Create(_host, rootVisualElement);
            new EquipmentHudPresenter(root, UiRoutingStateTestFactory.Create(_host, state), _model, _commands, Content(), Toaster).Initialize();
            simulate.FrameUpdate();
            return rootVisualElement.Q<EquipmentHudView>();
        }

        private GameContentPresentation Content()
        {
            return new GameContentPresentation(new FakeContentTextureSource(_texture, _texture, _texture));
        }
    }
}
