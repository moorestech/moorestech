using Client.WebUiHost.Common;
using NUnit.Framework;

namespace Client.Tests.WebUi
{
    public class WebUiJsonTest
    {
        private class SampleDto
        {
            public int ItemId;
            public string DisplayName;
            public string NullField;
        }

        [Test]
        public void SerializeToCamelCaseAndSkipNull()
        {
            var json = WebUiJson.Serialize(new SampleDto { ItemId = 3, DisplayName = "iron" });
            Assert.AreEqual("{\"itemId\":3,\"displayName\":\"iron\"}", json);
        }

        [Test]
        public void DeserializeFromCamelCase()
        {
            var dto = WebUiJson.Deserialize<SampleDto>("{\"itemId\":7,\"displayName\":\"gear\"}");
            Assert.AreEqual(7, dto.ItemId);
            Assert.AreEqual("gear", dto.DisplayName);
        }

        [Test]
        public void DeserializeWsClientMessage()
        {
            var msg = WebUiJson.Deserialize<Client.WebUiHost.Boot.WsClientMessage>(
                "{\"op\":\"action\",\"type\":\"debug.echo\",\"requestId\":\"a1\",\"topics\":[\"t1\"],\"payload\":{\"x\":1}}");
            Assert.AreEqual("action", msg.Op);
            Assert.AreEqual("debug.echo", msg.Type);
            Assert.AreEqual("a1", msg.RequestId);
            Assert.AreEqual(1, msg.Topics.Count);
            Assert.AreEqual("t1", msg.Topics[0]);
            Assert.AreEqual(1, (int)msg.Payload["x"]);
        }
    }
}
