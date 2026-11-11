using Client.Ui.State.Modal;
using Client.Ui.State.Modal.Logic;
using NUnit.Framework;

namespace Client.Tests.Ui.State.Modal
{
    /// <summary>
    /// 確定できる文の規則と空白の除去を、モーダルの可否・確定の文・BP 名の書き戻しが共有する（L0）
    /// The confirmable-text rule and trimming shared by the modal's eligibility, the confirmed text and the blueprint-name write-back (L0)
    /// </summary>
    public class ModalConfirmableTextTest
    {
        [Test]
        public void 空白だけとnullは確定できず前後に空白のある名前は除いた形で確定できる()
        {
            Assert.IsFalse(ModalLogic.HasConfirmableText(null));
            Assert.IsFalse(ModalLogic.HasConfirmableText(""));
            Assert.IsFalse(ModalLogic.HasConfirmableText(" \t　"));
            Assert.IsTrue(ModalLogic.HasConfirmableText("  参照拠点  "));
        }

        [Test]
        public void 入力付きモーダルは可否と除いた文を1回で導き入力なしは常に確定できる()
        {
            var input = new ModalRequest { Id = "1", RequiresInput = true };
            var plain = new ModalRequest { Id = "2", RequiresInput = false };
            Assert.IsFalse(ModalLogic.TryGetConfirmableText(input, "   ", out _));
            Assert.IsFalse(ModalLogic.TryGetConfirmableText(input, null, out _));
            Assert.IsTrue(ModalLogic.TryGetConfirmableText(input, "  参照拠点  ", out var trimmed));
            Assert.AreEqual("参照拠点", trimmed);
            Assert.IsTrue(ModalLogic.TryGetConfirmableText(plain, "", out var none));
            Assert.IsNull(none);
        }
    }
}
