using Client.Game.InGame.UI.Inventory.Main;
using Client.WebUiHost.Game.Actions;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Client.Tests.WebUi
{
    public class InventoryAreaMapperTest
    {
        private const int MainSlotCount = 54;

        [TestCase("main", 0, LocalMoveInventoryType.MainOrSub, 0)]
        [TestCase("main", 44, LocalMoveInventoryType.MainOrSub, 44)]
        [TestCase("hotbar", 0, LocalMoveInventoryType.MainOrSub, 45)]
        [TestCase("hotbar", 8, LocalMoveInventoryType.MainOrSub, 53)]
        [TestCase("grab", 0, LocalMoveInventoryType.Grab, 0)]
        public void ValidAreaSlotMapsToLocalSlot(string area, int slot, LocalMoveInventoryType expectedType, int expectedSlot)
        {
            var ok = InventoryAreaMapper.TryGetLocalSlot(area, slot, MainSlotCount, out var type, out var localSlot);
            Assert.IsTrue(ok);
            Assert.AreEqual(expectedType, type);
            Assert.AreEqual(expectedSlot, localSlot);
        }

        [TestCase("main", -1)]
        [TestCase("main", 45)]
        [TestCase("hotbar", -1)]
        [TestCase("hotbar", 9)]
        [TestCase("sub", 0)]
        [TestCase(null, 0)]
        public void InvalidAreaSlotReturnsFalse(string area, int slot)
        {
            var ok = InventoryAreaMapper.TryGetLocalSlot(area, slot, MainSlotCount, out _, out _);
            Assert.IsFalse(ok);
        }

        [TestCase(@"{""area"":""main"",""slot"":44}", LocalMoveInventoryType.MainOrSub, 44)]
        [TestCase(@"{""area"":""hotbar"",""slot"":8}", LocalMoveInventoryType.MainOrSub, 53)]
        [TestCase(@"{""area"":""grab""}", LocalMoveInventoryType.Grab, 0)]
        public void ValidSlotRefTokenParses(string json, LocalMoveInventoryType expectedType, int expectedSlot)
        {
            var ok = InventoryAreaMapper.TryParseSlotRef(JToken.Parse(json), MainSlotCount, out var type, out var localSlot);
            Assert.IsTrue(ok);
            Assert.AreEqual(expectedType, type);
            Assert.AreEqual(expectedSlot, localSlot);
        }

        [TestCase(@"""main""")]
        [TestCase(@"{""slot"":3}")]
        [TestCase(@"{""area"":1,""slot"":3}")]
        [TestCase(@"{""area"":{},""slot"":3}")]
        [TestCase(@"{""area"":""main"",""slot"":""abc""}")]
        [TestCase(@"{""area"":""main"",""slot"":1.5}")]
        [TestCase(@"{""area"":""main"",""slot"":45}")]
        public void InvalidSlotRefTokenReturnsFalse(string json)
        {
            var ok = InventoryAreaMapper.TryParseSlotRef(JToken.Parse(json), MainSlotCount, out _, out _);
            Assert.IsFalse(ok);
        }

        [Test]
        public void TryParseSlotRefRejectsOutOfRangeIntegers()
        {
            var overLong = JObject.Parse("{\"area\":\"main\",\"slot\":99999999999}");
            Assert.IsFalse(InventoryAreaMapper.TryParseSlotRef(overLong, MainSlotCount, out _, out _));

            var bigInteger = JObject.Parse("{\"area\":\"main\",\"slot\":99999999999999999999999999999}");
            Assert.IsFalse(InventoryAreaMapper.TryParseSlotRef(bigInteger, MainSlotCount, out _, out _));
        }

        [Test]
        public void TryParseSlotRefRejectsNullToken()
        {
            Assert.IsFalse(InventoryAreaMapper.TryParseSlotRef(null, MainSlotCount, out _, out _));
        }

        [Test]
        public void TryParseSlotRefMissingSlotKeyOnlyAllowedForGrab()
        {
            var mainNoSlot = JObject.Parse("{\"area\":\"main\"}");
            Assert.IsFalse(InventoryAreaMapper.TryParseSlotRef(mainNoSlot, MainSlotCount, out _, out _));

            var grabNoSlot = JObject.Parse("{\"area\":\"grab\"}");
            Assert.IsTrue(InventoryAreaMapper.TryParseSlotRef(grabNoSlot, MainSlotCount, out var grabType, out var grabSlot));
            Assert.AreEqual(LocalMoveInventoryType.Grab, grabType);
            Assert.AreEqual(0, grabSlot);
        }

        [Test]
        public void TryParseSlotRefRejectsNullAreaAndNullSlot()
        {
            var nullArea = JObject.Parse("{\"area\":null,\"slot\":3}");
            Assert.IsFalse(InventoryAreaMapper.TryParseSlotRef(nullArea, MainSlotCount, out _, out _));

            var nullSlot = JObject.Parse("{\"area\":\"main\",\"slot\":null}");
            Assert.IsFalse(InventoryAreaMapper.TryParseSlotRef(nullSlot, MainSlotCount, out _, out _));
        }

        [Test]
        public void TryParseSlotRefIgnoresSlotValueForGrab()
        {
            var grabBigSlot = JObject.Parse("{\"area\":\"grab\",\"slot\":999}");
            Assert.IsTrue(InventoryAreaMapper.TryParseSlotRef(grabBigSlot, MainSlotCount, out var type, out var slot));
            Assert.AreEqual(LocalMoveInventoryType.Grab, type);
            Assert.AreEqual(0, slot);
        }
    }
}
