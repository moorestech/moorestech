using System;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.Machine;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Block.Interface.State;
using Game.Context;
using Game.EnergySystem;
using MessagePack;
using NUnit.Framework;
using Tests.Module;
using Tests.Module.TestMod;
using Tests.Util;
using UnityEngine;
using static Tests.Util.ElectricNetworkReflectionTestUtil;

namespace Tests.CombinedTest.Core.Fluid
{
    /// <summary>
    ///     ポンプのテストで共有する給電付き設置と状態読み出しの補助
    ///     Powered placement and state-reading helpers shared by the pump tests
    /// </summary>
    internal static class PumpStateDetailTestUtil
    {
        // ForUnitTestModの map.json で定義された水の鉱脈座標
        // Water vein coordinates defined in ForUnitTestMod map.json
        internal static readonly Vector3Int WaterVeinPos = new(10, 0, 0);
        internal static readonly Vector3Int PoleOffset = new(2, 0, 0);

        internal static CommonMachineBlockStateDetail GetCommonDetail(IBlock pump)
        {
            var state = pump.GetBlockState();
            return MessagePackSerializer.Deserialize<CommonMachineBlockStateDetail>(state.CurrentStateDetails[CommonMachineBlockStateDetail.BlockStateDetailKey]);
        }

        internal static IBlock PlacePoweredPump(Vector3Int pos)
        {
            return PlacePoweredPump(pos, ForUnitTestModBlockId.ElectricPump, PoleOffset);
        }

        internal static IBlock PlacePoweredPump(Vector3Int pos, BlockId blockId, Vector3Int poleOffset)
        {
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            var added = worldBlockDatastore.TryAddBlock(blockId, pos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var pump);
            Assert.IsTrue(added, $"Failed to place pump at {pos}");

            // ポンプを電柱へ接続して電力網を成立させる
            // Connect the pump to a pole so it belongs to a usable electric network
            var polePosition = pos + poleOffset;
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.ElectricPoleId, polePosition, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            ElectricWireTestUtil.Connect(pos, polePosition);

            // ポンプが属するワイヤーセグメントへテスト発電機を登録し powerRate=1.0 にする
            // Register a test generator into the pump's wire segment so powerRate = 1.0
            GameUpdater.UpdateOneTick();
            var networkDatastore = ServerContext.GetService<IElectricWireNetworkLookup>();
            Assert.IsTrue(networkDatastore.TryGetEnergySegment(pump.BlockInstanceId, out var segment));
            AddGenerator(segment, new TestElectricGenerator(new ElectricPower(10000), new BlockInstanceId(10)));
            GameUpdater.UpdateOneTick();

            return pump;
        }
    }
}
