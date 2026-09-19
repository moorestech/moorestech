using System;
using System.Collections.Generic;
using System.Reflection;
using Core.Item.Interface;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.Machine;
using Game.Block.Blocks.Miner;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Block.Interface.State;
using Game.Context;
using Game.EnergySystem;
using MessagePack;
using NUnit.Framework;
using Server.Boot;
using Tests.Module;
using Tests.Module.TestMod;
using Tests.Util;
using UniRx;
using UnityEngine;
using static Tests.Util.ElectricNetworkReflectionTestUtil;

namespace Tests.CombinedTest.Core.Miner
{
    /// <summary>
    ///     採掘機のUI用電力・状態配信を検証する（裁定 2026-08-17 表示用要求電力は供給と同位置でラッチする / ADR 0010）
    ///     Verifies the miner's UI power and state publishing (ruling 2026-08-17 latch the displayed request with the supply / ADR 0010)
    /// </summary>
    public class MinerBlockStateDetailTest
    {
        private static readonly Vector3Int PoleOffset = new(2, 0, 0);

        [Test]
        public void 採掘と待機の遷移tickでも配信する分子と分母は同じ状態基準になる()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var (miner, processor) = PlacePoweredMiner();
            var publishedRates = new List<float>();
            using var subscription = miner.BlockStateChange.Subscribe(state => publishedRates.Add(ReadCommonDetail(state).PowerRate));

            // 採掘中→出力満杯で待機→空けて採掘再開、と両向きの遷移tickを踏ませる
            // Walk through both transition ticks: mining, idle on a full output, then mining again once emptied
            GameUpdater.RunFrames(5);
            FillOutputSlots(processor);
            GameUpdater.RunFrames(5);
            ClearOutputSlots(processor);
            GameUpdater.RunFrames(5);

            // 発電量は十分なので、分子と分母が同じ基準なら全配信で充足率は1になる
            // Generation is ample, so every publish reads a satisfaction of 1 when both sides share one basis
            Assert.Greater(publishedRates.Count, 0, "配信が1件も無いと検査が空振りになる");
            foreach (var rate in publishedRates) Assert.AreEqual(1f, rate, 0.001f, "遷移tickに分子と分母の状態基準がずれている");
        }

        [Test]
        public void 待機へ落ちたtickは前状態miningと現状態idleを発火つきで配信する()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var (miner, processor) = PlacePoweredMiner();
            GameUpdater.RunFrames(3);

            var published = new List<CommonMachineBlockStateDetail>();
            using var subscription = miner.BlockStateChange.Subscribe(state => published.Add(ReadCommonDetail(state)));
            FillOutputSlots(processor);
            GameUpdater.RunFrames(3);

            var dropCount = published.FindAll(detail => detail.PreviousStateType == VanillaMinerState.Mining.ToStr() && detail.CurrentStateType == VanillaMinerState.Idle.ToStr()).Count;
            Assert.AreEqual(1, dropCount, "採掘→待機の遷移は1回だけ配信されるはず");
        }

        [Test]
        public void 電柱を撤去した待機中の採掘機は供給電力0を発火つきで配信する()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var (miner, processor) = PlacePoweredMiner();
            FillOutputSlots(processor);
            GameUpdater.RunFrames(3);
            Assert.Greater(ReadCommonDetail(miner.GetBlockState()).CurrentPower, 0f, "待機中も待機分の電力は供給されているはず");

            // 給電経路が消えた後は最後の供給値が固着せず0へ落ちる
            // Once the supply path is gone the last supplied value must not stick; it falls to zero
            var fired = 0;
            using var subscription = miner.BlockStateChange.Subscribe(_ => fired++);
            ServerContext.WorldBlockDatastore.RemoveBlock(miner.BlockPositionInfo.OriginalPos + PoleOffset, BlockRemoveReason.ManualRemove);
            GameUpdater.RunFrames(2);

            Assert.AreEqual(0f, ReadCommonDetail(miner.GetBlockState()).CurrentPower, 0.0001f, "無給電のtickでは供給電力は0");
            Assert.Greater(fired, 0, "待機中でも配信値が動いたなら発火するはず");
        }

        private static (IBlock miner, VanillaMinerProcessorComponent processor) PlacePoweredMiner()
        {
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            var (_, pos) = MinerMiningTest.GetItemMapVein();
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.ElectricMinerId, pos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var miner);

            var polePosition = pos + PoleOffset;
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.ElectricPoleId, polePosition, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            ElectricWireTestUtil.Connect(pos, polePosition);

            GameUpdater.UpdateOneTick();
            var networkDatastore = ServerContext.GetService<IElectricWireNetworkLookup>();
            Assert.IsTrue(networkDatastore.TryGetEnergySegment(miner.BlockInstanceId, out var segment));
            AddGenerator(segment, new TestElectricGenerator(new ElectricPower(10000), new BlockInstanceId(10)));
            GameUpdater.UpdateOneTick();

            var processor = miner.GetComponent<VanillaMinerProcessorComponent>();
            Assert.IsTrue(processor.IsMining, "前提: 鉱脈上で給電された採掘機は採掘中になる");
            return (miner, processor);
        }

        // 採掘物と別アイテムで全スロットを塞ぎ、採掘物を入れられない出力満杯を作る
        // Block every slot with an item other than the mined one so the output cannot take the mined item
        private static void FillOutputSlots(VanillaMinerProcessorComponent processor)
        {
            var miningItems = (List<IItemStack>)typeof(VanillaMinerProcessorComponent).GetField("_miningItems", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(processor);
            var blockerItemId = miningItems[0].Id == new ItemId(1) ? new ItemId(2) : new ItemId(1);
            for (var i = 0; i < processor.GetSlotSize(); i++) processor.SetItem(i, blockerItemId, 1);
        }

        private static void ClearOutputSlots(VanillaMinerProcessorComponent processor)
        {
            for (var i = 0; i < processor.GetSlotSize(); i++) processor.SetItem(i, ItemMaster.EmptyItemId, 0);
        }

        private static CommonMachineBlockStateDetail ReadCommonDetail(BlockState state)
        {
            return MessagePackSerializer.Deserialize<CommonMachineBlockStateDetail>(state.CurrentStateDetails[CommonMachineBlockStateDetail.BlockStateDetailKey]);
        }
    }
}
