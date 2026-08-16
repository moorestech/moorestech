using Client.Game.InGame.UI.Inventory.Main;
using Client.Game.InGame.UI.UIState;
using Client.WebUiHost.Game.Actions;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Client.Tests.WebUi
{
    public class CollectActionTest
    {
        // 研究拡張後の54スロットで検証し、固定45スロット仮定が残っていないことを確認する
        // Verify against 54 slots (post research-expansion) to confirm no fixed 45-slot assumption remains
        private const int MainSlotCount = 54;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }

        [Test]
        public void SplitDragCountUsesHostGrabAndDestinationCount()
        {
            Assert.AreEqual(3, SplitDragActionHandler.CalculateCountPerSlot(10, 3));
            Assert.AreEqual(0, SplitDragActionHandler.CalculateCountPerSlot(2, 3));
        }

        [TestCase(UIStateEnum.GameScreen, UIStateEnum.PlayerInventory, true)]
        [TestCase(UIStateEnum.PlayerInventory, UIStateEnum.GameScreen, true)]
        [TestCase(UIStateEnum.Story, UIStateEnum.GameScreen, false)]
        [TestCase(UIStateEnum.PauseMenu, UIStateEnum.PlayerInventory, false)]
        public void UiStateRequestWhitelistRejectsUnrelatedCurrentStates(UIStateEnum current, UIStateEnum requested, bool expected)
        {
            Assert.AreEqual(expected, RequestUiStateActionHandler.IsAllowed(current, requested));
        }

        // grab 保持時は常に Grab を集積先にする（クリックスロットは無視）
        // While holding grab, the target is always Grab (the clicked slot is ignored)
        [Test]
        public void ResolveCollectTargetGrabHeldTargetsGrab()
        {
            var (type, slot) = CollectActionHandler.ResolveCollectTarget(true, LocalMoveInventoryType.MainOrSub, 7);
            Assert.AreEqual(LocalMoveInventoryType.Grab, type);
            Assert.AreEqual(0, slot);
        }

        // 空手時はクリックされたスロットを集積先にする
        // Empty-handed targets the clicked slot
        [Test]
        public void ResolveCollectTargetEmptyHandedTargetsClickedSlot()
        {
            var (type, slot) = CollectActionHandler.ResolveCollectTarget(false, LocalMoveInventoryType.MainOrSub, 7);
            Assert.AreEqual(LocalMoveInventoryType.MainOrSub, type);
            Assert.AreEqual(7, slot);
        }

        // 装備枠を集積先に保持
        // Keep equipment as collect target
        [Test]
        public void ResolveCollectTargetEmptyHandedTargetsClickedEquipmentSlot()
        {
            var (type, slot) = CollectActionHandler.ResolveCollectTarget(false, LocalMoveInventoryType.Equipment, 2);
            Assert.AreEqual(LocalMoveInventoryType.Equipment, type);
            Assert.AreEqual(2, slot);
        }

        [TestCase(@"{""area"":""main"",""slot"":3}", LocalMoveInventoryType.MainOrSub, 3)]
        [TestCase(@"{""area"":""main"",""slot"":53}", LocalMoveInventoryType.MainOrSub, 53)]
        [TestCase(@"{""area"":""equipment"",""slot"":2}", LocalMoveInventoryType.Equipment, 2)]
        public void TryParseClickableSlotRefAcceptsClickableSlots(string json, LocalMoveInventoryType expectedType, int expectedSlot)
        {
            var ok = InventoryAreaMapper.TryParseClickableSlotRef(JToken.Parse(json), MainSlotCount, out var type, out var slot);
            Assert.IsTrue(ok);
            Assert.AreEqual(expectedType, type);
            Assert.AreEqual(expectedSlot, slot);
        }

        // grab は collect 入力として不正なので拒否する
        // grab is invalid as a collect input and is rejected
        [TestCase(@"{""area"":""grab""}")]
        [TestCase(@"{""area"":""grab"",""slot"":0}")]
        [TestCase(@"{""area"":""sub"",""slot"":0}")]
        [TestCase(@"{""slot"":3}")]
        [TestCase(@"{""area"":""equipment"",""slot"":3}")]
        [TestCase(@"null")]
        public void TryParseClickableSlotRefRejectsNonClickable(string json)
        {
            var ok = InventoryAreaMapper.TryParseClickableSlotRef(JToken.Parse(json), MainSlotCount, out _, out _);
            Assert.IsFalse(ok);
        }
    }
}
