using System;
using System.Text.RegularExpressions;
using Client.Network.API;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.Network
{
    // サーバーイベントを復号する購読者は25箇所超あり、どれもcatchを持たない（ADR 0060 裁定B）。壊れたパケット1発で後続へ配信が止まらないことを配信側で固定する
    // Over 25 subscribers decode server events and none of them holds a catch (ADR 0060 adjudication B); this pins on the delivery side that one broken packet never stops the rest
    public class EventResponseDispatcherTest
    {
        private const string Tag = "test:event";

        [Test]
        public void 購読者が例外を投げても同じタグの後続購読者へ配信される()
        {
            var dispatcher = new EventResponseDispatcher();
            var received = false;

            dispatcher.Subscribe(Tag, _ => throw new InvalidOperationException("復号に失敗した想定"));
            dispatcher.Subscribe(Tag, _ => received = true);

            // 隔離が外れると1人目の例外がSubjectごと止め、2人目へ届かないまま無音で終わる
            // Without the isolation the first subscriber's exception tears down the Subject and the second is silently never reached
            LogAssert.Expect(LogType.Error, new Regex("イベントの購読者が例外を投げました"));
            dispatcher.Dispatch(Tag, new byte[] { 1 });

            Assert.IsTrue(received, "先に登録した購読者の例外で後続への配信が止まっている");
        }

        // 例外を握るだけで黙ると、購読者が落ち続けていることに誰も気づけない
        // Swallowing the exception in silence would leave a permanently failing subscriber unnoticed
        [Test]
        public void 例外を投げた購読者は次の配信でも呼ばれ続ける()
        {
            var dispatcher = new EventResponseDispatcher();
            var calls = 0;

            dispatcher.Subscribe(Tag, _ =>
            {
                calls++;
                throw new InvalidOperationException("復号に失敗した想定");
            });

            LogAssert.Expect(LogType.Error, new Regex("イベントの購読者が例外を投げました"));
            dispatcher.Dispatch(Tag, new byte[] { 1 });
            LogAssert.Expect(LogType.Error, new Regex("イベントの購読者が例外を投げました"));
            dispatcher.Dispatch(Tag, new byte[] { 2 });

            Assert.AreEqual(2, calls, "1度例外を投げた購読者が購読解除されている");
        }

        [Test]
        public void 購読解除した購読者へは配信されない()
        {
            var dispatcher = new EventResponseDispatcher();
            var calls = 0;

            var subscription = dispatcher.Subscribe(Tag, _ => calls++);
            dispatcher.Dispatch(Tag, new byte[] { 1 });
            subscription.Dispose();
            dispatcher.Dispatch(Tag, new byte[] { 2 });

            Assert.AreEqual(1, calls);
        }
    }
}
