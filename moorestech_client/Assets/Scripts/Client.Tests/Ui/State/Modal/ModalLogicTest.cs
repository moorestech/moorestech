using Client.Ui.State.Modal;
using Client.Ui.State.Modal.Logic;
using NUnit.Framework;

namespace Client.Tests.Ui.State.Modal
{
    public class ModalLogicTest
    {
        /// <web-test file="src/features/modal/modalLogic.test.ts">id と confirm 結果をそのまま返す</web-test>
        [Test]
        public void 確定の応答は要求IDと確定をそのまま運ぶ()
        {
            var response = ModalResponse.Confirm(Request("m1", false), "");
            Assert.AreEqual("m1", response.RequestId);
            Assert.AreEqual(ModalResult.Confirm, response.Result);
            Assert.IsNull(response.Text);
        }

        /// <web-test file="src/features/modal/modalLogic.test.ts">cancel 結果も同様に返す</web-test>
        [Test]
        public void 取消の応答も要求IDと取消をそのまま運ぶ()
        {
            var response = ModalResponse.Cancel(Request("x", false));
            Assert.AreEqual("x", response.RequestId);
            Assert.AreEqual(ModalResult.Cancel, response.Result);
            Assert.IsNull(response.Text);
        }

        /// <web-test file="src/features/modal/modalLogic.test.ts">text 付き confirm を組み立てる</web-test>
        [Test]
        public void 入力付きモーダルの確定は入力文を運ぶ()
        {
            var response = ModalResponse.Confirm(Request("m2", true), "家");
            Assert.AreEqual("m2", response.RequestId);
            Assert.AreEqual(ModalResult.Confirm, response.Result);
            Assert.AreEqual("家", response.Text);
        }

        /// <web-test file="src/features/modal/modalLogic.test.ts">text 省略時は text キーを含めない</web-test>
        [Test]
        public void 入力付きモーダルでも取消は入力文を運ばない()
        {
            Assert.IsNull(ModalResponse.Cancel(Request("m1", true)).Text);
        }

        /// <web-test file="src/features/modal/modalLogic.test.ts">非inputモーダルは常に確定可</web-test>
        [Test]
        public void 入力なしのモーダルは常に確定できる()
        {
            Assert.IsTrue(ModalLogic.TryGetConfirmableText(Request("m1", false), "", out _));
        }

        /// <web-test file="src/features/modal/modalLogic.test.ts">inputモーダルは空白のみを確定不可にする</web-test>
        [Test]
        public void 入力付きモーダルは空白だけの文では確定できない()
        {
            Assert.IsFalse(ModalLogic.TryGetConfirmableText(Request("m1", true), "   ", out _));
            Assert.IsTrue(ModalLogic.TryGetConfirmableText(Request("m1", true), " 家 ", out _));
        }

        /// <web-test file="src/features/modal/modalLogic.test.ts">confirm variant は blue</web-test>
        [Test]
        public void 確認の種別は青のボタン()
        {
            Assert.AreEqual(ModalButtonTone.Blue, ModalLogic.ConfirmButtonTone(ModalVariant.Confirm));
        }

        /// <web-test file="src/features/modal/modalLogic.test.ts">error variant は red</web-test>
        [Test]
        public void エラーの種別は赤のボタン()
        {
            Assert.AreEqual(ModalButtonTone.Red, ModalLogic.ConfirmButtonTone(ModalVariant.Error));
        }

        private static ModalRequest Request(string id, bool requiresInput)
        {
            return new ModalRequest { Id = id, Title = "t", Message = "m", ButtonText = "ok", Variant = ModalVariant.Confirm, RequiresInput = requiresInput };
        }
    }
}
