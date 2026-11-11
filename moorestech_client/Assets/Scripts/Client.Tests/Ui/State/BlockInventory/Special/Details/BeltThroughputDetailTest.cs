using Client.Ui.State.BlockInventory.Logic.Belt;
using Client.Ui.State.BlockInventory.Special.Details;
using NUnit.Framework;

namespace Client.Tests.Ui.State.BlockInventory.Special
{
    /// <summary>
    /// ベルトの詳細が直近の秒ごとの搬出を 9 本の棒（足りない古い側は空）にする
    /// The belt detail turns recent per-second exits into nine bars, padding the older missing side with empties
    /// </summary>
    public class BeltThroughputDetailTest
    {
        [Test]
        public void 直近3秒しかなければ古い6本を空にして秒あたり最大で割る()
        {
            var detail = new BeltThroughputDetail(new[] { BeltThroughputWindow.Measured(180, 9, 9) }, new[] { 1, 0, 1 }, 1);
            CollectionAssert.AreEqual(new float?[] { null, null, null, null, null, null, 1f, 0f, 1f }, detail.RecentSecondBars());
        }
    }
}
