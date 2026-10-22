using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Client.UitkPoc.RecipeGraph.Model
{
    // 目標資源から選択レシピをたどり、全消費先の需要を合算して台数を出す
    // Walks chosen recipes from the target and merges demand across consumers to get machine counts
    public static class RecipeChainSolver
    {
        public static ProductionGraph Solve(RecipeBook book, string targetGuid, float targetPerMinute, Dictionary<string, int> recipeChoice)
        {
            // 依存の後順を作り、逆順（目標→原料）で需要を流す
            // Build dependency post-order, then push demand in reverse (target to raw)
            var postOrder = new List<string>();
            var visiting = new HashSet<string>();
            var visited = new HashSet<string>();
            Visit(targetGuid);

            var demand = new Dictionary<string, float> { [targetGuid] = targetPerMinute };
            var flows = new List<(string from, string to, float rate)>();
            var graph = new ProductionGraph();
            var nodeIndex = new Dictionary<string, int>();
            for (var i = postOrder.Count - 1; i >= 0; i--) AddNode(postOrder[i]);

            // 合算した流量を辺へ畳む
            // Fold merged flows into edges
            var edgeIndex = new Dictionary<(int, int), ProductionEdge>();
            foreach (var (from, to, rate) in flows)
            {
                var key = (nodeIndex[from], nodeIndex[to]);
                if (!edgeIndex.TryGetValue(key, out var edge))
                {
                    edge = new ProductionEdge(key.Item1, key.Item2, book.ResourceNames[from], book.FluidGuids.Contains(from));
                    edgeIndex[key] = edge;
                    graph.Edges.Add(edge);
                }
                edge.AddRate(rate);
            }
            return graph;

            #region Internal

            RecipeEntry ChosenRecipe(string guid)
            {
                if (!book.RecipesByOutput.TryGetValue(guid, out var recipes)) return null;
                return recipes[recipeChoice.TryGetValue(guid, out var index) ? index : book.DefaultRecipeIndex(guid)];
            }

            void Visit(string guid)
            {
                if (visited.Contains(guid)) return;
                // 循環レシピは辿らずログに残す（PoCでは原料扱いに縮退）
                // Cyclic recipes are logged and degraded to raw for the PoC
                if (!visiting.Add(guid))
                {
                    Debug.LogWarning($"[UitkPoc] レシピ循環を検出: {book.ResourceNames[guid]}");
                    return;
                }
                var recipe = ChosenRecipe(guid);
                if (recipe != null) foreach (var input in recipe.Inputs) Visit(input.ResourceGuid);
                visiting.Remove(guid);
                visited.Add(guid);
                postOrder.Add(guid);
            }

            void AddNode(string guid)
            {
                var rate = demand.TryGetValue(guid, out var d) ? d : 0f;
                var recipe = ChosenRecipe(guid);
                var isFluid = book.FluidGuids.Contains(guid);
                nodeIndex[guid] = graph.Nodes.Count;
                if (recipe == null)
                {
                    var miner = book.MinersByResource.TryGetValue(guid, out var m) ? m : null;
                    var minerCount = miner == null ? 0f : rate * miner.TimeSeconds / 60f;
                    graph.Nodes.Add(new ProductionNode(guid, book.ResourceNames[guid], miner?.MachineName ?? (isFluid ? "液体源" : "採取"), true, isFluid, 0, rate, minerCount));
                    return;
                }

                // 1台あたり毎分クラフト数から台数を出し、入力へ需要を配る
                // Machine count from crafts per minute, then hand demand to inputs
                var outputAmount = recipe.Outputs.First(o => o.ResourceGuid == guid).Amount;
                var craftsPerMinute = rate / outputAmount;
                var machines = craftsPerMinute * recipe.TimeSeconds / 60f;
                graph.Nodes.Add(new ProductionNode(guid, book.ResourceNames[guid], recipe.MachineName, false, isFluid, book.RecipesByOutput[guid].Count, rate, machines));
                foreach (var input in recipe.Inputs)
                {
                    var inputRate = craftsPerMinute * input.Amount;
                    demand[input.ResourceGuid] = (demand.TryGetValue(input.ResourceGuid, out var current) ? current : 0f) + inputRate;
                    flows.Add((input.ResourceGuid, guid, inputRate));
                }
            }

            #endregion
        }
    }
}
