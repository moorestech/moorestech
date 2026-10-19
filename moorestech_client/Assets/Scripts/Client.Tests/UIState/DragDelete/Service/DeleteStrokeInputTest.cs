using System;
using Client.Game.InGame.UI.UIState.State.DragDelete.Selection;
using Client.Input;
using NUnit.Framework;
using UnityEngine.InputSystem;

namespace Client.Tests.UIState.DragDelete.Service
{
    public class DeleteStrokeInputTest : DeleteInputFixture
    {
        [TestCase(Key.LeftCtrl)]
        [TestCase(Key.LeftCommand)]
        public void 修飾キーは開始時に固定し途中で離しても除外を続ける(Key modifier)
        {
            var first = CreateTarget();
            var second = CreateTarget();
            Selection.ApplyBoxTargets(new[] { first, second }, DeleteSelectionIntent.Add);
            AimAt(first);
            Frame(true, new[] { modifier });
            Assert.IsTrue(HybridInput.IsControlOrCommandHeld());
            Service.Update(0, false);
            Assert.IsFalse(Selection.IsSelected(first));
            AimAt(second);
            Frame(true, Array.Empty<Key>());
            Assert.IsFalse(HybridInput.IsControlOrCommandHeld());
            Service.Update(0, false);
            Assert.IsFalse(Selection.IsSelected(second));
            Assert.AreEqual(1, second.Target.ResetCount);
        }

        [Test]
        public void 解放フレームの復元失敗でも次フレームに除外を続けない()
        {
            var first = CreateTarget();
            var broken = CreateTarget();
            var kept = CreateTarget();
            Selection.ApplyBoxTargets(new[] { first, broken, kept }, DeleteSelectionIntent.Add);
            AimAt(first);
            Frame(true, new[] { Key.LeftCtrl });
            Service.Update(0, false);
            AimAt(broken);
            broken.Target.ThrowOnReset = true;
            Frame(false, Array.Empty<Key>());
            Assert.Throws<InvalidOperationException>(() => Service.Update(0, false));
            Assert.IsFalse(Selection.AimFilter.IsCategoryRequired);
            AimAt(kept);
            Frame(false, Array.Empty<Key>());
            Service.Update(0, false);
            Assert.IsTrue(Selection.IsSelected(kept));
            Assert.AreEqual(0, kept.Target.ResetCount);
        }

        [TestCase(5, 1.25f)]
        [TestCase(20, 1.75f)]
        public void 実選択件数でF長押しが満了する(int count, float seconds)
        {
            // 選択はサービスの実なぞりから蓄積する
            // Accumulate selection through the service's real stroke input
            for (var index = 0; index < count; index++)
            {
                var target = CreateTarget();
                AimAt(target);
                Frame(true, Array.Empty<Key>());
                Service.Update(0, false);
            }
            Frame(false, Array.Empty<Key>());
            Service.Update(0, false);
            Assert.AreEqual(count, Selection.Count);
            Frame(false, new[] { Key.F });
            Service.Update(seconds - 0.125f, false);
            foreach (var target in Targets) Assert.AreEqual(0, target.Target.DeleteCount);
            Assert.IsTrue(Progress.IsShown);
            Frame(false, new[] { Key.F });
            Service.Update(0.125f, false);
            foreach (var target in Targets) Assert.AreEqual(1, target.Target.DeleteCount);
            Assert.IsFalse(Progress.IsShown);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void 実なぞり除外の通知がバーを戻し残件数で再待機する(bool removeAll)
        {
            var first = CreateTarget();
            var second = CreateTarget();
            Selection.ApplyBoxTargets(new[] { first, second }, DeleteSelectionIntent.Add);
            AimAt(first);
            Frame(false, new[] { Key.F });
            Service.Update(0.5f, false);
            Assert.IsTrue(Progress.IsShown);
            Frame(true, new[] { Key.F, Key.LeftCtrl });
            Service.Update(0, false);
            Assert.AreEqual(0, Progress.CurrentProgress);
            if (removeAll)
            {
                AimAt(second);
                Frame(true, new[] { Key.F });
                Service.Update(0, false);
                Assert.IsFalse(Progress.IsShown);
                Assert.AreEqual(0, Selection.Count);
            }
            else
            {
                Frame(true, new[] { Key.F });
                Service.Update(0.625f, false);
                Assert.AreEqual(0, second.Target.DeleteCount);
                Service.Update(0.125f, false);
                Assert.AreEqual(1, second.Target.DeleteCount);
            }
        }
    }
}
