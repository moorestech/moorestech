using Client.Game.InGame.UI.Inventory.Main;
using Client.WebUiHost.Game.Actions;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Client.Tests.WebUi
{
    public class CollectActionTest
    {
        // grab 保持時は常に Grab を集積先にする（クリックスロットは無視）
        // While holding grab, the target is always Grab (the clicked slot is ignored)
        [Test]
        public void ResolveCollectTargetGrabHeldTargetsGrab()
        {
            var (type, slot) = CollectActionHandler.ResolveCollectTarget(true, 7);
            Assert.AreEqual(LocalMoveInventoryType.Grab, type);
            Assert.AreEqual(0, slot);
        }

        // 空手時はクリックされたスロットを集積先にする
        // Empty-handed targets the clicked slot
        [Test]
        public void ResolveCollectTargetEmptyHandedTargetsClickedSlot()
        {
            var (type, slot) = CollectActionHandler.ResolveCollectTarget(false, 7);
            Assert.AreEqual(LocalMoveInventoryType.MainOrSub, type);
            Assert.AreEqual(7, slot);
        }

        // クリック可能スロット（main/hotbar）は受理する
        // Clickable slots (main/hotbar) are accepted
        [TestCase(@"{""area"":""main"",""slot"":3}", 3)]
        [TestCase(@"{""area"":""hotbar"",""slot"":2}", 38)]
        public void TryParseClickableSlotRefAcceptsClickableSlots(string json, int expectedSlot)
        {
            var ok = InventoryAreaMapper.TryParseClickableSlotRef(JToken.Parse(json), out var slot);
            Assert.IsTrue(ok);
            Assert.AreEqual(expectedSlot, slot);
        }

        // grab は collect 入力として不正なので拒否する
        // grab is invalid as a collect input and is rejected
        [TestCase(@"{""area"":""grab""}")]
        [TestCase(@"{""area"":""grab"",""slot"":0}")]
        [TestCase(@"{""area"":""sub"",""slot"":0}")]
        [TestCase(@"{""slot"":3}")]
        [TestCase(@"null")]
        public void TryParseClickableSlotRefRejectsNonClickable(string json)
        {
            var ok = InventoryAreaMapper.TryParseClickableSlotRef(JToken.Parse(json), out _);
            Assert.IsFalse(ok);
        }
    }
}
