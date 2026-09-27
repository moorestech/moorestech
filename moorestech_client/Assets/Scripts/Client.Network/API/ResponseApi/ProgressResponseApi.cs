using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Client.Network.Settings;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.Research;
using Server.Protocol.PacketResponse;

namespace Client.Network.API
{
    public class ProgressResponseApi
    {
        private readonly PacketExchangeManager _packetExchange;
        private readonly PlayerConnectionSetting _connectionSetting;

        public ProgressResponseApi(PacketExchangeManager packetExchangeManager, PlayerConnectionSetting playerConnectionSetting)
        {
            _packetExchange = packetExchangeManager;
            _connectionSetting = playerConnectionSetting;
        }

        public async UniTask<List<ChallengeCategoryResponse>> GetChallengeResponse(CancellationToken ct)
        {
            var request = new GetChallengeInfoProtocol.RequestChallengeMessagePack();
            var response = await _packetExchange.GetPacketResponse<GetChallengeInfoProtocol.ResponseChallengeInfoMessagePack>(request, ct);

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
        public async UniTask<UnlockStateResponse> GetUnlockState(CancellationToken ct)
        {
            var request = new GetGameUnlockStateProtocol.RequestGameUnlockStateProtocolMessagePack();
            var response = await _packetExchange.GetPacketResponse<GetGameUnlockStateProtocol.ResponseGameUnlockStateProtocolMessagePack>(request, ct);

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

        public async UniTask<Dictionary<Guid, ResearchNodeState>> GetResearchNodeStates(CancellationToken ct)
        {
            var request = new GetResearchInfoProtocol.RequestResearchInfoMessagePack(_connectionSetting.PlayerId);
            var response = await _packetExchange.GetPacketResponse<GetResearchInfoProtocol.ResponseResearchInfoMessagePack>(request, ct);

            return response.ToDictionary();
        }

        public async UniTask<List<string>> GetPlayedSkitIds(CancellationToken ct)
        {
            var request = new GetPlayedSkitIdsProtocol.RequestGetPlayedSkitIdsMessagePack();
            var response = await _packetExchange.GetPacketResponse<GetPlayedSkitIdsProtocol.ResponseGetPlayedSkitIdsMessagePack>(request, ct);

            return response.PlayedSkitIds;
        }

        public async UniTask<CompleteResearchProtocol.ResponseCompleteResearchMessagePack> CompleteResearch(Guid researchGuid, CancellationToken ct)
        {
            var request = new CompleteResearchProtocol.RequestCompleteResearchMessagePack(_connectionSetting.PlayerId, researchGuid);
            var response = await _packetExchange.GetPacketResponse<CompleteResearchProtocol.ResponseCompleteResearchMessagePack>(request, ct);

            return response;
        }
    }
}
