using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Core.Item;
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
using Tests.Util;
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
            // Prepare test recipe and block, and insert required inputs
            var recipe = MasterHolder.MachineRecipesMaster.MachineRecipes.Data[0];

            // マシンブロックの配置
            // Place the machine block
            var blockId = MasterHolder.BlockMaster.GetBlockId(recipe.BlockGuid);
            ServerContext.WorldBlockDatastore.TryAddBlock(blockId, Vector3Int.one, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var block);
            MachineRecipeSelectTestUtil.SelectRecipe(block, recipe);
            var blockInventory = block.GetComponent<VanillaMachineBlockInventoryComponent>();
            // すべての入力アイテムをインベントリにセットアップ
            foreach (var inputItem in recipe.InputItems)
            {
                blockInventory.InsertItem(itemStackFactory.Create(inputItem.ItemGuid, inputItem.Count));
            }
            
            var blockMachineComponent = block.GetComponent<VanillaMachineProcessorComponent>();
            
            // レシピが完了するまで十分な時間エネルギー供給＋アップデートを継続
            var craftTime = DateTime.Now.AddSeconds(recipe.Time);
            //最大クラフト時間を超過するまでクラフトする
            while (craftTime.AddSeconds(0.2).CompareTo(DateTime.Now) == 1)
            {
                blockMachineComponent.SupplyExternalPower(10000);
                GameUpdater.UpdateOneTick();
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
            // Fetch recipe with isRemain specified and create target block instance
            var recipe = MasterHolder.MachineRecipesMaster.MachineRecipes.Data
                .First(r => r.InputItems.Any(input => input.IsRemain.HasValue && input.IsRemain.Value));

            // マシンブロックの配置
            // Place the machine block
            var blockId = MasterHolder.BlockMaster.GetBlockId(recipe.BlockGuid);
            ServerContext.WorldBlockDatastore.TryAddBlock(blockId, Vector3Int.one, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var block);
            MachineRecipeSelectTestUtil.SelectRecipe(block, recipe);
            var blockInventory = block.GetComponent<VanillaMachineBlockInventoryComponent>();
            // レシピ通りにインプットへ投入する（isRemainアイテムも投入）
            foreach (var inputItem in recipe.InputItems)
            {
                blockInventory.InsertItem(itemStackFactory.Create(inputItem.ItemGuid, inputItem.Count));
            }
            
            var blockMachineComponent = block.GetComponent<VanillaMachineProcessorComponent>();
            
            // 処理完了まで十分な時間エネルギー供給＋アップデートを回す
            var craftTime = DateTime.Now.AddSeconds(recipe.Time);
            while (craftTime.AddSeconds(0.2).CompareTo(DateTime.Now) == 1)
            {
                blockMachineComponent.SupplyExternalPower(10000);
                GameUpdater.UpdateOneTick();
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
        
        // D3(a)回帰(C3): 加工完了直前に出力スロットが満杯だと、実現済み産出物を捨てず完了を保留する（実挿入もCanStoreOutputsと同じ判定を通す）
        // D3(a) regression (C3): when the output slot is full right at completion, hold completion instead of discarding the realized output (the real insert now shares CanStoreOutputs' check)
        [Test]
        public void ItemProcessingHoldsCompletionWhenOutputSlotIsFullTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var itemStackFactory = ServerContext.ItemStackFactory;

            var recipe = MasterHolder.MachineRecipesMaster.MachineRecipes.Data.First(r => 0 < r.InputItems.Length && 0 < r.OutputItems.Length);
            var outputItemId = MasterHolder.ItemMaster.GetItemId(recipe.OutputItems[0].ItemGuid);
            var maxStack = ItemStackLevelDataStore.Instance.GetMaxStack(outputItemId);

            var blockId = MasterHolder.BlockMaster.GetBlockId(recipe.BlockGuid);
            ServerContext.WorldBlockDatastore.TryAddBlock(blockId, Vector3Int.one, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var block);
            MachineRecipeSelectTestUtil.SelectRecipe(block, recipe);
            var blockInventory = block.GetComponent<VanillaMachineBlockInventoryComponent>();
            var blockMachineComponent = block.GetComponent<VanillaMachineProcessorComponent>();

            foreach (var inputItem in recipe.InputItems)
            {
                blockInventory.InsertItem(itemStackFactory.Create(inputItem.ItemGuid, inputItem.Count));
            }

            // 加工を開始させる(1tick分だけ電力供給してIdle→Processingへ遷移)
            // Start processing (one tick of power flips Idle → Processing)
            blockMachineComponent.SupplyExternalPower(10000);
            GameUpdater.UpdateOneTick();
            Assert.AreEqual(ProcessState.Processing, blockMachineComponent.CurrentState);

            // 出力スロット0を満杯にしてから完了tickを跨いで加工を進める
            // Fill output slot 0 to capacity, then push processing across the completion tick
            var inputSlotCount = GetInputSlotCount(blockInventory);
            blockInventory.SetItem(inputSlotCount, itemStackFactory.Create(outputItemId, maxStack));

            var craftTime = DateTime.Now.AddSeconds(recipe.Time);
            while (craftTime.AddSeconds(0.5).CompareTo(DateTime.Now) == 1)
            {
                blockMachineComponent.SupplyExternalPower(10000);
                GameUpdater.UpdateOneTick();
            }

            // 出力先が空かない限り完了は保留され続け、満杯スロットの中身も変わらない(実現出力の消失なし)
            // Completion stays held while the output has no room; the full slot's contents are untouched (no realized output is lost)
            Assert.AreEqual(ProcessState.Processing, blockMachineComponent.CurrentState);
            Assert.AreEqual(outputItemId, blockInventory.GetItem(inputSlotCount).Id);
            Assert.AreEqual(maxStack, blockInventory.GetItem(inputSlotCount).Count);

            // 出力スロットを空けると、保留していた実現出力がそのまま払い出されて完了する
            // Freeing the output slot pays out the held realized output and completes
            blockInventory.SetItem(inputSlotCount, itemStackFactory.CreatEmpty());
            blockMachineComponent.SupplyExternalPower(10000);
            GameUpdater.UpdateOneTick();

            Assert.AreEqual(ProcessState.Idle, blockMachineComponent.CurrentState);
            Assert.AreEqual(outputItemId, blockInventory.GetItem(inputSlotCount).Id);
            Assert.AreEqual(recipe.OutputItems[0].Count, blockInventory.GetItem(inputSlotCount).Count);
        }

        private int GetInputSlotCount(VanillaMachineBlockInventoryComponent vanillaMachineInventory)
        {
            var vanillaMachineInputInventory = (VanillaMachineInputInventory)typeof(VanillaMachineBlockInventoryComponent)
                .GetField("_vanillaMachineInputInventory", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(vanillaMachineInventory);
            return vanillaMachineInputInventory.InputSlot.Count;
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
