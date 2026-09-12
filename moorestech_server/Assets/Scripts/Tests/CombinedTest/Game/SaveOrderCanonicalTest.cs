using System;
using System.Collections.Generic;
using System.Linq;
using Core.Master;
using Core.Update;
using Game.Block.Interface;
using Game.Construction;
using Game.Context;
using Game.Hotbar;
using Game.PlayerInventory.Interface;
using Game.SaveLoad.Json;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Game
{
    // Dictionaryは削除跡のスロットを次のAddが再利用するため、内容が同じでも列挙順が履歴で変わる。
    // A Dictionary reuses the slot freed by a removal, so enumeration order depends on history even for identical contents.
    public class SaveOrderCanonicalTest
    {
        [Test]
        public void 撤去と設置を挟んでも保存されるブロックの並びはインスタンスIDの昇順になる()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;

            // ID採番を固定し、挿入順が昇順と偶然一致して検査が空振りするのを防ぐ
            // Fix the id allocation so the insertion order cannot coincidentally match ascending order and make the check vacuous
            GameRandom.Reseed(4242UL);
            for (var x = 0; x < 8; x++)
            {
                world.TryAddBlock(ForUnitTestModBlockId.ChestId, new Vector3Int(x * 2, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            }

            // 撤去で空いたスロットを後の設置が埋める。実プレイではほぼ常時起きる
            // Later placements fill the slots freed by removals, which happens almost constantly in real play
            world.RemoveBlock(new Vector3Int(2, 0), BlockRemoveReason.ManualRemove);
            world.RemoveBlock(new Vector3Int(8, 0), BlockRemoveReason.ManualRemove);
            world.TryAddBlock(ForUnitTestModBlockId.ChestId, new Vector3Int(40, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            world.TryAddBlock(ForUnitTestModBlockId.ChestId, new Vector3Int(42, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);

            var captured = serviceProvider.GetRequiredService<AssembleSaveJsonText>().Capture();
            var instanceIds = captured.World.Select(block => block.InstanceId).ToArray();
            Assert.AreEqual(8, instanceIds.Length, "検査対象のブロックが想定数だけ保存されていない");
            CollectionAssert.AreEqual(instanceIds.OrderBy(id => id).ToArray(), instanceIds, "保存されるブロックの並びが正準化されていない");
        }

        // 再接続や退出後の再参加でプレイヤー鍵のDictionaryは挿入順が入れ替わる。並びが変わると比較器が実在しない差分を並べる
        // Reconnects and rejoins shuffle the insertion order of player-keyed dictionaries, and a shifted order makes the comparer list differences that do not exist
        [Test]
        public void プレイヤー鍵のデータは挿入順が降順でもプレイヤーID昇順で保存される()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            // 昇順と偶然一致して検査が空振りしないよう、降順に作る
            // Build them in descending order so the check cannot pass vacuously by coinciding with ascending order
            var playerIds = new[] { 30, 20, 10 };
            var inventoryDataStore = serviceProvider.GetRequiredService<IPlayerInventoryDataStore>();
            foreach (var playerId in playerIds) inventoryDataStore.GetInventoryData(playerId);

            var hotbarAssignmentDatastore = serviceProvider.GetRequiredService<HotbarAssignmentDatastore>();
            hotbarAssignmentDatastore.LoadHotbar(playerIds.Select(playerId => new PlayerHotbarSaveJsonObject(playerId, new List<string>())).ToList());

            // 財布は外側(プレイヤー)と内側(ブロック)の2段なので、内側も降順に作って両方の正準化を測る
            // The wallet nests blocks inside players, so the inner level is built descending too to measure both canonicalizations
            var walletBlockIds = new[] { ForUnitTestModBlockId.ChestId, ForUnitTestModBlockId.MachineId }
                .OrderByDescending(blockId => blockId.AsPrimitive()).ToArray();
            var remainingPlacementCountDataStore = serviceProvider.GetRequiredService<RemainingPlacementCountDataStore>();
            foreach (var playerId in playerIds)
            foreach (var blockId in walletBlockIds)
                remainingPlacementCountDataStore.Refill(playerId, blockId, 3);

            var captured = serviceProvider.GetRequiredService<AssembleSaveJsonText>().Capture();
            var expectedPlayerIds = new[] { 10, 20, 30 };
            CollectionAssert.AreEqual(expectedPlayerIds, captured.Inventory.Select(inventory => inventory.PlayerId).ToArray(), "プレイヤーインベントリの並びが正準化されていない");
            CollectionAssert.AreEqual(expectedPlayerIds, captured.HotbarAssignments.Select(hotbar => hotbar.PlayerId).ToArray(), "ホットバー割当の並びが正準化されていない");
            CollectionAssert.AreEqual(expectedPlayerIds, captured.RemainingPlacementCounts.Select(wallet => wallet.PlayerId).ToArray(), "残り設置数の並びが正準化されていない");

            var expectedBlockGuids = walletBlockIds
                .OrderBy(blockId => blockId.AsPrimitive())
                .Select(blockId => MasterHolder.BlockMaster.GetBlockMaster(blockId).BlockGuid.ToString()).ToArray();
            foreach (var wallet in captured.RemainingPlacementCounts)
            {
                CollectionAssert.AreEqual(expectedBlockGuids, wallet.Entries.Select(entry => entry.BlockGuid).ToArray(), $"残り設置数の財布の並びが正準化されていない playerId:{wallet.PlayerId}");
            }
        }
    }
}
