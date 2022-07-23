using System.Collections.Generic;
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
            
            var result = new Dictionary<string, (bool IsCompleted, bool IsRewarded)>();

            foreach (var quest in data.Quests)
            {
                result.Add(quest.Id,(quest.IsCompleted,quest.IsRewarded));
            }
            
            receiveQuestDataEvent.InvokeReceiveQuestProgress(new QuestProgressProperties(result));
        }
    }
}