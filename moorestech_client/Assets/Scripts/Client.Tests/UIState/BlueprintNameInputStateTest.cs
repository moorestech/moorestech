using Client.Game.InGame.UI.Blueprint;
using NUnit.Framework;
using UniRx;

namespace Client.Tests.UIState
{
    public class BlueprintNameInputStateTest
    {
        [Test]
        public void 空白だけの名前は確定されない()
        {
            var state = new BlueprintNameInputState();
            string confirmed = null;
            using var subscription = state.OnConfirm.Subscribe(name => confirmed = name);
            state.Open();

            state.Confirm("   ");

            Assert.IsNull(confirmed);
            Assert.IsTrue(state.IsOpen);
        }

        [Test]
        public void 確定でTrimされた名前が流れ閉じる()
        {
            var state = new BlueprintNameInputState();
            string confirmed = null;
            var openLog = new System.Collections.Generic.List<bool>();
            using var s1 = state.OnConfirm.Subscribe(name => confirmed = name);
            using var s2 = state.OnOpenChanged.Subscribe(openLog.Add);
            state.Open();

            state.Confirm("  base  ");

            Assert.AreEqual("base", confirmed);
            Assert.IsFalse(state.IsOpen);
            CollectionAssert.AreEqual(new[] { true, false }, openLog);
        }

        [Test]
        public void 閉じているときの確定とキャンセルは無視される()
        {
            var state = new BlueprintNameInputState();
            var count = 0;
            using var s1 = state.OnConfirm.Subscribe(_ => count++);
            using var s2 = state.OnCancel.Subscribe(_ => count++);

            state.Confirm("x");
            state.Cancel();

            Assert.AreEqual(0, count);
        }
    }
}
