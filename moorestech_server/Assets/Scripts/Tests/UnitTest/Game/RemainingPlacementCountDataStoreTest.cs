using System;
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
        public void 財布キーは坂ベルトだけ直線代表で他は自分()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            Assert.AreEqual(ForUnitTestModBlockId.GearBeltConveyor, ConstructionWalletUtil.ResolveWalletBlockId(ForUnitTestModBlockId.TestGearBeltConveyorUp));
            Assert.AreEqual(ForUnitTestModBlockId.GearBeltConveyor, ConstructionWalletUtil.ResolveWalletBlockId(ForUnitTestModBlockId.GearBeltConveyor));
            Assert.AreEqual(ForUnitTestModBlockId.MachineId, ConstructionWalletUtil.ResolveWalletBlockId(ForUnitTestModBlockId.MachineId));
        }

        [Test]
        public void 設置と返却で残り設置数が遷移し変更が通知される()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var store = serviceProvider.GetService<RemainingPlacementCountDataStore>();
            var wallet = ForUnitTestModBlockId.GearBeltConveyor;
            var changes = 0;
            store.OnRemainingCountChanged.Subscribe(_ => changes++);

            // 残り0を財布で賄う設置は財布の判断漏れなので落ちる
            // A wallet-covered placement on an empty wallet means the caller skipped the wallet's decision, so it throws
            Assert.Throws<InvalidOperationException>(() => store.ApplyPlacement(PlayerId, wallet, 3, ConstructionWalletUsage.CoveredByWallet));
            Assert.AreEqual(0, store.GetRemainingCount(PlayerId, wallet));

            // 素材を払った設置は1セット分を補充してから1消費するのでN-1へ、以降は財布が賄い1ずつ減る
            // A placement that paid materials refills one set and consumes one, landing on N-1, and later ones are wallet-covered and drop by one
            store.ApplyPlacement(PlayerId, wallet, 3, ConstructionWalletUsage.PaidAndRefilled);
            Assert.AreEqual(2, store.GetRemainingCount(PlayerId, wallet));
            store.ApplyPlacement(PlayerId, wallet, 3, ConstructionWalletUsage.CoveredByWallet);
            Assert.AreEqual(1, store.GetRemainingCount(PlayerId, wallet));

            // 返却は+1、Nに達したら0へ戻る（凝縮返却。設置と撤去が完全な逆操作になる閾値）
            // Return adds one; reaching N resets to zero (condensed refund; the threshold that makes removal the exact inverse of placement)
            store.ApplyReturn(PlayerId, wallet, false);
            Assert.AreEqual(2, store.GetRemainingCount(PlayerId, wallet));
            Assert.IsTrue(ConstructionWalletUtil.WouldCondense(store.GetRemainingCount(PlayerId, wallet), 3));
            store.ApplyReturn(PlayerId, wallet, true);
            Assert.AreEqual(0, store.GetRemainingCount(PlayerId, wallet));

            // N未達なら加算のみ
            // Below N it simply accumulates
            Assert.IsFalse(ConstructionWalletUtil.WouldCondense(store.GetRemainingCount(PlayerId, wallet), 3));
            store.ApplyReturn(PlayerId, wallet, false);
            Assert.AreEqual(1, store.GetRemainingCount(PlayerId, wallet));

            // 通知はFlushまで溜まり、財布ごと1通へ集約される
            // Notifications accumulate until Flush and collapse into one per wallet
            Assert.AreEqual(0, changes);
            store.FlushChanges();
            Assert.AreEqual(1, changes);
        }

        [Test]
        public void 設置と撤去の残数遷移はConstructionWalletUtilが唯一の正本になる()
        {
            // 遷移式そのものの定義。クライアントの先読みも同じ関数を呼ぶ
            // The transitions themselves; the client's look-ahead calls these very functions
            Assert.AreEqual(0, ConstructionWalletUtil.AdvanceOnRemoval(2, true));
            Assert.AreEqual(3, ConstructionWalletUtil.AdvanceOnRemoval(2, false));
            Assert.AreEqual(1, ConstructionWalletUtil.AdvanceOnPlacement(2, 3, true));
            Assert.AreEqual(4, ConstructionWalletUtil.AdvanceOnPlacement(2, 3, false));

            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var store = serviceProvider.GetService<RemainingPlacementCountDataStore>();
            var wallet = ForUnitTestModBlockId.GearBeltConveyor;

            // サーバーの書き込み口が遷移式を経由していること。素材を払った設置は残りN-1へ
            // The server's write port goes through the transition; a placement that paid materials lands on N-1
            store.ApplyPlacement(PlayerId, wallet, 3, ConstructionWalletUsage.PaidAndRefilled);
            Assert.AreEqual(ConstructionWalletUtil.AdvanceOnPlacement(0, 3, false), store.GetRemainingCount(PlayerId, wallet));

            store.ApplyPlacement(PlayerId, wallet, 3, ConstructionWalletUsage.CoveredByWallet);
            Assert.AreEqual(ConstructionWalletUtil.AdvanceOnPlacement(2, 3, true), store.GetRemainingCount(PlayerId, wallet));

            store.ApplyReturn(PlayerId, wallet, false);
            Assert.AreEqual(ConstructionWalletUtil.AdvanceOnRemoval(1, false), store.GetRemainingCount(PlayerId, wallet));

            store.ApplyReturn(PlayerId, wallet, true);
            Assert.AreEqual(ConstructionWalletUtil.AdvanceOnRemoval(2, true), store.GetRemainingCount(PlayerId, wallet));

            // 空の財布で賄う設置は財布の判断漏れなので落ちる
            // A wallet-covered placement on an empty wallet means the caller skipped the wallet's decision, so it throws
            Assert.Throws<InvalidOperationException>(() => store.ApplyPlacement(PlayerId, wallet, 3, ConstructionWalletUsage.CoveredByWallet));
        }

        [Test]
        public void 読み取りだけではセーブに現れず0件はセーブしない()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var store = serviceProvider.GetService<RemainingPlacementCountDataStore>();
            var wallet = ForUnitTestModBlockId.GearBeltConveyor;

            store.GetRemainingCount(PlayerId, wallet);
            Assert.IsEmpty(store.GetSaveJsonObject());

            // 残り0まで使い切った財布はセーブに残らない
            // A wallet spent back down to zero leaves nothing in the save
            store.ApplyPlacement(PlayerId, wallet, 3, ConstructionWalletUsage.PaidAndRefilled);
            store.ApplyPlacement(PlayerId, wallet, 3, ConstructionWalletUsage.CoveredByWallet);
            store.ApplyPlacement(PlayerId, wallet, 3, ConstructionWalletUsage.CoveredByWallet);
            Assert.IsEmpty(store.GetSaveJsonObject().SelectMany(p => p.Entries));
        }
    }
}
