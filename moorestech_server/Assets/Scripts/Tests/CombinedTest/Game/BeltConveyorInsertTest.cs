using System;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.Chest;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Game
{
    public class BeltConveyorInsertTest
    {
        //2つのアイテムがチェストから出されてベルトコンベアに入り、全てチェストに入るテスト
        [Test]
        public void TwoItemIoTest()
        {
            var (_, saveServiceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            
            //それぞれを設置
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.ChestId, Vector3Int.zero, BlockDirection.North, out var inputChest, System.Array.Empty<BlockCreateParam>());
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(0, 0, 1), BlockDirection.North, out var beltConveyor, System.Array.Empty<BlockCreateParam>());
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.ChestId, new Vector3Int(0, 0, 2), BlockDirection.North, out var outputChest, System.Array.Empty<BlockCreateParam>());
            
            //インプットチェストにアイテムを2つ入れる
            var inputChestComponent = inputChest.GetComponent<VanillaChestComponent>();
            inputChestComponent.SetItem(0, new ItemId(1), 2);
            
            //ベルトコンベアのアイテムが出てから入るまでの6秒間アップデートする
            var now = DateTime.Now;
            while (DateTime.Now - now < TimeSpan.FromSeconds(5)) GameUpdater.UpdateWithWait();
            
            //アイテムが出ているか確認
            Assert.AreEqual(0, inputChestComponent.GetItem(0).Count);
            //アイテムが入っているか確認
            var outputChestComponent = outputChest.GetComponent<VanillaChestComponent>();
            Assert.AreEqual(2, outputChestComponent.GetItem(0).Count);
        }
    }
}