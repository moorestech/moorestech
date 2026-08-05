using System;
using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.BlockSystem.PlaceSystem.ElectricWireConnect.Parts;
using Core.Master;
using Game.Context;
using Game.UnlockState;
using Mooresmaster.Model.BlocksModule;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Client.Tests.PlaceSystem.ElectricWireConnect
{
    public class ElectricWirePoleSelectionTest
    {
        [SetUp]
        public void SetUp()
        {
            // MasterHolderの静的初期化だけが目的で戻り値は使わない
            // Only used to initialize the static MasterHolder; the return values are unused
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }

        [Test]
        public void サイクルは末尾の次に先頭へ戻り前送りは先頭の前に末尾へ回る()
        {
            // 3種の電柱リストでインデックスの循環だけを検証する
            // Verify index wrap-around with a three-pole list
            var poles = new List<BlockId> { new(1), new(2), new(3) };
            var selection = new ElectricWirePoleSelection(poles);

            Assert.AreEqual(new BlockId(1), selection.SelectedBlockId);
            selection.CycleNext();
            selection.CycleNext();
            selection.CycleNext();
            Assert.AreEqual(new BlockId(1), selection.SelectedBlockId);
            selection.CyclePrevious();
            Assert.AreEqual(new BlockId(3), selection.SelectedBlockId);
        }

        [Test]
        public void 未解放の電柱は列挙から除外される()
        {
            var unlockState = ServerContext.GetService<IGameUnlockStateDataController>();

            var poles = ElectricWirePoleSelection.ListUnlockedPoles(unlockState);

            // ロック済みの電柱は含まれず、初期解放済みの電柱は含まれる
            // The locked pole is absent while the default-unlocked pole is present
            Assert.IsFalse(poles.Contains(ForUnitTestModBlockId.LockedElectricPoleId));
            Assert.IsTrue(poles.Contains(ForUnitTestModBlockId.ElectricPoleId));
        }

        [Test]
        public void 電柱以外のBlockTypeは解放済みでも列挙から除外される()
        {
            var unlockState = ServerContext.GetService<IGameUnlockStateDataController>();

            var poles = ElectricWirePoleSelection.ListUnlockedPoles(unlockState);

            // TestBlockはBlockType=Blockで初期解放済みだが、電柱ではないため除外される
            // TestBlock is BlockType=Block and unlocked by default, but excluded for not being a pole
            Assert.IsFalse(poles.Contains(ForUnitTestModBlockId.BlockId));
        }

        [Test]
        public void 解放済み電柱はSortPriority昇順でGuidタイブレークして並ぶ()
        {
            var unlockState = ServerContext.GetService<IGameUnlockStateDataController>();

            // 通常はロック済みの電柱も解放し、複数電柱での並び検証を成立させる
            // Also unlock the normally-locked pole so ordering across multiple poles is exercised
            unlockState.UnlockBlock(MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.LockedElectricPoleId).BlockGuid);

            // 実装のOrderBy式を複製して期待値を求め、入力配列を反転して並び順が配列順に依存しないことを確認する
            // Duplicate the implementation's OrderBy expression for the expectation and reverse the input array to prove order-independence
            Array.Reverse(MasterHolder.BlockMaster.Blocks.Data);
            var expected = MasterHolder.BlockMaster.Blocks.Data
                .Where(block => block.BlockType == BlockMasterElement.BlockTypeConst.ElectricPole)
                .Where(block => unlockState.BlockUnlockStateInfos.TryGetValue(block.BlockGuid, out var info) && info.IsUnlocked)
                .OrderBy(block => block.SortPriority ?? 0)
                .ThenBy(block => block.BlockGuid)
                .Select(block => MasterHolder.BlockMaster.GetBlockId(block.BlockGuid))
                .ToList();

            var actual = ElectricWirePoleSelection.ListUnlockedPoles(unlockState);

            CollectionAssert.AreEqual(expected, actual);
        }
    }
}
