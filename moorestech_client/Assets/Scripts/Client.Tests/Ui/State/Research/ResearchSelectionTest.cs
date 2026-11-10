using System;
using Client.Ui.State.Research.Tree;
using NUnit.Framework;

namespace Client.Tests.Ui.State.Research
{
    /// <summary>
    /// 選択の切り替え（同じノードで閉じる・別のノードへ移る）
    /// Selection toggling (same node closes, another node moves)
    /// </summary>
    public class ResearchSelectionTest
    {
        private static readonly Guid A = Guid.Parse("00000000-0000-0000-0000-00000000000a");
        private static readonly Guid B = Guid.Parse("00000000-0000-0000-0000-00000000000b");

        [Test]
        public void 同じノードの再タップで閉じ別のノードは移る()
        {
            var selected = ResearchSelection.None.Toggled(A);
            Assert.IsTrue(selected.Is(A));
            Assert.IsTrue(selected.Toggled(B).Is(B));
            Assert.IsFalse(selected.Toggled(A).Is(A));
        }
    }
}
