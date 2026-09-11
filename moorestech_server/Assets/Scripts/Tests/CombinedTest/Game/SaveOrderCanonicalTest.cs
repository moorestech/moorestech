using System;
using System.Linq;
using Core.Update;
using Game.Block.Interface;
using Game.Context;
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
    }
}
