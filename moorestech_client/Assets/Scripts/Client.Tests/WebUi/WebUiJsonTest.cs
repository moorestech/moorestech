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
    }
}
