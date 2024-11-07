using System;
using System.Collections.Generic;
using System.Linq;
using Core.Master;
using UnitGenerator;

public class CraftingSolver
{
    public static Dictionary<RecipeId, int> Solve(List<Recipe> recipes, Dictionary<ItemId, int> initialInventory, ItemId targetItemName,int targetQuantity)
    {
        Dictionary<ItemId, List<Recipe>> itemsProducedByRecipe = new Dictionary<ItemId, List<Recipe>>();
        foreach (var recipe in recipes)
        {
            foreach (var output in recipe.Outputs)
            {
                if (!itemsProducedByRecipe.ContainsKey(output.ItemId))
                    itemsProducedByRecipe[output.ItemId] = new List<Recipe>();
                itemsProducedByRecipe[output.ItemId].Add(recipe);
            }
        }
        
        
        
        // BFS Initialization
        var initialState = new State
        {
            Inventory = new Dictionary<ItemId, int>(initialInventory),
            RecipesUsed = new Dictionary<RecipeId, int>(),
            MaterialUsed = 0
        };
        if (!initialState.Inventory.ContainsKey(targetItemName))
            initialState.Inventory[targetItemName] = 0;
        initialState.Inventory[targetItemName] -= targetQuantity; // Negative quantity indicates need

        var queue = new Queue<State>();
        queue.Enqueue(initialState);

        var visited = new HashSet<string>();
        State bestState = null;

        while (queue.Count > 0)
        {
            var state = queue.Dequeue();

            var stateKey = GetStateKey(state);
            if (visited.Contains(stateKey))
                continue;
            visited.Add(stateKey);

            // Check if all needed items are satisfied
            if (state.Inventory.All(kvp => kvp.Value >= 0))
            {
                // Found a valid solution
                if (bestState == null || state.MaterialUsed < bestState.MaterialUsed)
                {
                    bestState = state;
                }
                continue;
            }

            // Find an item that is needed (negative quantity)
            var neededItemKvp = state.Inventory.FirstOrDefault(kvp => kvp.Value < 0);
            if (neededItemKvp.Key == null)
                continue;

            ItemId itemId = neededItemKvp.Key;
            int quantityNeeded = -neededItemKvp.Value;

            // Try to fulfill the need from inventory
            if (state.Inventory.ContainsKey(itemId) && state.Inventory[itemId] > 0)
            {
                int available = state.Inventory[itemId];
                int used = Math.Min(available, quantityNeeded);
                var newState = CloneState(state);
                newState.Inventory[itemId] -= used; // Consume inventory
                newState.Inventory[itemId] += quantityNeeded; // Need fulfilled
                queue.Enqueue(newState);
                continue;
            }

            // Try combinations of recipes that produce the needed item
            if (itemsProducedByRecipe.ContainsKey(itemId))
            {
                var producingRecipes = itemsProducedByRecipe[itemId];

                // Calculate max runs for each recipe
                var maxRunsList = new List<int>();
                foreach (var recipe in producingRecipes)
                {
                    int maxRuns = 10; // Set an upper limit to prevent infinite loops
                    maxRunsList.Add(maxRuns);
                }

                // Generate combinations of runs
                var combinations = GenerateCombinations(producingRecipes, maxRunsList, quantityNeeded, state, itemId);

                foreach (var combination in combinations)
                {
                    // For each combination, create new state
                    var newState = CloneState(state);
                    bool canProceed = true;

                    // For each recipe and number of runs
                    for (int i = 0; i < producingRecipes.Count; i++)
                    {
                        var recipe = producingRecipes[i];
                        int timesToRun = combination[i];

                        if (timesToRun == 0)
                            continue;

                        // Update recipes used
                        if (!newState.RecipesUsed.ContainsKey(recipe.RecipeId))
                            newState.RecipesUsed[recipe.RecipeId] = 0;
                        newState.RecipesUsed[recipe.RecipeId] += timesToRun;

                        // Update inventory with outputs
                        foreach (var output in recipe.Outputs)
                        {
                            int produced = output.Quantity * timesToRun;
                            if (!newState.Inventory.ContainsKey(output.ItemId))
                                newState.Inventory[output.ItemId] = 0;
                            newState.Inventory[output.ItemId] += produced;
                        }

                        // Consume ingredients
                        foreach (var ingredient in recipe.Inputs)
                        {
                            int required = ingredient.Quantity * timesToRun;
                            if (!newState.Inventory.ContainsKey(ingredient.ItemId))
                                newState.Inventory[ingredient.ItemId] = 0;
                            newState.Inventory[ingredient.ItemId] -= required; // Negative indicates need
                        }
                    }

                    // Check for excessive negative inventory (to prevent infinite loops)
                    foreach (var kvp in newState.Inventory)
                    {
                        if (kvp.Value < -1000)
                        {
                            canProceed = false;
                            break;
                        }
                    }

                    if (!canProceed)
                        continue;

                    queue.Enqueue(newState);
                }
            }
        }

        if (bestState == null)
        {
            return null;
        }
        else
        {
            return bestState.RecipesUsed;
        }
    }

    private static List<int[]> GenerateCombinations(List<Recipe> recipes, List<int> maxRunsList, int quantityNeeded, State state, ItemId itemId)
    {
        var combinations = new List<int[]>();
        int n = recipes.Count;
        int[] currentRuns = new int[n];
        GenerateCombinationsRecursive(recipes, maxRunsList, quantityNeeded, 0, currentRuns, combinations, state, itemId);
        return combinations;
    }

    private static void GenerateCombinationsRecursive(List<Recipe> recipes, List<int> maxRunsList, int quantityNeeded, int index, int[] currentRuns, List<int[]> combinations, State state, ItemId itemId)
    {
        if (index == recipes.Count)
        {
            // Calculate total output
            int totalProduced = state.Inventory.ContainsKey(itemId) && state.Inventory[itemId] > 0 ? state.Inventory[itemId] : 0;
            for (int i = 0; i < recipes.Count; i++)
            {
                var recipe = recipes[i];
                int timesToRun = currentRuns[i];
                var outputItem = recipe.Outputs.FirstOrDefault(o => o.ItemId == itemId);
                if (outputItem != null)
                {
                    totalProduced += timesToRun * outputItem.Quantity;
                }
            }

            if (totalProduced >= -state.Inventory[itemId]) // We need to produce at least the needed quantity
            {
                combinations.Add((int[])currentRuns.Clone());
            }
            return;
        }

        for (int run = 0; run <= maxRunsList[index]; run++)
        {
            currentRuns[index] = run;
            GenerateCombinationsRecursive(recipes, maxRunsList, quantityNeeded, index + 1, currentRuns, combinations, state, itemId);
        }
    }

    private static string GetStateKey(State state)
    {
        var inventoryPart = string.Join(",", state.Inventory.OrderBy(kvp => kvp.Key).Select(kvp => $"{kvp.Key}:{kvp.Value}"));
        var recipesPart = string.Join(",", state.RecipesUsed.OrderBy(kvp => kvp.Key).Select(kvp => $"{kvp.Key}:{kvp.Value}"));
        return $"{inventoryPart}|{recipesPart}|{state.MaterialUsed}";
    }

    private static State CloneState(State state)
    {
        return new State
        {
            Inventory = new Dictionary<ItemId, int>(state.Inventory),
            RecipesUsed = new Dictionary<RecipeId, int>(state.RecipesUsed),
            MaterialUsed = state.MaterialUsed
        };
    }
}

public class InputItem
{
    public ItemId ItemId;
    public int Quantity;
    
    public InputItem(ItemId itemId, int quantity)
    {
        ItemId = itemId;
        Quantity = quantity;
    }
}

public class OutputItem
{
    public ItemId ItemId;
    public int Quantity;
    
    public OutputItem(ItemId itemId, int quantity)
    {
        ItemId = itemId;
        Quantity = quantity;
    }
}

public class Recipe
{
    public RecipeId RecipeId;
    public List<InputItem> Inputs;
    public List<OutputItem> Outputs;
    
    public Recipe(RecipeId recipeId, List<InputItem> inputs, List<OutputItem> outputs)
    {
        RecipeId = recipeId;
        Inputs = inputs;
        Outputs = outputs;
    }
}

[UnitOf(typeof(int))]
public partial struct RecipeId { }

public class State
{
    public int MaterialUsed;
    public Dictionary<ItemId, int> Inventory; // Negative values indicate needs
    public Dictionary<RecipeId, int> RecipesUsed;
}
