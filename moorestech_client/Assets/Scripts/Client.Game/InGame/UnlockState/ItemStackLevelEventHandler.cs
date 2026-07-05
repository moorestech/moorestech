using Client.Game.InGame.Context;
using Client.Network.API;
using MessagePack;
using Server.Event.EventReceive;
using VContainer.Unity;

namespace Client.Game.InGame.UnlockState
{
    // 研究完了・チャレンジ完了イベントを購読しスタックレベルを追随させる（他プレイヤー分含む）
    // Follow research and challenge completions (including other players') to keep stack levels current
    public class ItemStackLevelEventHandler : IInitializable
    {
        public void Initialize()
        {
            ClientContext.VanillaApi.Event.SubscribeEventResponse(ResearchCompleteEventPacket.EventTag, OnResearchComplete);
            ClientContext.VanillaApi.Event.SubscribeEventResponse(CompletedChallengeEventPacket.EventTag, OnChallengeComplete);
        }

        private void OnResearchComplete(byte[] payload)
        {
            var data = MessagePackSerializer.Deserialize<ResearchCompleteEventPacket.ResearchCompleteEventMessagePack>(payload);
            ResearchItemStackLevelApplier.Apply(data.ResearchNodeGuid);
        }

        private void OnChallengeComplete(byte[] payload)
        {
            // 完了後のチャレンジ状態スナップショットから再導出（サーバーの完了＋カテゴリ解放時実行を漏れなくミラーする）
            // Re-derive from the post-completion challenge snapshot to fully mirror server completion and category-unlock execution
            var data = MessagePackSerializer.Deserialize<CompletedChallengeEventMessagePack>(payload);
            foreach (var category in data.ChallengeCategories)
            {
                foreach (var completedGuid in category.CompletedChallengeGuids) ChallengeItemStackLevelApplier.ApplyCleared(completedGuid);
                foreach (var currentGuid in category.CurrentChallengeGuids) ChallengeItemStackLevelApplier.ApplyStarted(currentGuid);
            }
        }
    }
}
