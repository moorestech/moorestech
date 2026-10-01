using Client.Game.InGame.UI.UIState.State.SubInventory;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.Interact.Reach
{
    /// <summary>
    ///     Fで開くアクションが、開いた対象そのものを到達判定の対象として運ぶことを検証
    ///     Verifies the F-open actions carry the opened target itself as the reach target
    /// </summary>
    public class SubInventoryReachTargetTest : InteractTargetSelectorTestFixture
    {
        [Test]
        public void ブロックを開くと開いたブロックの面が到達判定の対象になる()
        {
            var block = CreateOpenableBlockTarget(new Vector3(1f, 0f, 0f));

            var source = block.Actions[0].Execute().TransitContext.GetContext<ISubInventorySource>();

            Assert.AreSame(block, source.ReachTarget);
        }

        [Test]
        public void 車両インベントリを開くと開いた車両の面が到達判定の対象になる()
        {
            var car = CreateTrainCarTarget(new Vector3(1f, 0f, 0f));

            // Actions[0] がF=車両インベントリ（TrainCarInteractable.Initializeの並び）
            // Actions[0] is F = car inventory, per the order in TrainCarInteractable.Initialize
            var source = car.Actions[0].Execute().TransitContext.GetContext<ISubInventorySource>();

            Assert.AreSame(car, source.ReachTarget);
        }
    }
}
