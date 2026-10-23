using System.Collections;
using System.Runtime.Serialization;
using Client.Game.InGame.UI.UIState.State;
using Client.WebUiHost.Game.Actions;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.WebUi.BlockDetail
{
    public class RocketLaunchPadCommandActionTest
    {
        // 外部payloadの不正値を既存コードと警告で拒否する
        // Reject invalid external payloads with the established error code and a warning
        [UnityTest]
        public IEnumerator InvalidCommandsAreRejectedBeforeReadingGameState() => UniTask.ToCoroutine(async () =>
        {
            var handler = new RocketLaunchPadCommandActionHandler(null);
            var inputs = new[] { null, "{}", "{\"command\":null}", "{\"command\":3}", "{\"command\":{}}", "{\"command\":\"Build\"}", "{\"command\":\"other\"}" };
            foreach (var json in inputs)
            {
                LogAssert.Expect(LogType.Warning, "[RocketLaunchPadCommandAction] invalid_payload: command must be build or launch");
                var result = await handler.ExecuteAsync(json == null ? null : JObject.Parse(json));
                Assert.IsFalse(result.Ok);
                Assert.AreEqual("invalid_payload", result.Error);
            }
        });

        // 正しい合図も開いているブロックが無ければ送信しない
        // Even valid commands must not send when there is no open block
        [UnityTest]
        public IEnumerator ValidCommandsRequireAnOpenBlock() => UniTask.ToCoroutine(async () =>
        {
            // ネットワーク購読を起動せず、未開状態だけを構成する
            // Construct only the closed state without starting a network subscription
            var state = (SubInventoryState)FormatterServices.GetUninitializedObject(typeof(SubInventoryState));
            var handler = new RocketLaunchPadCommandActionHandler(state);
            foreach (var command in new[] { "build", "launch" })
            {
                LogAssert.Expect(LogType.Warning, "[RocketLaunchPadCommandAction] block_not_open: an open RocketLaunchPad block is required");
                var result = await handler.ExecuteAsync(new JObject { ["command"] = command });
                Assert.IsFalse(result.Ok);
                Assert.AreEqual("block_not_open", result.Error);
            }
        });
    }
}
