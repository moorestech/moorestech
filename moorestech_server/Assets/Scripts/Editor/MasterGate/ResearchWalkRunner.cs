using System;
using System.Collections.Generic;
using System.Linq;
using Core.Master;
using Game.Context;
using Game.PlayerIdentity;
using Game.PlayerInventory.Interface;
using Game.Research;
using Game.UnlockState;
using Microsoft.Extensions.DependencyInjection;
using UnityEngine;

namespace Server.MasterGate.Editor
{
    /// <summary>
    ///     G3: 出荷マスタで組んだサーバーで、G2 の研究順に各ノードのコストだけを主インベントリへ渡して CompleteResearch が通り、G2 が使った解放が実際に付くかを確かめる
    ///     G3: on a server built from the shipped master, give each node only its cost in G2's order, and check CompleteResearch succeeds and the unlocks G2 relied on are applied
    /// </summary>
    public static class ResearchWalkRunner
    {
        private const string GateIdentity = "gate:research-walk";

        public static bool Run(ServiceProvider serviceProvider, IReadOnlyList<ResearchWalkStep> steps, out string failure)
        {
            // 歩く前に、研究順がロード済みの全研究を重複なく覆うかを確かめる（完走だけでは全ノードの保証にならない）
            // Before walking, check the order covers all loaded research without duplicates; finishing the walk alone does not prove every node
            var loadedResearchGuids = MasterHolder.ResearchMaster.GetAllResearches().Select(node => node.ResearchNodeGuid).ToList();
            if (!ResearchWalkOrderCoverage.TryCheck(steps, loadedResearchGuids, out failure))
            {
                Debug.LogError(failure);
                return false;
            }

            // サーバーは ShippedMasterOwner が本番と同じ DI 構築で組んだもの。ゲート専用のプレイヤーを1人登録する
            // The server was built by ShippedMasterOwner with the production DI container; register one gate-only player
            var registry = serviceProvider.GetRequiredService<PlayerIdentityRegistry>();
            var assignment = registry.PreviewAssignment(GateIdentity);
            registry.Commit(assignment);
            var playerId = assignment.PlayerId;
            var research = serviceProvider.GetRequiredService<IResearchDataStore>();
            var unlocks = serviceProvider.GetRequiredService<IGameUnlockStateDataController>();
            var inventory = serviceProvider.GetRequiredService<IPlayerInventoryDataStore>().GetInventoryData(playerId).MainOpenableInventory;

            // 最初の失敗で止める（後続は前提が欠けて連鎖的に落ちるだけで、原因の特定を妨げるため）
            // Stop at the first failure; later nodes would only cascade on the missing prerequisite and hide the cause
            failure = null;
            foreach (var step in steps)
            {
                failure = WalkOne(step);
                if (failure != null) break;
            }
            if (failure != null) Debug.LogError(failure);
            else Debug.Log($"[ResearchWalk] PASS {steps.Count} nodes completed in G2 order");
            return failure == null;

            #region Internal

            string WalkOne(ResearchWalkStep step)
            {
                // G2 は前提が揃った段に置いたので、支払い前に前提不足の状態なら Python のモデルと実装の前提判定がずれている
                // G2 placed the node where its prerequisites are done, so a missing-prerequisite state before paying means the Python model and the runtime disagree
                var node = MasterHolder.ResearchMaster.GetResearch(step.ResearchNodeGuid);
                var before = research.GetResearchNodeStates(playerId)[step.ResearchNodeGuid];
                if (before is ResearchNodeState.Completed or ResearchNodeState.UnresearchableNotEnoughPreNode or ResearchNodeState.UnresearchableAllReasons)
                    return Fail(step, $"支払い前の状態が {before}（前提の判定が G2 とずれている）");

                // コストを最大スタックごとに分けて主インベントリへ入れる。入り切らなければ、その時点の枠ではゲームでも払えない
                // Insert each cost split by max stack; if it does not fit, the game cannot pay it at this point either
                foreach (var cost in node.ConsumeItems)
                {
                    var stacks = ServerContext.ItemStackFactory.CreateSplitStacks(MasterHolder.ItemMaster.GetItemId(cost.ItemGuid), cost.ItemCount);
                    var overflow = 0;
                    foreach (var stack in stacks) overflow += inventory.InsertItem(stack).Count;
                    if (0 < overflow) return Fail(step, $"コスト {cost.ItemGuid} x{cost.ItemCount} のうち {overflow} 個が主インベントリに入らない（最大スタックで分けた {stacks.Count} 枠）");
                }

                var after = research.GetResearchNodeStates(playerId)[step.ResearchNodeGuid];
                if (after != ResearchNodeState.Researchable) return Fail(step, $"コストを渡した後の状態が {after}");
                if (!research.CompleteResearch(step.ResearchNodeGuid, playerId)) return Fail(step, "CompleteResearch が false を返した");

                // G2 がこの段で付くと見なした解放（ブロック・機械レシピ）が実際に付いたかを、解放済み GUID の集合 1 つの判定で見る
                // Check the unlocks G2 assumed at this step (blocks, machine recipes) with one membership test against the unlocked GUID sets
                var unlockedBlocks = new HashSet<Guid>(unlocks.BlockUnlockStateInfos.Where(pair => pair.Value.IsUnlocked).Select(pair => pair.Key));
                var unlockedRecipes = new HashSet<Guid>(unlocks.MachineRecipeUnlockStateInfos.Where(pair => pair.Value.IsUnlocked).Select(pair => pair.Key));
                return FirstNotUnlocked(step, "ブロック", step.UnlockBlockGuids, unlockedBlocks)
                       ?? FirstNotUnlocked(step, "機械レシピ", step.UnlockMachineRecipeGuids, unlockedRecipes);
            }

            string FirstNotUnlocked(ResearchWalkStep step, string label, IReadOnlyList<Guid> expected, HashSet<Guid> unlocked)
            {
                foreach (var guid in expected)
                {
                    if (!unlocked.Contains(guid)) return Fail(step, $"{label} {guid} が解放されていない");
                }
                return null;
            }

            string Fail(ResearchWalkStep step, string reason)
            {
                return $"[ResearchWalk] FAIL {step.Name} ({step.ResearchNodeGuid}): {reason}";
            }

            #endregion
        }
    }
}
