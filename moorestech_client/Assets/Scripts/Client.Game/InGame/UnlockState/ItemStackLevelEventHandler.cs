using Client.Game.InGame.Context;
using Client.Network.API;
using MessagePack;
using Server.Event.EventReceive;
using VContainer.Unity;

namespace Client.Game.InGame.UnlockState
{
    // 他プレイヤー含む研究完了イベントを購読しスタックレベルを追随させる
    // Follow research completions (including other players') to keep stack levels current
    public class ItemStackLevelEventHandler : IInitializable
    {
        public void Initialize()
        {
            ClientContext.VanillaApi.Event.SubscribeEventResponse(ResearchCompleteEventPacket.EventTag, OnResearchComplete);
        }

        private void OnResearchComplete(byte[] payload)
        {
            var data = MessagePackSerializer.Deserialize<ResearchCompleteEventPacket.ResearchCompleteEventMessagePack>(payload);
            ResearchItemStackLevelApplier.Apply(data.ResearchNodeGuid);
        }
    }
}
