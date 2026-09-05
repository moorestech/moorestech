using System.Collections.Generic;
using System.Linq;
using Core.Master;
using Game.UnlockState;
using Mooresmaster.Model.CraftRecipesModule;
using Mooresmaster.Model.ItemsModule;
using Mooresmaster.Model.MachineRecipesModule;

namespace Client.Game.InGame.UI.Inventory.RecipeViewer
{
    /// <summary>
    /// アイテムID → 全てのレシピ を管理するためのクラス
    /// </summary>
    public class  ItemRecipeViewerDataContainer
    {
        private readonly Dictionary<ItemId, RecipeViewerItemRecipes> _recipeViewerElements = new();
        private readonly IGameUnlockStateData _gameUnlockStateData;

        public RecipeViewerItemRecipes GetItem(ItemId itemId)
        {
            return _recipeViewerElements.GetValueOrDefault(itemId);
        }

        public RecipeViewerItemVisibility EvaluateVisibility(ItemId itemId, ItemMasterElement itemMaster)
        {
            // レシピビューアの表示可否を決める SSOT。未知タイプは呼び出し側が処理する
            // SSOT that decides recipe-viewer visibility; unknown types are handled by the caller
            switch (itemMaster.RecipeViewType)
            {
                case ItemMasterElement.RecipeViewTypeConst.Default:
                    // アンロック済みかつクラフト/機械レシピがあれば表示する
                    // Show when unlocked and a craft or machine recipe exists
                    if (!IsItemUnlocked(itemId)) return RecipeViewerItemVisibility.Hide;
                    var defaultRecipes = GetItem(itemId);
                    if (defaultRecipes == null) return RecipeViewerItemVisibility.Hide;
                    var hasCraft = defaultRecipes.UnlockedCraftRecipes().Count != 0;
                    var hasMachine = defaultRecipes.UnlockedMachineRecipes().Count != 0;
                    return hasCraft || hasMachine ? RecipeViewerItemVisibility.Show : RecipeViewerItemVisibility.Hide;
                case ItemMasterElement.RecipeViewTypeConst.IsUnlocked:
                    return IsItemUnlocked(itemId) ? RecipeViewerItemVisibility.Show : RecipeViewerItemVisibility.Hide;
                case ItemMasterElement.RecipeViewTypeConst.IsCraftRecipeExist:
                    var craftRecipes = GetItem(itemId);
                    if (craftRecipes == null) return RecipeViewerItemVisibility.Hide;
                    return craftRecipes.UnlockedCraftRecipes().Count != 0 ? RecipeViewerItemVisibility.Show : RecipeViewerItemVisibility.Hide;
                case ItemMasterElement.RecipeViewTypeConst.ForceHide:
                    return RecipeViewerItemVisibility.Hide;
                case ItemMasterElement.RecipeViewTypeConst.ForceShow:
                    return RecipeViewerItemVisibility.Show;
                default:
                    return RecipeViewerItemVisibility.UnknownType;
            }

            #region Internal

            bool IsItemUnlocked(ItemId id)
            {
                // dict に無いアイテムはロック扱いに倒して例外を避ける
                // Treat items missing from the dict as locked to avoid exceptions
                if (!_gameUnlockStateData.ItemUnlockStateInfos.TryGetValue(id, out var state)) return false;
                return state.IsUnlocked;
            }

            #endregion
        }

        public ItemRecipeViewerDataContainer(IGameUnlockStateData gameUnlockStateData)
        {
            _gameUnlockStateData = gameUnlockStateData;

            // そのアイテムを作成するための機械のレシピを取得
            // Get the recipe of the machine to create the item
            var machineRecipeDictionary = new Dictionary<ItemId, List<MachineRecipeMasterElement>>();
            foreach (var machineRecipeMaster in MasterHolder.MachineRecipesMaster.MachineRecipes.Data)
            {
                foreach (var outputItem in machineRecipeMaster.OutputItems)
                {
                    var itemId = MasterHolder.ItemMaster.GetItemId(outputItem.ItemGuid);
                    if (!machineRecipeDictionary.ContainsKey(itemId))
                    {
                        machineRecipeDictionary.Add(itemId, new List<MachineRecipeMasterElement>());
                    }
                    
                    machineRecipeDictionary[itemId].Add(machineRecipeMaster);
                }
            }
            
            // そのアイテムを作成するためのクラフトレシピを取得
            // Get the craft recipe to create the item
            foreach (var itemId in MasterHolder.ItemMaster.GetItemAllIds())
            {
                var resultCraftRecipes = MasterHolder.CraftRecipeMaster.GetResultItemCraftRecipes(itemId).ToList();
                
                // そのアイテムを作成するための機械のレシピを機械ごとに作成
                // Create a machine recipe for each machine to create the item
                var resultMachineRecipes = new Dictionary<BlockId, List<MachineRecipeMasterElement>>();
                if (machineRecipeDictionary.TryGetValue(itemId, out var machineRecipesList))
                {
                    foreach (var machineRecipe in machineRecipesList)
                    {
                        var blockId = MasterHolder.BlockMaster.GetBlockId(machineRecipe.BlockGuid);
                        if (resultMachineRecipes.ContainsKey(blockId))
                        {
                            resultMachineRecipes[blockId].Add(machineRecipe);
                        }
                        else
                        {
                            resultMachineRecipes.Add(blockId, new List<MachineRecipeMasterElement> { machineRecipe });
                        }
                    }
                }
                
                _recipeViewerElements.Add(itemId, new RecipeViewerItemRecipes(resultCraftRecipes, resultMachineRecipes, itemId, gameUnlockStateData));
            }
            
            // レシピが存在しないアイテムを除外する
            // Exclude items with no recipes
            var removeList = new List<ItemId>();
            foreach (var kv in _recipeViewerElements)
            {
                var itemId = kv.Key;
                var recipe = kv.Value;
                
                if (recipe.AllCraftRecipes.Count == 0 && recipe.MachineRecipes.Count == 0)
                {
                    removeList.Add(itemId);
                }
            }
            foreach (var itemId in removeList)
            {
                _recipeViewerElements.Remove(itemId);
            }
        }
    }
    
    public class RecipeViewerItemRecipes
    {
        public readonly ItemId ResultItemId;
        
        //TODO 他のmodの他のレシピにも対応できるようの柔軟性をもたせた設計を考える
        public readonly Dictionary<BlockId, List<MachineRecipeMasterElement>> MachineRecipes;
        public readonly List<CraftRecipeMasterElement> AllCraftRecipes;
        
        private readonly IGameUnlockStateData _gameUnlockStateData;
        
        public RecipeViewerItemRecipes(List<CraftRecipeMasterElement> craftRecipes, Dictionary<BlockId, List<MachineRecipeMasterElement>> machineRecipes, ItemId resultItemId, IGameUnlockStateData gameUnlockStateData)
        {
            AllCraftRecipes = craftRecipes;
            MachineRecipes = machineRecipes;
            ResultItemId = resultItemId;
            _gameUnlockStateData = gameUnlockStateData;
        }
        
        /// <summary>
        /// アンロック済みのクラフトレシピを取得
        /// Get unlocked craft recipes
        /// </summary>
        public List<CraftRecipeMasterElement> UnlockedCraftRecipes()
        {
            var infos = _gameUnlockStateData.CraftRecipeUnlockStateInfos;
            return AllCraftRecipes.Where(c => infos[c.CraftRecipeGuid].IsUnlocked).ToList();
        }

        /// <summary>
        /// アンロック済みの機械レシピを機械ごとに取得
        /// Get unlocked machine recipes grouped by block
        /// </summary>
        public Dictionary<BlockId, List<MachineRecipeMasterElement>> UnlockedMachineRecipes()
        {
            var infos = _gameUnlockStateData.MachineRecipeUnlockStateInfos;
            var result = new Dictionary<BlockId, List<MachineRecipeMasterElement>>();
            foreach (var kv in MachineRecipes)
            {
                var blockId = kv.Key;
                var unlockedRecipes = kv.Value
                    .Where(m => infos[m.MachineRecipeGuid].IsUnlocked)
                    .ToList();
                if (0 < unlockedRecipes.Count)
                {
                    result.Add(blockId, unlockedRecipes);
                }
            }
            return result;
        }
    }

    /// <summary>
    /// レシピビューア表示判定の結果。UnknownType は呼び出し側でフォールバック
    /// Result of the recipe-viewer visibility check; UnknownType is a caller-side fallback
    /// </summary>
    public enum RecipeViewerItemVisibility
    {
        Show,
        Hide,
        UnknownType,
    }
}
