using System;
using System.Linq;
using Game.Block.Blocks.Machine.Inventory;
using Game.Block.Component;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Component.ConnectJudge;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.SaveLoad.Interface;
using Game.SaveLoad.Json;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.UnitTest.Game.SaveLoad
{
    // 初期ロードでもブロック設置イベントは発火し、ベルト-機械の接続がセーブロードで復元されることを検証
    // Verify block place events still fire on initial load so belt-machine connections survive save/load
    public class BlockConnectionSaveLoadTest
    {
        // 設置イベントを丸ごと抑制すると壊れる回帰をロードで捕捉する（レビュー指摘の代替案の否定根拠）
        // Catches the regression where suppressing the whole place event breaks connection restore on load
        [Test]
        public void ベルトと機械の接続が初期ロードで復元される()
        {
            var (_, saveServiceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var beltPos = new Vector3Int(0, 0, 9);
            var machinePos = new Vector3Int(0, 0, 10);

            // ベルトを先に設置する。ロード順も同じになり、接続は機械設置イベント経由でのみ張られる
            // Place belt first; load order matches, so the connection can only form via the machine's place event
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.BeltConveyorId, beltPos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var belt);
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.MachineId, machinePos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var machine);

            // 設置直後にベルトが機械へ1件接続していることを前提確認する
            // Sanity-check that the belt has exactly one connection to the machine right after placement
            var savedConnector = belt.GetComponent<BlockConnectorComponent<IBlockInventory, DefaultConnectJudge>>();
            Assert.AreEqual(1, savedConnector.ConnectedTargets.Count);

            var saveJson = saveServiceProvider.GetService<AssembleSaveJsonText>().AssembleSaveJson();

            // 別ワールドへロードし、初期ロード経路（TryAddBlock isInitialLoad=true）を通す
            // Load into a fresh world, exercising the initial-load path (TryAddBlock isInitialLoad=true)
            var (_, loadServiceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            (loadServiceProvider.GetService<IWorldSaveDataLoader>() as WorldLoaderFromJson).Load(saveJson);

            // ロード後もベルト-機械の接続が復元されていることを検証する
            // Verify the belt-machine connection is restored after load
            var loadedBelt = ServerContext.WorldBlockDatastore.GetBlock(beltPos);
            var loadedMachineInventory = ServerContext.WorldBlockDatastore.GetBlock(machinePos).GetComponent<VanillaMachineBlockInventoryComponent>();
            var loadedConnector = loadedBelt.GetComponent<BlockConnectorComponent<IBlockInventory, DefaultConnectJudge>>();

            Assert.AreEqual(1, loadedConnector.ConnectedTargets.Count);
            Assert.AreSame(loadedMachineInventory, loadedConnector.ConnectedTargets.First().Key);
        }
    }
}
