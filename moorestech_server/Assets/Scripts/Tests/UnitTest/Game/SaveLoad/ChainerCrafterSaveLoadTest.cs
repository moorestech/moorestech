using System.Collections.Generic;
using Core.Master;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.CraftChainer.BlockComponent.Crafter;
using Game.CraftChainer.CraftChain;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.UnitTest.Game.SaveLoad
{
    public class ChainerCrafterSaveLoadTest
    {
        [Test]
        public void SaveLoadTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);

            var blockFactory = ServerContext.BlockFactory;
            var posInfo = new BlockPositionInfo(new Vector3Int(0, 0, 0), BlockDirection.North, Vector3Int.one);

            // クラフターを作成
            // Create a Crafter block
            var crafterBlock = blockFactory.Create(ForUnitTestModBlockId.CraftChainerCrafter, new BlockInstanceId(1), posInfo);

            // レシピを設定
            // Set the recipe
            var originalCrafter = crafterBlock.GetComponent<ChainerCrafterComponent>();
            var inputItems = new List<CraftingSolverItem>
            {
                new(new ItemId(1), 10),
                new(new ItemId(2), 5)
            };

            var outputItems = new List<CraftingSolverItem>
            {
                new(new ItemId(3), 15)
            };

            originalCrafter.SetRecipe(inputItems, outputItems);
            
            // セーブデータを取得
            // Get the save data
            var saveState = crafterBlock.GetSaveState();
            
            // ブロックをロード
            // Load the block
            var loadedBlock = blockFactory.Load( crafterBlock.BlockGuid, new BlockInstanceId(2), saveState, posInfo);
            var loadedCrafterComponent = loadedBlock.GetComponent<ChainerCrafterComponent>();

            // ノードIDのチェック
            // Check the node ID
            Assert.AreEqual(originalCrafter.NodeId, loadedCrafterComponent.NodeId);

            // レシピの設定をチェック
            // Check the recipe settings
            Assert.AreEqual(originalCrafter.CraftingSolverRecipe.Inputs.Count, loadedCrafterComponent.CraftingSolverRecipe.Inputs.Count);
            Assert.AreEqual(originalCrafter.CraftingSolverRecipe.Outputs.Count, loadedCrafterComponent.CraftingSolverRecipe.Outputs.Count);

            for (int i = 0; i < originalCrafter.CraftingSolverRecipe.Inputs.Count; i++)
            {
                Assert.AreEqual(originalCrafter.CraftingSolverRecipe.Inputs[i].ItemId, loadedCrafterComponent.CraftingSolverRecipe.Inputs[i].ItemId);
                Assert.AreEqual(originalCrafter.CraftingSolverRecipe.Inputs[i].Quantity, loadedCrafterComponent.CraftingSolverRecipe.Inputs[i].Quantity);
            }

            for (int i = 0; i < originalCrafter.CraftingSolverRecipe.Outputs.Count; i++)
            {
                Assert.AreEqual(originalCrafter.CraftingSolverRecipe.Outputs[i].ItemId, loadedCrafterComponent.CraftingSolverRecipe.Outputs[i].ItemId);
                Assert.AreEqual(originalCrafter.CraftingSolverRecipe.Outputs[i].Quantity, loadedCrafterComponent.CraftingSolverRecipe.Outputs[i].Quantity);
            }
        }
    }
}