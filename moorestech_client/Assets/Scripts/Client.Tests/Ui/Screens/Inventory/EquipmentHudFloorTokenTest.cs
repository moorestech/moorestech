using System.Linq;
using Client.Tests.Ui.Content;
using Client.Tests.Ui.Screens.Inventory.Support;
using Client.Ui.Content;
using Client.Ui.Foundation.Layers;
using Client.Ui.Foundation.Stage;
using Client.Ui.Screens.Inventory.Equipment;
using Client.Ui.Screens.Inventory.Parts;
using Client.Ui.State.Inventory.Panel;
using Client.Ui.State.Items;
using Core.Master;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.Inventory
{
    [TestFixture(1280f, 720f, 1f)]
    [TestFixture(1706.6666f, 720f, 1.5f)]
    public class EquipmentHudFloorTokenTest : ScreenTestHost
    {
        // Tokens.uss の --hotbar-bottom 2px・--equipment-right 24px・--hotbar-slot-size 51.68px
        // Tokens.uss --hotbar-bottom 2px, --equipment-right 24px, --hotbar-slot-size 51.68px
        private const float HotbarBottom = 2f;
        private const float EquipmentRight = 24f;
        private const float HotbarSlotSize = 51.68f;
        private readonly Vector2 _logicalPanelSize;
        private readonly float _pixelsPerPoint;
        private Texture2D _texture;

        public EquipmentHudFloorTokenTest(float width, float height, float panelPixelsPerPoint)
        {
            _logicalPanelSize = new Vector2(width, height);
            _pixelsPerPoint = panelPixelsPerPoint;
        }

        [OneTimeSetUp]
        public void SetUpLogicalPanelSize()
        {
            panelSize = _logicalPanelSize;
        }

        [SetUp]
        public void SetUpPanelPixelsPerPoint()
        {
            pixelsPerPoint = _pixelsPerPoint;
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_texture);
        }

        [Test]
        public void 装備の列はホットバーと同じ床と枠の大きさで右下に立つ()
        {
            _texture = new Texture2D(4, 4) { hideFlags = HideFlags.DontSave };
            var view = new EquipmentHudView(new PlayerSlotGestureRouter(new RecordingPlayerSlotCommands(), Toaster), new GameContentPresentation(new FakeContentTextureSource(_texture, _texture, _texture)));
            var layer = UiLayerStack.Build(rootVisualElement).GetLayer(UiLayer.Chrome, UiFamily.Viewport);
            layer.Add(view);
            var empty = new SlotItem(ItemMaster.EmptyItemId, 0);
            view.SetShown(true);
            view.Render(new PlayerInventoryViewModel(new[] { empty }, new[] { empty, empty, empty }, 0, new System.Collections.Generic.Dictionary<int, string>()));
            simulate.FrameUpdate();

            Assert.AreEqual(layer.worldBound.yMax - HotbarBottom, view.worldBound.yMax, 0.5f, "床");
            Assert.AreEqual(layer.worldBound.xMax - EquipmentRight, view.worldBound.xMax, 0.5f, "右");
            var slot = view.Query<VisualElement>(className: "item-slot").First();
            Assert.AreEqual(HotbarSlotSize, slot.worldBound.width, 0.5f, "枠の大きさ");
        }
    }
}
