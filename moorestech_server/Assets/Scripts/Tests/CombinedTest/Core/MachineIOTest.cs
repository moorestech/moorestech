using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Core.Item.Interface;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.Machine;
using Game.Block.Blocks.Machine.Inventory;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.EnergySystem;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Core
{
    public class MachineIOTest
    {
        //アイテムが通常通り処理されるかのテスト
        [Test]
        public void ItemProcessingOutputTest()
        {
            // テスト用DIコンテナと主要サービスを初期化
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            
            var itemStackFactory = ServerContext.ItemStackFactory;
            var blockFactory = ServerContext.BlockFactory;
            
            // テスト用レシピとブロックを準備し、必要なインプットを投入
            var recipe = MasterHolder.MachineRecipesMaster.MachineRecipes.Data[0];
            
            
            var blockId = MasterHolder.BlockMaster.GetBlockId(recipe.BlockGuid);
            var block = blockFactory.Create(blockId, new BlockInstanceId(1), new BlockPositionInfo(Vector3Int.one, BlockDirection.North, Vector3Int.one));
            var blockInventory = block.GetComponent<VanillaMachineBlockInventoryComponent>();
            // すべての入力アイテムをインベントリにセットアップ
            foreach (var inputItem in recipe.InputItems)
            {
                blockInventory.InsertItem(itemStackFactory.Create(inputItem.ItemGuid, inputItem.Count));
            }
            
            var blockMachineComponent = block.GetComponent<VanillaElectricMachineComponent>();
            
            // レシピが完了するまで十分な時間エネルギー供給＋アップデートを継続
            var craftTime = DateTime.Now.AddSeconds(recipe.Time);
            //最大クラフト時間を超過するまでクラフトする
            while (craftTime.AddSeconds(0.2).CompareTo(DateTime.Now) == 1)
            {
                blockMachineComponent.SupplyEnergy(new ElectricPower(10000));
                GameUpdater.UpdateWithWait();
            }
            
            //検証
            (List<IItemStack> input, List<IItemStack> output) = GetInputOutputSlot(blockInventory);
            
            // インプットは全て消費されていることを確認
            Assert.AreEqual(0, input.Count);
            foreach (var inputItem in input) Assert.AreEqual(ItemMaster.EmptyItemId, inputItem.Id);
            
            // アウトプットが期待通り生成されていることを確認
            Assert.AreNotEqual(0, output.Count);
            for (var i = 0; i < output.Count; i++)
            {
                var expectedOutputId = MasterHolder.ItemMaster.GetItemId(recipe.OutputItems[i].ItemGuid);
                Assert.AreEqual(expectedOutputId, output[i].Id);
                Assert.AreEqual(recipe.OutputItems[i].Count, output[i].Count);
            }
        }
        
        [Test]
        public void ItemProcessingRemainInputTest()
        {
            // テスト用DIコンテナと主要サービスを初期化
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            
            var itemStackFactory = ServerContext.ItemStackFactory;
            var blockFactory = ServerContext.BlockFactory;
            
            // isRemain指定のレシピを取得して対象ブロックのインスタンスを作成
            var recipe = MasterHolder.MachineRecipesMaster.MachineRecipes.Data
                .First(r => r.InputItems.Any(input => input.IsRemain.HasValue && input.IsRemain.Value));
            
            var blockId = MasterHolder.BlockMaster.GetBlockId(recipe.BlockGuid);
            var block = blockFactory.Create(blockId, new BlockInstanceId(2), new BlockPositionInfo(Vector3Int.one, BlockDirection.North, Vector3Int.one));
            var blockInventory = block.GetComponent<VanillaMachineBlockInventoryComponent>();
            // レシピ通りにインプットへ投入する（isRemainアイテムも投入）
            foreach (var inputItem in recipe.InputItems)
            {
                blockInventory.InsertItem(itemStackFactory.Create(inputItem.ItemGuid, inputItem.Count));
            }
            
            var blockMachineComponent = block.GetComponent<VanillaElectricMachineComponent>();
            
            // 処理完了まで十分な時間エネルギー供給＋アップデートを回す
            var craftTime = DateTime.Now.AddSeconds(recipe.Time);
            while (craftTime.AddSeconds(0.2).CompareTo(DateTime.Now) == 1)
            {
                blockMachineComponent.SupplyEnergy(new ElectricPower(10000));
                GameUpdater.UpdateWithWait();
            }
            
            (List<IItemStack> input, List<IItemStack> output) = GetInputOutputSlot(blockInventory);
            
            // isRemain 指定のインプットが加工後も残っていることを検証
            Assert.AreEqual(1, input.Count);
            var remainSource = recipe.InputItems.First(i => i.IsRemain.HasValue && i.IsRemain.Value);
            var expectedRemainId = MasterHolder.ItemMaster.GetItemId(remainSource.ItemGuid);
            Assert.AreEqual(expectedRemainId, input[0].Id);
            Assert.AreEqual(remainSource.Count, input[0].Count);
            
            // アウトプットが通常通り生成されていることを検証
            Assert.AreNotEqual(0, output.Count);
            for (var i = 0; i < output.Count; i++)
            {
                var expectedOutputId = MasterHolder.ItemMaster.GetItemId(recipe.OutputItems[i].ItemGuid);
                Assert.AreEqual(expectedOutputId, output[i].Id);
                Assert.AreEqual(recipe.OutputItems[i].Count, output[i].Count);
            }
        }
        
        public (List<IItemStack>, List<IItemStack>) GetInputOutputSlot(VanillaMachineBlockInventoryComponent vanillaMachineInventory)
        {
            // 非公開フィールドからインプット／アウトプットスロットを取り出し、テスト用に整形して返す
            var vanillaMachineInputInventory = (VanillaMachineInputInventory)typeof(VanillaMachineBlockInventoryComponent)
                .GetField("_vanillaMachineInputInventory", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(vanillaMachineInventory);
            var vanillaMachineOutputInventory = (VanillaMachineOutputInventory)typeof(VanillaMachineBlockInventoryComponent)
                .GetField("_vanillaMachineOutputInventory", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(vanillaMachineInventory);
            
            var inputSlot = vanillaMachineInputInventory.InputSlot.Where(i => i.Count != 0).ToList();
            inputSlot.Sort((a, b) => a.Id.AsPrimitive() - b.Id.AsPrimitive());
            
            var outputSlot = vanillaMachineOutputInventory.OutputSlot.Where(i => i.Count != 0).ToList();
            outputSlot.Sort((a, b) => a.Id.AsPrimitive() - b.Id.AsPrimitive());
            
            return (inputSlot, outputSlot);
        }
    }
}
