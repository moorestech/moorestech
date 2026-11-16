using System;
using System.Diagnostics;
using Client.Input;
using Client.WebUiHost.Boot;
using NUnit.Framework;

namespace Client.Tests.WebUiHost.Transport
{
    // 受信の度に受信時刻が残ることを固定する。記録行が消えると無応答の判定が二度と立たず fail-open が無音で死ぬ
    // Pin that every receipt leaves its time; without the record line the silence verdict never fires and fail-open dies silently
    public sealed class WebSocketMessageReceiptRecordTest
    {
        [SetUp]
        public void SetUp()
        {
            WebUiInputExclusivity.ResetWebSilenceForTest();
        }

        [TearDown]
        public void TearDown()
        {
            WebUiInputExclusivity.ResetWebSilenceForTest();
        }

        [Test]
        public void 解釈できない受信でも受信時刻を記録する()
        {
            Assert.IsNull(WebUiInputExclusivity.SecondsSinceLastWebMessage(Stopwatch.GetTimestamp()), "前提: 未受信から始める");
            var dispatcher = new WebSocketMessageDispatcher(new WebSocketHub());

            dispatcher.DispatchAsync(new WebSocketConnection(null, Guid.NewGuid()), "not json").GetAwaiter().GetResult();

            Assert.IsNotNull(WebUiInputExclusivity.SecondsSinceLastWebMessage(Stopwatch.GetTimestamp()), "受信したのに受信時刻が残っていない");
        }
    }
}
