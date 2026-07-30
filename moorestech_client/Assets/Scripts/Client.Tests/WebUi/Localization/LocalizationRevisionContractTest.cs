using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using Client.Localization;
using Client.WebUiHost.Boot;
using Client.WebUiHost.Game;
using Client.WebUiHost.Game.Topics;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Client.Tests.WebUi.Localization
{
    public class LocalizationRevisionContractTest
    {
        [SetUp]
        public void SetUp()
        {
            Localize.Initialize();
        }

        [Test]
        public async Task TopicSnapshotCarriesCurrentDictionaryRevision()
        {
            var expectedRevision = Localize.GetDictionaryRevision();
            using var topic = new LocalizationTopic(new WebSocketHub());

            // wire JSONで辞書世代を検証
            // Verify dictionary generation in wire JSON
            var wire = JObject.Parse(await topic.GetSnapshotJsonAsync());

            Assert.AreEqual(expectedRevision, wire.Value<long>("revision"));
        }

        [Test]
        public async Task DictionaryEndpointRejectsAStaleExpectedRevision()
        {
            var staleRevision = Localize.GetDictionaryRevision();
            Localize.Initialize();
            var context = new DefaultHttpContext();
            context.Request.QueryString = QueryString.Create(
                "revision",
                staleRevision.ToString(CultureInfo.InvariantCulture));
            context.Response.Body = new MemoryStream();

            // 旧世代で現辞書の成功応答を拒否
            // Reject successful current responses for an old generation
            await LocalizationDictionaryEndpoint.HandleAsync(context, "/api/i18n/english");

            Assert.AreEqual(StatusCodes.Status409Conflict, context.Response.StatusCode);
        }
    }
}
