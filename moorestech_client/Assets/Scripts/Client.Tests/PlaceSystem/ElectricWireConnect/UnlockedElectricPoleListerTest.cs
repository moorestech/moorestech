using System.Linq;
using Client.Game.InGame.BlockSystem.PlaceSystem.ElectricWireConnect.Parts;
using Core.Master;
using Game.Context;
using Game.UnlockState;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Client.Tests.PlaceSystem.ElectricWireConnect
{
    public class UnlockedElectricPoleListerTest
    {
        [SetUp]
        public void SetUp()
        {
            // MasterHolderの静的初期化だけが目的で戻り値は使わない
            // Only used to initialize the static MasterHolder; the return values are unused
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }

        [Test]
        public void 未解放の電柱は列挙から除外される()
        {
            var unlockState = ServerContext.GetService<IGameUnlockStateDataController>();

            var poles = UnlockedElectricPoleLister.List(unlockState);

            // ロック済みの電柱は含まれず、初期解放済みの電柱は含まれる
            // The locked pole is absent while the default-unlocked pole is present
            Assert.IsFalse(poles.Contains(ForUnitTestModBlockId.LockedElectricPoleId));
            Assert.IsTrue(poles.Contains(ForUnitTestModBlockId.ElectricPoleId));
        }

        [Test]
        public void 電柱以外のBlockTypeは解放済みでも列挙から除外される()
        {
            var unlockState = ServerContext.GetService<IGameUnlockStateDataController>();

            var poles = UnlockedElectricPoleLister.List(unlockState);

            // TestBlockはBlockType=Blockで初期解放済みだが、電柱ではないため除外される
            // TestBlock is BlockType=Block and unlocked by default, but excluded for not being a pole
            Assert.IsFalse(poles.Contains(ForUnitTestModBlockId.BlockId));
        }

        [Test]
        public void 解放済み電柱はSortPriority昇順で同値ならGuid昇順に並ぶ()
        {
            var unlockState = ServerContext.GetService<IGameUnlockStateDataController>();

            // 通常はロック済みの電柱も解放し、SortPriority同値の2件でタイブレークを成立させる
            // Also unlock the normally-locked pole so the two same-SortPriority entries exercise the tiebreak
            unlockState.UnlockBlock(MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.LockedElectricPoleId).BlockGuid);

            var actual = UnlockedElectricPoleLister.List(unlockState);

            // 比較対象が1件だけの空検証にならないよう、両電柱が含まれることを先に確認する
            // Confirm both poles are present first so the check below isn't a vacuous single-item comparison
            Assert.IsTrue(actual.Contains(ForUnitTestModBlockId.ElectricPoleId));
            Assert.IsTrue(actual.Contains(ForUnitTestModBlockId.LockedElectricPoleId));

            // 各BlockIdからSortPriorityとBlockGuidを引き直し、隣接ペアの並び順という性質そのものを検証する
            // Look up SortPriority and BlockGuid from each returned BlockId and assert the adjacency-ordering property itself
            for (var i = 1; i < actual.Count; i++)
            {
                var previous = MasterHolder.BlockMaster.GetBlockMaster(actual[i - 1]);
                var current = MasterHolder.BlockMaster.GetBlockMaster(actual[i]);
                var previousPriority = previous.SortPriority ?? 0;
                var currentPriority = current.SortPriority ?? 0;

                Assert.LessOrEqual(previousPriority, currentPriority);
                if (previousPriority == currentPriority)
                    Assert.Less(previous.BlockGuid.CompareTo(current.BlockGuid), 0);
            }
        }
    }
}
