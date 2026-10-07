using Client.Game.InGame.Train.View.Object.Core;
using Client.Game.InGame.UI.UIState.State.SubInventory;
using Client.Tests.Common;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.Interact.Reach
{
    /// <summary>
    ///     Fで開いた対象が到達判定の対象として引けることを検証
    ///     Verifies the F-opened target resolves as the reach target
    /// </summary>
    public class SubInventoryReachTargetTest : InteractTargetSelectorTestFixture
    {
        [Test]
        public void ブロックを開くと開いたブロックの面が到達判定の対象になる()
        {
            var block = CreateOpenableBlockTarget(new Vector3(1f, 0f, 0f));

            var source = block.Actions[0].Execute().TransitContext.GetContext<ISubInventorySource>();

            Assert.IsTrue(source.TryGetReachTarget(out var reachTarget));
            Assert.AreSame(block, reachTarget);
        }

        [Test]
        public void 車両インベントリを開くと開いた車両の面が到達判定の対象になる()
        {
            var car = CreateTrainCarTarget(new Vector3(1f, 0f, 0f));

            // Actions[0] がF=車両インベントリ（TrainCarInteractable.Initializeの並び）
            // Actions[0] is F = car inventory, per the order in TrainCarInteractable.Initialize
            var source = car.Actions[0].Execute().TransitContext.GetContext<ISubInventorySource>();

            Assert.IsTrue(source.TryGetReachTarget(out var reachTarget));
            Assert.AreSame(car, reachTarget);
        }

        [Test]
        public void 車両の表示が作り直されると同じIDの新しい面を到達判定の対象にする()
        {
            var openedCar = CreateTrainCarTarget(new Vector3(1f, 0f, 0f));
            var openedEntity = openedCar.GetComponent<TrainCarEntityObject>();
            var source = openedCar.Actions[0].Execute().TransitContext.GetContext<ISubInventorySource>();

            // 再同期で旧viewが破棄され、同じIDで新しいviewが登録される
            // A resync destroys the old view and registers a new one under the same ID
            var rebuiltCar = CreateTrainCarTarget(new Vector3(1f, 0f, 0f));
            TrainCarViewRegistry.Register(openedEntity.TrainCarInstanceId, rebuiltCar.GetComponent<TrainCarEntityObject>());

            Assert.IsTrue(source.TryGetReachTarget(out var reachTarget));
            Assert.AreSame(rebuiltCar, reachTarget);
        }

        [Test]
        public void 車両の表示が登録から消えると到達判定の対象を引けない()
        {
            var car = CreateTrainCarTarget(new Vector3(1f, 0f, 0f));
            var entity = car.GetComponent<TrainCarEntityObject>();
            var source = car.Actions[0].Execute().TransitContext.GetContext<ISubInventorySource>();

            TrainCarViewRegistry.Unregister(entity.TrainCarInstanceId);

            Assert.IsFalse(source.TryGetReachTarget(out _));
        }
    }
}
