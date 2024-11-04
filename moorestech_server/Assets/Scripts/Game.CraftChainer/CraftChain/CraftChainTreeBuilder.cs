using System.Collections.Generic;
using Core.Master;
using Game.CraftChainer.Util;
using UnityEngine;

namespace Game.CraftChainer
{
    public class CraftChainTreeBuilder
    {
        private readonly ItemRecipeViewerDataContainer _itemRecipeViewerDataContainer;

        public CraftChainTreeBuilder(ItemRecipeViewerDataContainer itemRecipeViewerDataContainer)
        {
            _itemRecipeViewerDataContainer = itemRecipeViewerDataContainer;
        }

        public List<ICraftChainTreeNode> BuildCraftChainTree(CraftChainItem targetCraftChainItem)
        {
            // TODO ループ検知
            // TODO 原材料の場合はそれも検知させる
            
            var result = new List<ICraftChainTreeNode>();
            
            // クラフトのレシピの取得
            var recipes = _itemRecipeViewerDataContainer.GetItem(targetCraftChainItem.ItemId);
            foreach (var craftRecipe in recipes.CraftRecipes)
            {
                var requireCraftChainItems = craftRecipe.ToRequireCraftChainItems(targetCraftChainItem);
                var requireItems = new Dictionary<CraftChainItem, ICraftChainTreeNode>();
                foreach (var craftChain in requireCraftChainItems)
                {
                    var childNodes = BuildCraftChainTree(craftChain);
                    foreach (var childNode in childNodes)
                    {
                        requireItems.Add(craftChain, childNode);
                    }
                }
                
                var results = craftRecipe.ToResultCraftChainItems(targetCraftChainItem);
                result.Add(new CraftChainTreeNode(ICraftChainTreeNode.CraftRecipe, results, requireItems));
            }
            
            // 機械加工のレシピの取得
            foreach (var machineRecipe in recipes.MachineRecipes)
            {
                foreach (var recipeMaster in machineRecipe.Value)
                {
                    // 目的のアイテムの製造個数をチェックする
                    var requireCraftChainItems = recipeMaster.ToRequireMachineChainItems(targetCraftChainItem);
                    var requireItems = new Dictionary<CraftChainItem, ICraftChainTreeNode>();
                    foreach (var craftChain in requireCraftChainItems)
                    {
                        var childNodes = BuildCraftChainTree(craftChain);
                        foreach (var childNode in childNodes)
                        {
                            requireItems.Add(craftChain, childNode);
                        }
                    }
                    
                    var resultItem = recipeMaster.ToResultMachineChainItems(targetCraftChainItem);
                    result.Add(new CraftChainTreeNode(ICraftChainTreeNode.MachineRecipe, resultItem, requireItems));
                }
            }
            
            return result;
        }
    }
}
