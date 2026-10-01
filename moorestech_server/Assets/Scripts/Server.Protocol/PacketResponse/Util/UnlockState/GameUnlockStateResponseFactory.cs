using System.Collections.Generic;
using Game.UnlockState;
using static Server.Protocol.PacketResponse.GetGameUnlockStateProtocol;

namespace Server.Protocol.PacketResponse.Util.UnlockState
{
    // アンロック状態の各集合を通信応答へ組み立てる
    // Assemble every unlock-state collection into the wire response
    internal static class GameUnlockStateResponseFactory
    {
        internal static ResponseGameUnlockStateProtocolMessagePack Create(IGameUnlockStateData state)
        {
            // レシピの解放状態を二つの集合へ分ける
            // Split recipe unlock states into locked and unlocked sets
            var lockedCraftRecipe = new List<string>();
            var unlockedCraftRecipe = new List<string>();
            foreach (var craftRecipe in state.CraftRecipeUnlockStateInfos.Values)
            {
                if (craftRecipe.IsUnlocked)
                {
                    unlockedCraftRecipe.Add(craftRecipe.CraftRecipeGuid.ToString());
                }
                else
                {
                    lockedCraftRecipe.Add(craftRecipe.CraftRecipeGuid.ToString());
                }
            }
            
            // アイテムの状態は通信上のIDで集める
            // Gather item states by their wire IDs
            var lockedItem = new List<int>();
            var unlockedItem = new List<int>();
            foreach (var item in state.ItemUnlockStateInfos.Values)
            {
                if (item.IsUnlocked)
                {
                    unlockedItem.Add(item.ItemId.AsPrimitive());
                }
                else
                {
                    lockedItem.Add(item.ItemId.AsPrimitive());
                }
            }

            // チャレンジカテゴリの解放状態を集める
            // Gather challenge-category unlock states
            var lockedChallengeCategory = new List<string>();
            var unlockedChallengeCategory = new List<string>();
            foreach (var challenge in state.ChallengeCategoryUnlockStateInfos.Values)
            {
                if (challenge.IsUnlocked)
                {
                    unlockedChallengeCategory.Add(challenge.ChallengeCategoryGuid.ToString());
                }
                else
                {
                    lockedChallengeCategory.Add(challenge.ChallengeCategoryGuid.ToString());
                }
            }
            
            // 機械レシピのアンロック状態を取得
            // Get machine recipe unlock states
            var lockedMachineRecipe = new List<string>();
            var unlockedMachineRecipe = new List<string>();
            foreach (var machineRecipe in state.MachineRecipeUnlockStateInfos.Values)
            {
                if (machineRecipe.IsUnlocked)
                {
                    unlockedMachineRecipe.Add(machineRecipe.MachineRecipeGuid.ToString());
                }
                else
                {
                    lockedMachineRecipe.Add(machineRecipe.MachineRecipeGuid.ToString());
                }
            }

            // ブロックと列車車両のアンロック状態を取得
            // Get block and train car unlock states
            var lockedBlock = new List<string>();
            var unlockedBlock = new List<string>();
            foreach (var block in state.BlockUnlockStateInfos.Values)
            {
                if (block.IsUnlocked) unlockedBlock.Add(block.BlockGuid.ToString());
                else lockedBlock.Add(block.BlockGuid.ToString());
            }

            var lockedTrainCar = new List<string>();
            var unlockedTrainCar = new List<string>();
            foreach (var trainCar in state.TrainCarUnlockStateInfos.Values)
            {
                if (trainCar.IsUnlocked) unlockedTrainCar.Add(trainCar.TrainCarGuid.ToString());
                else lockedTrainCar.Add(trainCar.TrainCarGuid.ToString());
            }

            // 接続ツールのアンロック状態を取得
            // Get connect tool unlock states
            var lockedConnectTool = new List<string>();
            var unlockedConnectTool = new List<string>();
            foreach (var connectTool in state.ConnectToolUnlockStateInfos.Values)
            {
                if (connectTool.IsUnlocked) unlockedConnectTool.Add(connectTool.ConnectToolGuid.ToString());
                else lockedConnectTool.Add(connectTool.ConnectToolGuid.ToString());
            }

            // 全カテゴリを同じ時点の応答へまとめる
            // Put all categories into one response from the same state
            return new ResponseGameUnlockStateProtocolMessagePack(
                unlockedCraftRecipeGuidsStr: unlockedCraftRecipe,
                lockedCraftRecipeGuidsStr: lockedCraftRecipe,
                lockedItemIdsInt: lockedItem,
                unlockedItemIdsInt: unlockedItem,
                lockedChallengeCategoryGuidsStr: lockedChallengeCategory,
                unlockedChallengeCategoryGuidsStr: unlockedChallengeCategory,
                lockedMachineRecipeGuidsStr: lockedMachineRecipe,
                unlockedMachineRecipeGuidsStr: unlockedMachineRecipe,
                lockedBlockGuidsStr: lockedBlock,
                unlockedBlockGuidsStr: unlockedBlock,
                lockedTrainCarGuidsStr: lockedTrainCar,
                unlockedTrainCarGuidsStr: unlockedTrainCar,
                lockedConnectToolGuidsStr: lockedConnectTool,
                unlockedConnectToolGuidsStr: unlockedConnectTool,
                isBlueprintUnlocked: state.IsBlueprintUnlocked);
        }
    }
}
