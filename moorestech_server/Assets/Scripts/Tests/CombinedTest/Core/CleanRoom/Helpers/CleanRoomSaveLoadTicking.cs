using Tests.Util.EnergySystem;
using System;
using System.Linq;
using System.Reflection;
using System.Text.Json.Nodes;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.CleanRoom;
using Game.Block.Blocks.CleanRoom.Machine;
using Game.Block.Blocks.Machine;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.CleanRoom;
using Game.Context;
using Game.EnergySystem;
using Game.SaveLoad.Interface;
using Game.SaveLoad.Json;
using Game.World.Interface.DataStore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using Tests.Util;
using UnityEngine;

namespace Tests.CombinedTest.Core.CleanRoom.Helpers
{
    // クリーンルーム復元テストの進行と給電をまとめる
    // Drive ticks and power for clean-room restoration tests
    internal static class CleanRoomSaveLoadTicking
    {
        internal static void TickUntilRealCleanClass(IBlock filter, CleanRoomDatastore datastore, Vector3Int cell, int maxTicks)
        {
            for (var i = 0; i < maxTicks && !HasRealCleanClass(datastore, cell); i++) TickFilter(filter);
        }

        internal static bool HasRealCleanClass(CleanRoomDatastore datastore, Vector3Int cell)
        {
            return datastore.TryGetCleanRoomAt(cell, out var room) &&
                   room.ThresholdIndex < MasterHolder.CleanRoomMaster.OutThresholdIndex;
        }

        internal static void TickUntilProcessing(IBlock filter, IBlock machine, CleanRoomMachineProcessorComponent processor, int maxTicks)
        {
            for (var i = 0; i < maxTicks && processor.CurrentState != ProcessState.Processing; i++) TickRoom(filter, machine);
        }

        internal static void TickFilter(IBlock filter)
        {
            // 清浄機を電線経由の満電で毎tick進め、室内純度を進める
            // Keep the filter fully powered through wires each tick to advance room purity
            EnsureFilterWiredPower(filter);
            GameUpdater.UpdateOneTick();
        }

        internal static void TickRoom(IBlock filter, IBlock machine)
        {
            // 清浄機は電線経由、機械は内部経路で同じtick満電にし、通常の室内加工経路を通す
            // Power the filter through wires and the machine through its internal path in the same tick for normal in-room processing
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


    }
}
