using System;
using Core.Block.BlockFactory;
using Core.Block.Blocks.Chest;
using Core.Update;
using Game.World.Interface.DataStore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Test.Module.TestMod;

namespace Test.CombinedTest.Game
{
    public class BeltConveyorInsertTest
    {

        //2つのアイテムがチェストから出されてベルトコンベアに入り、全てチェストに入るテスト
        [Test]
        public void TwoItemIoTest()
        {
            var (_, saveServiceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModDirectory.ForUnitTestModDirectory);
            var worldBlockDatastore = saveServiceProvider.GetService<IWorldBlockDatastore>();
            var blockFactory = saveServiceProvider.GetService<BlockFactory>();

            var inputChest = (VanillaChest)blockFactory.Create(UnitTestModBlockId.ChestId,1);
            var beltConveyor = blockFactory.Create(UnitTestModBlockId.BeltConveyorId,2);
            var outputChest = (VanillaChest)blockFactory.Create(UnitTestModBlockId.ChestId,3);
            
            //それぞれを設置
            worldBlockDatastore.AddBlock(inputChest,0,0,BlockDirection.North);
            worldBlockDatastore.AddBlock(beltConveyor,0,1,BlockDirection.North);
            worldBlockDatastore.AddBlock(outputChest,0,2,BlockDirection.North);
            
            //インプットチェストにアイテムを2つ入れる
            inputChest.SetItem(0,1,2);
            
            //ベルトコンベアのアイテムが出てから入るまでの6秒間アップデートする
            var now = DateTime.Now;
            while (DateTime.Now - now < TimeSpan.FromSeconds(5))
            {
                GameUpdate.Update();
            }
            
            //アイテムが出ているか確認
            Assert.AreEqual(0,inputChest.GetItem(0).Count);
            //アイテムが入っているか確認
            Assert.AreEqual(2,outputChest.GetItem(0).Count);
        }
    }
}