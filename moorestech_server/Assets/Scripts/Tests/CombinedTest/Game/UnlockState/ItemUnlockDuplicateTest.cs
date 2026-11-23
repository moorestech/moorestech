using Game.UnlockState;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UniRx;
using static Tests.Module.TestMod.ForUnitTestItemId;

namespace Tests.CombinedTest.Game.UnlockState
{
    /// <summary>
    /// 2つの研究が同じアイテムを解放しても、解放の通知は最初の1回だけ流れる
    /// When two researches unlock the same item, the unlock notification fires only the first time
    /// </summary>
    public class ItemUnlockDuplicateTest
    {
        [Test]
        public void 解放済みのアイテムを再び解放しても通知は1回()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var unlockState = serviceProvider.GetService<IGameUnlockStateDataController>();
            var fired = 0;
            unlockState.OnUnlockItem.Subscribe(_ => fired++);

            unlockState.UnlockItem(ItemId4);
            unlockState.UnlockItem(ItemId4);

            Assert.IsTrue(unlockState.ItemUnlockStateInfos[ItemId4].IsUnlocked);
            Assert.AreEqual(1, fired);
        }
    }
}
