using System;
using System.Collections.Generic;
using System.Linq;
using Core.Master;
using UnitGenerator;

namespace Game.CraftChainer.CraftChain
{
    public class CraftingSolver
    {
        public static Dictionary<CraftingSolverRecipeId, int> Solve(
            List<CraftingSolverRecipe> recipes,
            Dictionary<ItemId, int> initialInventory,
            CraftingSolverItem targetItem)
        {
            // Step 1: Build a mapping from items to the recipes that produce them
            // ステップ1：アイテムからそれを生産するレシピへのマッピングを構築する
            var itemsProducedByRecipe = BuildItemsProducedByRecipe(recipes);
            
            // Step 2: Initialize the initial state for BFS
            // ステップ2：BFSのための初期状態を初期化する
            var initialState = InitializeState(initialInventory, targetItem.ItemId, targetItem.Quantity);
            
            // Step 3: Prepare BFS structures
            // ステップ3：BFSの構造を準備する
            var (queue, visitedStates, bestState) = InitializeBFS(initialState);
            
            // Step 4: Perform BFS to find the optimal crafting solution
            // ステップ4：最適なクラフト解を見つけるためにBFSを実行する
            while (queue.Count > 0)
            {
                var currentState = queue.Dequeue();
                
                if (IsStateVisited(currentState, visitedStates))
                    continue;
                
                MarkStateAsVisited(currentState, visitedStates);
                
                if (IsGoalState(currentState))
                {
                    bestState = UpdateBestState(currentState, bestState);
                    continue;
                }
                
                var neededItem = FindNeededItem(currentState);
                if (neededItem == null)
                    continue;
                
                if (TryFulfillNeedFromInventory(currentState, neededItem, queue))
                    continue;
                
                ExpandState(currentState, neededItem, itemsProducedByRecipe, queue);
            }
            
            // Step 5: Return the best solution found
            // ステップ5：見つかった最適な解を返す
            return bestState?.RecipesUsed;
        }
        
        private static Dictionary<ItemId, List<CraftingSolverRecipe>> BuildItemsProducedByRecipe(List<CraftingSolverRecipe> recipes)
        {
            var itemsProduced = new Dictionary<ItemId, List<CraftingSolverRecipe>>();
            foreach (var recipe in recipes)
            {
                foreach (var output in recipe.Outputs)
                {
                    if (!itemsProduced.ContainsKey(output.ItemId))
                        itemsProduced[output.ItemId] = new List<CraftingSolverRecipe>();
                    itemsProduced[output.ItemId].Add(recipe);
                }
            }
            return itemsProduced;
        }
        
        private static CraftingSolverState InitializeState(Dictionary<ItemId, int> inventory, ItemId targetItem, int targetQty)
        {
            var state = new CraftingSolverState
            {
                Inventory = new Dictionary<ItemId, int>(inventory),
                RecipesUsed = new Dictionary<CraftingSolverRecipeId, int>(),
                MaterialUsed = 0
            };
            
            if (!state.Inventory.ContainsKey(targetItem))
                state.Inventory[targetItem] = 0;
            state.Inventory[targetItem] -= targetQty; // Negative quantity indicates a need
            
            return state;
        }
        
        private static (Queue<CraftingSolverState>, HashSet<string>, CraftingSolverState) InitializeBFS(CraftingSolverState initialState)
        {
            var queue = new Queue<CraftingSolverState>();
            queue.Enqueue(initialState);
            
            var visitedStates = new HashSet<string>();
            CraftingSolverState bestState = null;
            
            return (queue, visitedStates, bestState);
        }
        
        private static bool IsStateVisited(CraftingSolverState state, HashSet<string> visitedStates)
        {
            var key = GenerateStateKey(state);
            return visitedStates.Contains(key);
        }
        
        private static void MarkStateAsVisited(CraftingSolverState state, HashSet<string> visitedStates)
        {
            var key = GenerateStateKey(state);
            visitedStates.Add(key);
        }
        
        private static bool IsGoalState(CraftingSolverState state)
        {
            return state.Inventory.Values.All(quantity => quantity >= 0);
        }
        
        private static CraftingSolverState UpdateBestState(CraftingSolverState currentState, CraftingSolverState bestState)
        {
            if (bestState == null || currentState.MaterialUsed < bestState.MaterialUsed)
                return currentState;
            return bestState;
        }
        
        private static KeyValuePair<ItemId, int>? FindNeededItem(CraftingSolverState state)
        {
            foreach (var kvp in state.Inventory)
            {
                if (kvp.Value < 0)
                    return kvp;
            }
            return null;
        }
        
        private static bool TryFulfillNeedFromInventory(CraftingSolverState state, KeyValuePair<ItemId, int>? neededItem, Queue<CraftingSolverState> queue)
        {
            var itemId = neededItem.Value.Key;
            int quantityNeeded = -neededItem.Value.Value;
            
            if (state.Inventory.TryGetValue(itemId, out int available) && available > 0)
            {
                int used = Math.Min(available, quantityNeeded);
                var newState = CloneState(state);
                newState.Inventory[itemId] -= used; // Consume from inventory
                newState.Inventory[itemId] += quantityNeeded; // Fulfill the need
                queue.Enqueue(newState);
                return true;
            }
            return false;
        }
        
        private static void ExpandState(
            CraftingSolverState state,
            KeyValuePair<ItemId, int>? neededItem,
            Dictionary<ItemId, List<CraftingSolverRecipe>> itemsProducedByRecipe,
            Queue<CraftingSolverState> queue)
        {
            var itemId = neededItem.Value.Key;
            int quantityNeeded = -neededItem.Value.Value;
            
            if (itemsProducedByRecipe.TryGetValue(itemId, out var producingRecipes))
            {
                var maxRunsList = producingRecipes.Select(_ => 10).ToList(); // Limit runs to prevent infinite loops
                
                var combinations = GenerateRecipeCombinations(producingRecipes, maxRunsList, quantityNeeded, state, itemId);
                
                foreach (var combination in combinations)
                {
                    var newState = ApplyRecipeCombination(state, producingRecipes, combination);
                    if (IsStateValid(newState))
                        queue.Enqueue(newState);
                }
            }
        }
        
    #region Internal
        
        private static List<int[]> GenerateRecipeCombinations(
            List<CraftingSolverRecipe> recipes,
            List<int> maxRunsList,
            int quantityNeeded,
            CraftingSolverState state,
            ItemId itemId)
        {
            var combinations = new List<int[]>();
            int recipeCount = recipes.Count;
            int[] currentRuns = new int[recipeCount];
            
            void RecursiveGenerate(int index)
            {
                if (index == recipeCount)
                {
                    int totalProduced = state.Inventory.TryGetValue(itemId, out int existing) && existing > 0 ? existing : 0;
                    
                    for (int i = 0; i < recipeCount; i++)
                    {
                        var output = recipes[i].Outputs.FirstOrDefault(o => o.ItemId == itemId);
                        if (output != null)
                            totalProduced += currentRuns[i] * output.Quantity;
                    }
                    
                    if (totalProduced >= -state.Inventory[itemId])
                        combinations.Add((int[])currentRuns.Clone());
                    return;
                }
                
                for (int run = 0; run <= maxRunsList[index]; run++)
                {
                    currentRuns[index] = run;
                    RecursiveGenerate(index + 1);
                }
            }
            
            RecursiveGenerate(0);
            return combinations;
        }
        
        private static CraftingSolverState ApplyRecipeCombination(
            CraftingSolverState state,
            List<CraftingSolverRecipe> recipes,
            int[] combination)
        {
            var newState = CloneState(state);
            
            for (int i = 0; i < recipes.Count; i++)
            {
                int runs = combination[i];
                if (runs == 0) continue;
                
                var recipe = recipes[i];
                if (!newState.RecipesUsed.ContainsKey(recipe.CraftingSolverRecipeId))
                    newState.RecipesUsed[recipe.CraftingSolverRecipeId] = 0;
                newState.RecipesUsed[recipe.CraftingSolverRecipeId] += runs;
                
                foreach (var output in recipe.Outputs)
                {
                    int produced = output.Quantity * runs;
                    if (!newState.Inventory.ContainsKey(output.ItemId))
                        newState.Inventory[output.ItemId] = 0;
                    newState.Inventory[output.ItemId] += produced;
                }
                
                foreach (var input in recipe.Inputs)
                {
                    int required = input.Quantity * runs;
                    if (!newState.Inventory.ContainsKey(input.ItemId))
                        newState.Inventory[input.ItemId] = 0;
                    newState.Inventory[input.ItemId] -= required; // Negative indicates a need
                }
            }
            
            return newState;
        }
        
        private static bool IsStateValid(CraftingSolverState state)
        {
            return state.Inventory.Values.All(quantity => quantity >= -1000);
        }
        
        private static string GenerateStateKey(CraftingSolverState state)
        {
            var inventoryKey = string.Join(",", state.Inventory.OrderBy(kvp => kvp.Key).Select(kvp => $"{kvp.Key}:{kvp.Value}"));
            var recipesKey = string.Join(",", state.RecipesUsed.OrderBy(kvp => kvp.Key).Select(kvp => $"{kvp.Key}:{kvp.Value}"));
            return $"{inventoryKey}|{recipesKey}|{state.MaterialUsed}";
        }
        
        private static CraftingSolverState CloneState(CraftingSolverState state)
        {
            return new CraftingSolverState
            {
                Inventory = new Dictionary<ItemId, int>(state.Inventory),
                RecipesUsed = new Dictionary<CraftingSolverRecipeId, int>(state.RecipesUsed),
                MaterialUsed = state.MaterialUsed
            };
        }
        
    #endregion
    }
    
    public class CraftingSolverItem
    {
        public readonly ItemId ItemId;
        public readonly int Quantity;
        
        public CraftingSolverItem(ItemId itemId, int quantity)
        {
            ItemId = itemId;
            Quantity = quantity;
        }
    }
    
    public class CraftingSolverRecipe
    {
        public readonly CraftingSolverRecipeId CraftingSolverRecipeId;
        public readonly List<CraftingSolverItem> Inputs;
        public readonly List<CraftingSolverItem> Outputs;
        
        public CraftingSolverRecipe(CraftingSolverRecipeId craftingSolverRecipeId, List<CraftingSolverItem> inputs, List<CraftingSolverItem> outputs)
        {
            CraftingSolverRecipeId = craftingSolverRecipeId;
            Inputs = inputs;
            Outputs = outputs;
        }
    }
    
    [UnitOf(typeof(int), UnitGenerateOptions.Comparable)]
    public partial struct CraftingSolverRecipeId { }
    
    public class CraftingSolverState
    {
        public int MaterialUsed;
        public Dictionary<ItemId, int> Inventory; // Negative values indicate needs
        public Dictionary<CraftingSolverRecipeId, int> RecipesUsed;
    }
}