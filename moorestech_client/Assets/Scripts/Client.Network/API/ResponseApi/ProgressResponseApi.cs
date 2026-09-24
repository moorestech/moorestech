using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.Research;
using Server.Protocol.PacketResponse;

namespace Client.Network.API
{
    public static class ProgressResponseApi
    {
        public static async UniTask<List<ChallengeCategoryResponse>> GetChallengeResponse(this VanillaApiWithResponse api, CancellationToken ct)
        {
            var request = new GetChallengeInfoProtocol.RequestChallengeMessagePack();
            var response = await api.PacketExchange.GetPacketResponse<GetChallengeInfoProtocol.ResponseChallengeInfoMessagePack>(request, ct);

            var result = new List<ChallengeCategoryResponse>();
            foreach (var category in response.Categories)
            {
                var categoryMaster = MasterHolder.ChallengeMaster.GetChallengeCategory(category.ChallengeCategoryGuid);
                var current = category.CurrentChallengeGuids.Select(MasterHolder.ChallengeMaster.GetChallenge).ToList();
                var completed = category.CompletedChallengeGuids.Select(MasterHolder.ChallengeMaster.GetChallenge).ToList();

                result.Add(new ChallengeCategoryResponse(categoryMaster, category.IsUnlocked, current, completed));
            }

            return result;
        }

        
        // アンロック状態をクライアント表現へ変換する
        // Convert unlock state into the client representation
        public static async UniTask<UnlockStateResponse> GetUnlockState(this VanillaApiWithResponse api, CancellationToken ct)
        {
            var request = new GetGameUnlockStateProtocol.RequestGameUnlockStateProtocolMessagePack();
            var response = await api.PacketExchange.GetPacketResponse<GetGameUnlockStateProtocol.ResponseGameUnlockStateProtocolMessagePack>(request, ct);

            return new UnlockStateResponse(
                lockedCraftRecipeGuids: response.LockedCraftRecipeGuids,
                unlockedCraftRecipeGuids: response.UnlockedCraftRecipeGuids,
                lockedItemIds: response.LockedItemIds,
                unlockedItemIds: response.UnlockedItemIds,
                lockedChallengeCategoryGuids: response.LockedCategoryChallengeGuids,
                unlockedChallengeCategoryGuids: response.UnlockedCategoryChallengeGuids,
                lockedMachineRecipeGuids: response.LockedMachineRecipeGuids,
                unlockedMachineRecipeGuids: response.UnlockedMachineRecipeGuids,
                lockedBlockGuids: response.LockedBlockGuids,
                unlockedBlockGuids: response.UnlockedBlockGuids,
                lockedTrainCarGuids: response.LockedTrainCarGuids,
                unlockedTrainCarGuids: response.UnlockedTrainCarGuids,
                lockedConnectToolGuids: response.LockedConnectToolGuids,
                unlockedConnectToolGuids: response.UnlockedConnectToolGuids,
                isBlueprintUnlocked: response.IsBlueprintUnlocked);
        }

        public static async UniTask<Dictionary<Guid, ResearchNodeState>> GetResearchNodeStates(this VanillaApiWithResponse api, CancellationToken ct)
        {
            var request = new GetResearchInfoProtocol.RequestResearchInfoMessagePack(api.ConnectionSetting.PlayerId);
            var response = await api.PacketExchange.GetPacketResponse<GetResearchInfoProtocol.ResponseResearchInfoMessagePack>(request, ct);

            return response.ToDictionary();
        }

        public static async UniTask<List<string>> GetPlayedSkitIds(this VanillaApiWithResponse api, CancellationToken ct)
        {
            var request = new GetPlayedSkitIdsProtocol.RequestGetPlayedSkitIdsMessagePack();
            var response = await api.PacketExchange.GetPacketResponse<GetPlayedSkitIdsProtocol.ResponseGetPlayedSkitIdsMessagePack>(request, ct);

            return response.PlayedSkitIds;
        }

        public static async UniTask<CompleteResearchProtocol.ResponseCompleteResearchMessagePack> CompleteResearch(this VanillaApiWithResponse api, Guid researchGuid, CancellationToken ct)
        {
            var request = new CompleteResearchProtocol.RequestCompleteResearchMessagePack(api.ConnectionSetting.PlayerId, researchGuid);
            var response = await api.PacketExchange.GetPacketResponse<CompleteResearchProtocol.ResponseCompleteResearchMessagePack>(request, ct);

            return response;
        }
    }
}
