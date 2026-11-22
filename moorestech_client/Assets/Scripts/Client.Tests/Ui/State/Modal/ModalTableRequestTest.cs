using System.Collections.Generic;
using Client.Ui.State.Modal;
using Cysharp.Threading.Tasks;
using NUnit.Framework;

namespace Client.Tests.Ui.State.Modal
{
    /// <summary>
    /// 項目名と値の行を載せたモーダルの要求
    /// Modal requests carrying label-and-values rows
    /// </summary>
    public class ModalTableRequestTest
    {
        [Test]
        public void 表のモーダルは行を保留の要求に載せ応答で結果を返す()
        {
            var service = new ModalRequestService();
            var rows = new List<ModalTableRow> { new("部屋クラスを B に", "失敗 −13.1%") };
            var (requestId, response) = service.RequestTableModal("失敗率の改善方法", "今: 失敗 50.5%", rows, "閉じる");

            // 入力欄の無い確認の形で、行は要求のまま載る
            // A confirm-style request without input; rows ride along unchanged
            Assert.AreEqual(requestId, service.Pending.Id);
            Assert.IsFalse(service.Pending.RequiresInput);
            Assert.AreEqual(ModalVariant.Confirm, service.Pending.Variant);
            Assert.AreEqual(1, service.Pending.Rows.Count);
            Assert.AreEqual("部屋クラスを B に", service.Pending.Rows[0].Label);
            Assert.AreEqual("失敗 −13.1%", service.Pending.Rows[0].Values);

            // 応答で結果だけが返る
            // The reply resolves the result only
            Assert.IsTrue(service.Respond(requestId, ModalResult.Cancel, null));
            Assert.AreEqual(UniTaskStatus.Succeeded, response.Status);
            Assert.AreEqual(ModalResult.Cancel, response.GetAwaiter().GetResult());
            Assert.IsNull(service.Pending);
        }

        [Test]
        public void 確認と入力のモーダルは行を空で載せる()
        {
            var service = new ModalRequestService();
            service.RequestModal("t", "m", "ok", ModalVariant.Confirm).Forget();
            Assert.AreEqual(0, service.Pending.Rows.Count);

            service.RequestInputModal("t", "m", "ok");
            Assert.AreEqual(0, service.Pending.Rows.Count);
        }
    }
}
