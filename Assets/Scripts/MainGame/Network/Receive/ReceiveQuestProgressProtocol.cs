using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using MainGame.Basic.Quest;
using MainGame.Network.Event;
using MessagePack;
using Server.Protocol.PacketResponse;

namespace MainGame.Network.Receive
{
    public class ReceiveQuestProgressProtocol: IAnalysisPacket
    {
        private readonly ReceiveQuestDataEvent receiveQuestDataEvent;

        public ReceiveQuestProgressProtocol(ReceiveQuestDataEvent receiveQuestDataEvent)
        {
            this.receiveQuestDataEvent = receiveQuestDataEvent;
        }

        public void Analysis(List<byte> packet)
        {
            var data = MessagePackSerializer.Deserialize<QuestProgressResponseProtocolMessagePack>(packet.ToArray());
            
            var result = data.Quests.ToDictionary(q => q.Id, q =>
            {
                return new QuestProgressData(q.IsCompleted, q.IsRewarded,q.IsRewardEarnable);
            });

            receiveQuestDataEvent.InvokeReceiveQuestProgress(new QuestProgressProperties(result)).Forget();
        }
    }
}