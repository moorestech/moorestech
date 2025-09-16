using System;
using Game.Research;
using MessagePack;
using UniRx;

namespace Server.Event.EventReceive
{
    /// <summary>
    /// 研究状態の変化をクライアントに通知するイベントパケット
    /// </summary>
    public class ResearchCompleteEventPacket : IDisposable
    {
        public const string EventTag = "va:event:researchComplete";

        private readonly IDisposable _disposable;

        public ResearchCompleteEventPacket(EventProtocolProvider eventProtocolProvider, ResearchEvent researchEvent)
        {
            // 研究完了イベントをサブスクライブ
            _disposable = researchEvent.OnResearchCompleted.Subscribe(data =>
            {
                var eventData = new ResearchCompleteEventMessagePack(data.playerId, data.researchNode.ResearchNodeGuid);
                var payload = MessagePackSerializer.Serialize(eventData);
                eventProtocolProvider.AddBroadcastEvent(EventTag, payload);
            });
        }

        public void Dispose()
        {
            _disposable?.Dispose();
        }

        [MessagePackObject]
        public class ResearchCompleteEventMessagePack
        {
            [Key(0)] public int PlayerId { get; set; }
            [Key(1)] public string ResearchGuidStr { get; set; }

            [IgnoreMember] public Guid ResearchNodeGuid => Guid.Parse(ResearchGuidStr);

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public ResearchCompleteEventMessagePack() { }

            public ResearchCompleteEventMessagePack(int playerId, Guid researchGuid)
            {
                PlayerId = playerId;
                ResearchGuidStr = researchGuid.ToString();
            }
        }
    }
}