using System;
using Game.Research;
using MessagePack;
using UniRx;

namespace Server.Event.EventReceive
{
    /// <summary>
    /// 研究状態の変化をクライアントに通知するイベントパケット
    /// </summary>
    public class ResearchCompleteEventPacket
    {
        public const string EventTag = "va:event:researchComplete";
        
        public ResearchCompleteEventPacket(EventProtocolProvider eventProtocolProvider, ResearchEvent researchEvent)
        {
            // 研究完了イベントをサブスクライブ
            researchEvent.OnResearchCompleted.Subscribe(data =>
            {
                var eventData = new ResearchCompleteEventMessagePack(data.playerId, data.researchNode.ResearchNodeGuid);
                var payload = MessagePackSerializer.Serialize(eventData);
                eventProtocolProvider.AddBroadcastEvent(EventTag, payload);
            });
        }
        
        [MessagePackObject]
        public class ResearchCompleteEventMessagePack
        {
            [Key(0)] public int PlayerId { get; set; }
            [Key(1)] public string ResearchGuidStr { get; set; }

            [IgnoreMember] public Guid ResearchNodeGuid => Guid.Parse(ResearchGuidStr);

            [Obsolete("This constructor is for deserialization. Do not use directly.")]
            public ResearchCompleteEventMessagePack() { }
            
            public ResearchCompleteEventMessagePack(int playerId, Guid researchGuid)
            {
                PlayerId = playerId;
                ResearchGuidStr = researchGuid.ToString();
            }
        }
    }
}