using System.Threading;
using Client.Ui.State.Modal;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UniRx;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.Ui.State.Modal
{
    /// <summary>
    /// モーダルは開いた側の寿命で閉じ、持ち手の Dispose は自分の要求だけを取り消す（ADR 0105 §4）
    /// A modal closes with its opener's lifetime, and disposing the handle cancels only its own request (ADR 0105 §4)
    /// </summary>
    public class ModalRequestOwnerLifetimeTest
    {
        [Test]
        public void 開いた側の寿命が尽きると自分の要求が取り消しで閉じる()
        {
            var service = new ModalRequestService();
            using var owner = new CancellationTokenSource();
            var request = service.RequestModal("t", "m", "ok", ModalVariant.Confirm, owner.Token);
            Assert.AreEqual(UniTaskStatus.Pending, request.Response.Status);

            owner.Cancel();

            Assert.IsNull(service.Pending);
            Assert.AreEqual(UniTaskStatus.Succeeded, request.Response.Status);
            var (result, text) = request.Response.GetAwaiter().GetResult();
            Assert.AreEqual(ModalResult.Cancel, result);
            Assert.IsNull(text);
        }

        [Test]
        public void 置き換えられた後に前の開き手の寿命が尽きても新しい要求は閉じない()
        {
            var service = new ModalRequestService();
            using var firstOwner = new CancellationTokenSource();
            using var secondOwner = new CancellationTokenSource();
            service.RequestModal("first", "m", "ok", ModalVariant.Confirm, firstOwner.Token);
            var second = service.RequestInputModal("second", "m", "ok", secondOwner.Token);

            firstOwner.Cancel();

            Assert.AreEqual(second.RequestId, service.Pending.Id);
            Assert.AreEqual(UniTaskStatus.Pending, second.Response.Status);
        }

        [Test]
        public void 持ち手のDisposeは自分の要求だけを取り消す()
        {
            var service = new ModalRequestService();
            var first = service.RequestModal("first", "m", "ok", ModalVariant.Confirm, CancellationToken.None);
            var second = service.RequestInputModal("second", "m", "ok", CancellationToken.None);

            // 置き換えられた前の持ち手を Dispose しても、新しい要求は残る
            // Disposing the replaced handle leaves the newer request
            first.Dispose();
            Assert.AreEqual(second.RequestId, service.Pending.Id);

            second.Dispose();
            Assert.IsNull(service.Pending);
            Assert.AreEqual(ModalResult.Cancel, second.Response.GetAwaiter().GetResult().result);
        }

        [Test]
        public void 応答した後は寿命が尽きてもDisposeしても何も変えない()
        {
            var service = new ModalRequestService();
            using var owner = new CancellationTokenSource();
            var request = service.RequestModal("t", "m", "ok", ModalVariant.Confirm, owner.Token);
            Assert.IsTrue(service.Respond(request.RequestId, ModalResult.Confirm, null));
            var changes = 0;
            using var subscription = service.OnPendingChanged.Subscribe(_ => changes++);

            owner.Cancel();
            request.Dispose();

            Assert.AreEqual(0, changes);
            Assert.AreEqual(ModalResult.Confirm, request.Response.GetAwaiter().GetResult().result);
        }

        [Test]
        public void 応答済みの開き手の寿命が後で尽きても次の開き手の要求は閉じない()
        {
            var service = new ModalRequestService();
            using var firstOwner = new CancellationTokenSource();
            var first = service.RequestModal("first", "m", "ok", ModalVariant.Confirm, firstOwner.Token);
            service.Respond(first.RequestId, ModalResult.Confirm, null);
            var second = service.RequestModal("second", "m", "ok", ModalVariant.Confirm, CancellationToken.None);

            firstOwner.Cancel();

            Assert.AreEqual(second.RequestId, service.Pending.Id);
        }

        [Test]
        public void 寿命が尽きた開き手の要求は出さず今の保留も置き換えない()
        {
            var service = new ModalRequestService();
            var shown = service.RequestModal("shown", "m", "ok", ModalVariant.Confirm, CancellationToken.None);
            using var ended = new CancellationTokenSource();
            ended.Cancel();

            LogAssert.Expect(LogType.Warning, "[ModalRequestService] request 2 ignored: the owner's lifetime has already ended");
            var ignored = service.RequestInputModal("ignored", "m", "ok", ended.Token);

            Assert.AreEqual(shown.RequestId, service.Pending.Id);
            Assert.AreEqual(UniTaskStatus.Pending, shown.Response.Status);
            Assert.AreEqual(ModalResult.Cancel, ignored.Response.GetAwaiter().GetResult().result);
            Assert.IsFalse(service.IsLatestRequest(ignored.RequestId));

            // 出なかった要求の持ち手を Dispose しても、出ている要求は閉じない
            // Disposing the handle of the unshown request does not close the shown one
            ignored.Dispose();
            Assert.AreEqual(shown.RequestId, service.Pending.Id);
        }
    }
}
