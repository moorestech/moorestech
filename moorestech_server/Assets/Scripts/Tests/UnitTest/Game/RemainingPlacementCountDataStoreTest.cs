using System.Linq;
using Game.Construction;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UniRx;

namespace Tests.UnitTest.Game
{
    public class RemainingPlacementCountDataStoreTest
    {
        private const int PlayerId = 1;

        [Test]
        public void 財布キーはファミリー所属なら直線代表で非所属なら自分()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            Assert.AreEqual(ForUnitTestModBlockId.GearBeltConveyor, ConstructionWalletUtil.ResolveWalletBlockId(ForUnitTestModBlockId.TestGearBeltConveyorUp));
            Assert.AreEqual(ForUnitTestModBlockId.GearBeltConveyor, ConstructionWalletUtil.ResolveWalletBlockId(ForUnitTestModBlockId.GearBeltConveyor));
            Assert.AreEqual(ForUnitTestModBlockId.MachineId, ConstructionWalletUtil.ResolveWalletBlockId(ForUnitTestModBlockId.MachineId));
        }

        [Test]
        public void 補充と消費と返却で残り設置数が遷移し変更が通知される()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var store = serviceProvider.GetService<RemainingPlacementCountDataStore>();
            var wallet = ForUnitTestModBlockId.GearBeltConveyor;
            var changes = 0;
            store.OnRemainingCountChanged.Subscribe(_ => changes++);

            // 残り0では消費できない
            // Nothing to consume while the wallet is empty
            Assert.IsFalse(store.TryConsumeOne(PlayerId, wallet));
            Assert.AreEqual(0, store.GetRemainingCount(PlayerId, wallet));

            store.Refill(PlayerId, wallet, 3);
            Assert.AreEqual(3, store.GetRemainingCount(PlayerId, wallet));
            Assert.IsTrue(store.TryConsumeOne(PlayerId, wallet));
            Assert.AreEqual(2, store.GetRemainingCount(PlayerId, wallet));

            // 返却は+1、Nに達したら0へ戻る（凝縮返却。設置と撤去が完全な逆操作になる閾値）
            // Return adds one; reaching N resets to zero (condensed refund; the threshold that makes removal the exact inverse of placement)
            Assert.IsTrue(ConstructionWalletUtil.WouldCondense(store.GetRemainingCount(PlayerId, wallet), 3));
            store.ReturnOne(PlayerId, wallet, 3);
            Assert.AreEqual(0, store.GetRemainingCount(PlayerId, wallet));

            // N未達なら加算のみ
            // Below N it simply accumulates
            Assert.IsFalse(ConstructionWalletUtil.WouldCondense(store.GetRemainingCount(PlayerId, wallet), 3));
            store.ReturnOne(PlayerId, wallet, 3);
            Assert.AreEqual(1, store.GetRemainingCount(PlayerId, wallet));
            Assert.AreEqual(4, changes);
        }

        [Test]
        public void 読み取りだけではセーブに現れず0件はセーブしない()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var store = serviceProvider.GetService<RemainingPlacementCountDataStore>();
            var wallet = ForUnitTestModBlockId.GearBeltConveyor;

            store.GetRemainingCount(PlayerId, wallet);
            Assert.IsEmpty(store.GetSaveJsonObject());

            store.Refill(PlayerId, wallet, 3);
            store.TryConsumeOne(PlayerId, wallet); store.TryConsumeOne(PlayerId, wallet); store.TryConsumeOne(PlayerId, wallet);
            Assert.IsEmpty(store.GetSaveJsonObject().SelectMany(p => p.Entries));
        }
    }
}
