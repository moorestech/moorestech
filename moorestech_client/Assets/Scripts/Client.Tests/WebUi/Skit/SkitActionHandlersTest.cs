using Client.Tests.Ui.Screens.Skit;
using Client.WebUiHost.Game.Actions;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Client.Tests.WebUi.Skit
{
    /// <summary>
    /// skit.* の 5 本の action が payload を読んで ISkitCommands の対応する操作へ渡し、受理を wire へ返すことを確かめる（L0。S2-skit D-54）
    /// Verifies the five skit.* actions read their payload, hand it to the matching ISkitCommands operation and return acceptance on the wire (L0; S2-skit D-54)
    /// </summary>
    public class SkitActionHandlersTest
    {
        private const string SessionId = "session-1";
        private const int Revision = 3;

        [Test]
        public void 各actionはpayloadを対応する操作へ渡し受理を返す()
        {
            var commands = new RecordingSkitCommands();

            // 5 本を Web の payload の形で順に送る
            // Send all five in the Web payload shape, in order
            Execute(new SkitAdvanceActionHandler(commands), "skit.advance", BasePayload());
            Execute(new SkitSelectActionHandler(commands), "skit.select", With("choiceId", "route-b"));
            Execute(new SkitSetAutoActionHandler(commands), "skit.set_auto", With("enabled", true));
            Execute(new SkitSkipActionHandler(commands), "skit.skip", BasePayload());
            Execute(new SkitSetUiHiddenActionHandler(commands), "skit.set_ui_hidden", With("hidden", true));

            // 種別・session・版・選択肢・真偽値がそのまま届く
            // Kind, session, revision, choice and flag arrive unchanged
            Assert.AreEqual(5, commands.Issued.Count);
            Assert.AreEqual(new SkitCommandRecord(SkitCommandKind.Advance, SessionId, Revision, null, false), commands.Issued[0]);
            Assert.AreEqual(new SkitCommandRecord(SkitCommandKind.Select, SessionId, Revision, "route-b", false), commands.Issued[1]);
            Assert.AreEqual(new SkitCommandRecord(SkitCommandKind.SetAuto, SessionId, Revision, null, true), commands.Issued[2]);
            Assert.AreEqual(new SkitCommandRecord(SkitCommandKind.Skip, SessionId, Revision, null, false), commands.Issued[3]);
            Assert.AreEqual(new SkitCommandRecord(SkitCommandKind.SetUiHidden, SessionId, Revision, null, true), commands.Issued[4]);
        }

        [Test]
        public void 真偽値の無いpayloadはinvalid_payloadで操作を送らない()
        {
            var commands = new RecordingSkitCommands();

            var auto = new SkitSetAutoActionHandler(commands).ExecuteAsync(BasePayload()).GetAwaiter().GetResult();
            var hidden = new SkitSetUiHiddenActionHandler(commands).ExecuteAsync(BasePayload()).GetAwaiter().GetResult();

            Assert.AreEqual("invalid_payload", auto.Error);
            Assert.AreEqual("invalid_payload", hidden.Error);
            Assert.AreEqual(0, commands.Issued.Count);
        }

        private static void Execute(SkitActionHandlerBase handler, string expectedActionType, JObject payload)
        {
            Assert.AreEqual(expectedActionType, handler.ActionType);
            var result = handler.ExecuteAsync(payload).GetAwaiter().GetResult();
            Assert.IsTrue(result.Ok, $"{expectedActionType} が受理されない: {result.Error}");
        }

        private static JObject BasePayload()
        {
            return new JObject { ["sessionId"] = SessionId, ["sceneRevision"] = Revision };
        }

        private static JObject With(string key, JToken value)
        {
            var payload = BasePayload();
            payload[key] = value;
            return payload;
        }
    }
}
