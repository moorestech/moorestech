using Client.Game.InGame.UI.UIState;
using Client.Tests.Ui.Content;
using Client.Tests.Ui.Screens.Hotbar;
using Client.Tests.Ui.Screens.Inventory.Support;
using Client.Tests.Ui.State.Inventory.Equipment;
using Client.Ui.Content;
using Client.Ui.Foundation.Input;
using Client.Ui.Foundation.Layers;
using Client.Ui.Foundation.Stage;
using Client.Ui.Screens.Hotbar;
using Client.Ui.Screens.Inventory.Equipment;
using Client.Ui.Screens.Inventory.Parts;
using Client.Ui.Screens.Inventory.Wheel;
using Client.Ui.State.Inventory.Logic;
using Client.Ui.State.Inventory.Panel;
using Client.Ui.State.Items;
using Core.Master;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.Inventory
{
    /// <summary>
    /// 装備 HUD とホットバーの列は実 UI として拾われたままホイールを装備へ素通し、ホットバーの帯は拾わない（bd albof.47）
    /// The equipment HUD and the hotbar row stay pickable real UI but pass the wheel to equipment; the hotbar band does not pick (bd albof.47)
    /// </summary>
    public class EquipmentWheelPassthroughTest : ScreenTestHost
    {
        private GameObject _host;
        private FakeEquipmentSelectionPort _port;
        private EquipmentWheelRouterFixture _wheel;
        private VisualElement _viewport;
        private Texture2D _texture;

        [SetUp]
        public void SetUp()
        {
            _host = new GameObject("equipment-wheel-passthrough");
            _port = new FakeEquipmentSelectionPort(3);
            _wheel = new EquipmentWheelRouterFixture(_host, UIStateEnum.GameScreen, _port);
            _viewport = UiLayerStack.Build(rootVisualElement).GetLayer(UiLayer.Chrome, UiFamily.Viewport);
            _texture = new Texture2D(4, 4) { hideFlags = HideFlags.DontSave };
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_host);
            UnityEngine.Object.DestroyImmediate(_texture);
        }

        /// <web-test file="src/features/commonHud/alwaysOnHudWheel.test.ts">装備HUDは実UIのままホイールだけ素通す</web-test>
        [Test]
        public void 装備HUDは拾う実UIのままホイールだけ素通す()
        {
            var hud = MountEquipmentHud();
            var picked = rootVisualElement.panel.Pick(Find<VisualElement>(hud, "equipment-slot-0").worldBound.center);

            Assert.IsNotNull(picked, "実 UI として拾う");
            Assert.AreEqual(PickingMode.Position, hud.pickingMode);
            Assert.IsTrue(hud.ClassListContains(WheelPassthroughMark.ClassName));
            Assert.IsFalse(EquipmentWheelPickJudge.IsBlockingUi(picked), "ホイールは素通す");
        }

        /// <web-test file="e2e/tests/inventory/equipment.spec.ts">装備HUDの上でもホイールで装備が切り替わる</web-test>
        [Test]
        public void 装備HUDの上でも切り替わる()
        {
            var hud = MountEquipmentHud();
            WheelAt(Find<VisualElement>(hud, "equipment-slot-0").worldBound.center);
            CollectionAssert.AreEqual(new[] { 1 }, _port.Selections);
        }

        /// <web-test file="e2e/tests/inventory/equipment.spec.ts">ホットバーHUDの上でもホイールで装備が切り替わる</web-test>
        [Test]
        public void ホットバーHUDの上でも切り替わる()
        {
            // 列の中央は枠の上。拾われた枠は列の素通しの印の子孫なのでホイールが装備へ届く
            // The row centre is over a slot; the picked slot descends from the row's pass-through mark, so the wheel reaches equipment
            var hotbar = MountHotbarHud();
            var center = Find<VisualElement>(hotbar, "hotbar-grid").worldBound.center;
            Assert.IsNotNull(rootVisualElement.panel.Pick(center), "枠は実 UI として拾う");
            WheelAt(center);
            CollectionAssert.AreEqual(new[] { 1 }, _port.Selections);
        }

        /// <web-test file="e2e/tests/inventory/equipment.spec.ts">ホットバー帯のスロット列外でもホイールで装備が切り替わる</web-test>
        [Test]
        public void ホットバー帯のスロット列外でも切り替わる()
        {
            // Web と同じく列の左の何も無い所（列の左端の半分の x）。帯は全幅でそこを覆うが拾わない
            // Like the Web, empty space left of the row (half the row's left x); the full-width band covers it but does not pick
            var hotbar = MountHotbarHud();
            var row = Find<VisualElement>(hotbar, "hotbar-grid").worldBound;
            var outside = new Vector2(row.xMin / 2f, row.center.y);
            Assert.IsTrue(hotbar.worldBound.Contains(outside), "帯がその点を覆う");
            WheelAt(outside);
            CollectionAssert.AreEqual(new[] { 1 }, _port.Selections);
        }

        private HotbarHudView MountHotbarHud()
        {
            var hotbar = new HotbarHudView(new RecordingHotbarCommands(), HotbarTestPresentation.Create(), Toaster);
            _viewport.Add(hotbar);
            hotbar.Render(HotbarViewModelSamples.WebFixture());
            simulate.FrameUpdate();
            simulate.FrameUpdate();
            return hotbar;
        }

        private EquipmentHudView MountEquipmentHud()
        {
            var empty = new SlotItem(ItemMaster.EmptyItemId, 0);
            var hud = new EquipmentHudView(new PlayerSlotGestureRouter(new RecordingPlayerSlotCommands(), Toaster), new GameContentPresentation(new FakeContentTextureSource(_texture, _texture, _texture)));
            _viewport.Add(hud);
            hud.SetShown(true);
            hud.Render(new PlayerInventoryViewModel(new[] { empty }, new[] { empty, empty, empty }, 0, new System.Collections.Generic.Dictionary<int, string>()));
            simulate.FrameUpdate();
            return hud;
        }

        private void WheelAt(Vector2 panelPosition)
        {
            var picked = rootVisualElement.panel.Pick(panelPosition);
            _wheel.Router.Route(100, WheelDeltaUnit.Pixel, EquipmentWheelPickJudge.IsBlockingUi(picked));
        }
    }
}
