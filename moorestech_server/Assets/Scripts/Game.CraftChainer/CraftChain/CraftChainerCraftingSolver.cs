using System;
using System.Collections.Generic;
using System.Linq;
using Core.Master;

namespace Game.CraftChainer.CraftChain
{
    public static class CraftChainerCraftingSolver
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
            var initialState = InitializeState(initialInventory, targetItem.ItemId, targetItem.Count);
            
            // Step 3: Prepare BFS structures
            // ステップ3：BFSの構造を準備する
            var (queue, visitedStates, bestState) = InitializeBFS(initialState);
            
            // Step 4: Perform BFS to find the optimal crafting solution
            // ステップ4：最適なクラフト解を見つけるためにBFSを実行する
            while (queue.Count > 0)
            {
                // Dequeue the next state from the queue to explore
                // 探索するためにキューから次の状態を取り出す
                var currentState = queue.Dequeue();
                
                // If the current state has already been visited, skip processing it
                // 現在の状態がすでに訪問済みであれば、処理をスキップする
                if (IsStateVisited(currentState, visitedStates))
                    continue;
                
                // Mark the current state as visited to avoid revisiting it
                // 再度訪問しないよう、現在の状態を訪問済みとしてマークする
                MarkStateAsVisited(currentState, visitedStates);
                
                // If the goal state is reached, update the best solution and continue
                // 目標状態に到達した場合、最適な解を更新して次の反復へ進む
                if (IsGoalState(currentState))
                {
                    bestState = UpdateBestState(currentState, bestState);
                    continue;
                }
                
                // Find an item that is still needed; if none are found, continue
                // まだ必要とされているアイテムを探す。見つからなければ次へ進む
                var neededItem = FindNeededItem(currentState);
                if (neededItem == null)
                    continue;
                
                // Try to fulfill the needed item from existing inventory; if successful, continue
                // 既存の在庫から必要なアイテムを満たせるか試みる。成功したら次へ進む
                if (TryFulfillNeedFromInventory(currentState, neededItem, queue))
                    continue;
                
                // Expand the current state by applying recipes to produce the needed item
                // 必要なアイテムを生産するレシピを適用して、現在の状態を展開する
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
            
            if (itemsProducedByRecipe.TryGetValue(itemId, out var producingRecipes))
            {
                var maxRunsList = producingRecipes.Select(_ => 10).ToList(); // Limit runs to prevent infinite loops
                
                var combinations = GenerateRecipeCombinations(producingRecipes, maxRunsList, state, itemId);
                
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
                            totalProduced += currentRuns[i] * output.Count;
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
                    int produced = output.Count * runs;
                    if (!newState.Inventory.ContainsKey(output.ItemId))
                        newState.Inventory[output.ItemId] = 0;
                    newState.Inventory[output.ItemId] += produced;
                }
                
                foreach (var input in recipe.Inputs)
                {
                    int required = input.Count * runs;
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
    
    public class CraftingSolverState
    {
        public int MaterialUsed;
        public Dictionary<ItemId, int> Inventory; // Negative values indicate needs
        public Dictionary<CraftingSolverRecipeId, int> RecipesUsed;
    }
}