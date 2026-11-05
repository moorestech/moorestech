using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using Client.Input;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.Ui.Input
{
    /// <summary>
    /// Web 無応答の閾値が webSocketClient.ts の pong 途絶で WS を閉じる値と一致することを確かめる（L0。片側だけ変えると Web 入力中にワールドのキーが通る）
    /// Verifies the web-silence threshold matches the pong-timeout close in webSocketClient.ts (L0; changing one side alone lets world keys through while typing in the web)
    /// </summary>
    public class WebSilenceThresholdWebParityTest
    {
        private const string WebSocketClientPath = "moorestech_web/webui/src/bridge/transport/webSocketClient.ts";

        [Test]
        public void 閾値はWebのpong途絶の値と同じ()
        {
            var root = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
            var text = File.ReadAllText(Path.Combine(root, WebSocketClientPath));

            // 心拍で WS を閉じる判定 `Date.now() - this.lastPongAt >= 15000` の数値を拾う
            // Pick the number from the heartbeat close check `Date.now() - this.lastPongAt >= 15000`
            var match = Regex.Match(text, @"lastPongAt\s*>=\s*(\d+)");
            Assert.IsTrue(match.Success, $"pong timeout check not found in {WebSocketClientPath}");
            var webMilliseconds = double.Parse(match.Groups[1].Value);

            // 閾値は入力排他の内側に閉じた private 定数なので反射で読む
            // The threshold is a private constant kept inside input exclusivity, so read it by reflection
            var field = typeof(UiInputExclusivity).GetField("WebSilenceThresholdSeconds", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(field, "UiInputExclusivity.WebSilenceThresholdSeconds not found");
            Assert.AreEqual(webMilliseconds / 1000.0, (double)field.GetRawConstantValue());
        }
    }
}
