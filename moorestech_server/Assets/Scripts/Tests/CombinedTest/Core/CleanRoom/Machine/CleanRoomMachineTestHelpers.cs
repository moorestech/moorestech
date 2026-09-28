using Tests.Util.EnergySystem;
using Core.Inventory;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.CleanRoom;
using Game.Block.Blocks.CleanRoom.Machine;
using Game.Block.Blocks.Machine;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Block.Interface.State;
using Game.CleanRoom;
using Game.Context;
using Game.EnergySystem;
using MessagePack;
using NUnit.Framework;
using System.Linq;
using Tests.Module.TestMod;
using Tests.Util;
using UnityEngine;

namespace Tests.CombinedTest.Core.CleanRoom.Machine
{
    // 加工・給電テストの共通土台
    // Shared processing and power setup for machine tests
    internal static class CleanRoomMachineTestHelpers
    {
        #region TestHelper

        // 内寸3x3x3の密閉室に満電用フィルターを1台置く
        // Build a sealed 3x3x3-interior room with one filter prepared for full-power operation
        internal static IBlock BuildSmallCleanRoomWithFilter()
        {
            CleanRoomDetectionTest.BuildBox(new Vector3Int(0, 0, 0), new Vector3Int(4, 4, 4));
            var filter = CleanRoomHatchTest.PlaceBlock(ForUnitTestModBlockId.CleanRoomAirFilterId, new Vector3Int(1, 1, 1));
            filter.GetComponent<IOpenableBlockInventoryComponent>().SetItem(0, ForUnitTestItemId.TestCleanRoomFilter, 5);
            return filter;
        }

        internal static void LoadMachineInput(IBlock machine, int count)
        {
            var recipe = MasterHolder.MachineRecipesMaster.GetRecipeElement(System.Guid.Parse("19b0d248-0ce5-4e5f-b59c-5897177b6268"));
            MachineRecipeSelectTestUtil.SelectRecipe(machine, recipe);
            machine.GetComponent<IOpenableBlockInventoryComponent>().SetItem(0, ForUnitTestItemId.TestChipRawWafer, count);
        }

        internal static bool IsAnyChipLevel(ItemId itemId)
        {
            return itemId.Equals(ForUnitTestItemId.TestSemiconductorChipLv1) ||
                   itemId.Equals(ForUnitTestItemId.TestSemiconductorChipLv2) ||
                   itemId.Equals(ForUnitTestItemId.TestSemiconductorChipLv3) ||
                   itemId.Equals(ForUnitTestItemId.TestSemiconductorChipLv4);
        }

        internal static void TickRoom(IBlock filter, IBlock machine)
        {
            // 清浄機は電線経由で満電を維持し、機械は内部経路で満電にして同tick進める
            // Keep the filter fully powered through wires and the machine through its internal path within the same tick
            EnsureFilterWiredPower(filter);
            machine.GetComponent<CleanRoomMachineProcessorComponent>().SupplyExternalPower(100f);
            GameUpdater.UpdateOneTick();
        }

        // 清浄機が発電機付きセグメントに居なければ、部屋外の電柱経由で満電の発電機を接続する
        // Unless the filter's segment already has a generator, wire a full-power generator through a pole outside the room
        internal static void EnsureFilterWiredPower(IBlock filter)
        {
            var datastore = ServerContext.GetService<IElectricWireNetworkLookup>();
            if (datastore.TryGetEnergySegment(filter.BlockInstanceId, out var segment) && 0 < ElectricNetworkReflectionTestUtil.GetGenerators(segment).Count) return;
            ElectricWireTestUtil.WirePower(filter.BlockPositionInfo.OriginalPos, new Vector3Int(30, 0, 30), 100f);
        }

        internal static void TickWithPower(IBlock block, float power, int ticks)
        {
            // 既存の電力系テストと同じくConsumerへ毎tick直接供給する
            // Supply the consumer directly every tick, matching existing powered block tests
            var consumer = block.GetComponent<CleanRoomMachineProcessorComponent>();
            for (var i = 0; i < ticks; i++)
            {
                consumer.SupplyExternalPower(power);
                GameUpdater.UpdateOneTick();
            }
        }

        // GetBlockStateDetails結果をデシリアライズ
        // Deserialize the result of GetBlockStateDetails
        internal static CommonMachineBlockStateDetail GetCommonMachineState(IBlock machine)
        {
            var stateObservable = machine.GetComponent<IBlockStateObservable>();
            var details = stateObservable.GetBlockStateDetails();
            var detail = details.First(d => d.Key == CommonMachineBlockStateDetail.BlockStateDetailKey);
            return MessagePackSerializer.Deserialize<CommonMachineBlockStateDetail>(detail.Value);
        }

        #endregion
    }
}
