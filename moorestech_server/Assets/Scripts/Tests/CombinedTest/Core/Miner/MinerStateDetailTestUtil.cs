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
using Mooresmaster.Model.BlocksModule;
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
    ///     採掘機の状態配信テストで共有する設置・スロット操作の補助
    ///     Placement and slot helpers shared by the miner state publishing tests
    /// </summary>
    internal static class MinerStateDetailTestUtil
    {
        internal static readonly Vector3Int PoleOffset = new(2, 0, 0);

        internal static (IBlock miner, VanillaMinerProcessorComponent processor) PlacePoweredMiner()
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

        internal static (IBlock miner, VanillaMinerProcessorComponent processor) PlacePoweredGearMiner()
        {
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            var (_, pos) = MinerMiningTest.GetItemMapVein();
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.GearMiner, pos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var miner);

            var generatorPosition = pos + new Vector3Int(0, 0, -1);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.InfinityTorqueSimpleGearGenerator, generatorPosition, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            GameUpdater.RunFrames(2);

            var processor = miner.GetComponent<VanillaMinerProcessorComponent>();
            Assert.IsTrue(processor.IsMining, "前提: 鉱脈上で給電された歯車採掘機は採掘中になる");
            return (miner, processor);
        }

        // 採掘物と別アイテムで全スロットを塞ぎ、採掘物を入れられない出力満杯を作る
        // Block every slot with an item other than the mined one so the output cannot take the mined item
        internal static void FillOutputSlots(VanillaMinerProcessorComponent processor)
        {
            var miningItems = (List<IItemStack>)typeof(VanillaMinerProcessorComponent).GetField("_miningItems", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(processor);
            var blockerItemId = miningItems[0].Id == new ItemId(1) ? new ItemId(2) : new ItemId(1);
            for (var i = 0; i < processor.GetSlotSize(); i++) processor.SetItem(i, blockerItemId, 1);
        }

        internal static void ClearOutputSlots(VanillaMinerProcessorComponent processor)
        {
            for (var i = 0; i < processor.GetSlotSize(); i++) processor.SetItem(i, ItemMaster.EmptyItemId, 0);
        }

        internal static CommonMachineBlockStateDetail ReadCommonDetail(BlockState state)
        {
            return MessagePackSerializer.Deserialize<CommonMachineBlockStateDetail>(state.CurrentStateDetails[CommonMachineBlockStateDetail.BlockStateDetailKey]);
        }
    }
}
