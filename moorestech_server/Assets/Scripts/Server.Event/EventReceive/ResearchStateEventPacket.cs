using System;
using Game.Research;
using MessagePack;
using Server.Protocol;
using UniRx;

namespace Server.Event.EventReceive
{
    /// <summary>
    /// 研究状態の変化をクライアントに通知するイベントパケット
    /// </summary>
    public class ResearchStateEventPacket
    {
        public const string EventTag = "va:event:research_state";

        private readonly EventProtocolProvider _eventProtocolProvider;

        public ResearchStateEventPacket(EventProtocolProvider eventProtocolProvider, ResearchEvent researchEvent)
        {
            _eventProtocolProvider = eventProtocolProvider;

            // 研究完了イベントをサブスクライブ
            researchEvent.OnResearchCompleted.Subscribe(data =>
            {
                var eventData = new ResearchStateEventData
                {
                    PlayerId = data.playerId,
                    ResearchGuidStr = data.researchGuid.ToString(),
                    IsCompleted = true
                };
                AddBroadcastEvent(new ResearchStateEventMessagePack(EventTag, eventData));
            });

            // 研究失敗イベントをサブスクライブ
            researchEvent.OnResearchFailed.Subscribe(data =>
            {
                var eventData = new ResearchStateEventData
                {
                    PlayerId = data.playerId,
                    ResearchGuidStr = data.researchGuid.ToString(),
                    IsCompleted = false,
                    FailureReason = data.reason
                };
                AddBroadcastEvent(new ResearchStateEventMessagePack(EventTag, eventData));
            });
        }

        private void AddBroadcastEvent(ResearchStateEventMessagePack eventMessagePack)
        {
            var payload = MessagePackSerializer.Serialize(eventMessagePack);
            _eventProtocolProvider.AddBroadcastEvent(EventTag, payload);
        }

        #region MessagePack Classes

        [MessagePackObject]
        public class ResearchStateEventMessagePack : ProtocolMessagePackBase
        {
            [Key(0)] public string Tag { get; set; }
            [Key(1)] public ResearchStateEventData Data { get; set; }

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public ResearchStateEventMessagePack()
            {
            }

            public ResearchStateEventMessagePack(string tag, ResearchStateEventData data)
            {
                Tag = tag;
                Data = data;
            }
        }

        [MessagePackObject]
        public class ResearchStateEventData
        {
            [Key(0)] public int PlayerId { get; set; }
            [Key(1)] public string ResearchGuidStr { get; set; }
            [Key(2)] public bool IsCompleted { get; set; }
            [Key(3)] public string FailureReason { get; set; }

            [IgnoreMember] public Guid ResearchGuid => Guid.Parse(ResearchGuidStr);

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public ResearchStateEventData()
            {
            }
        }

        #endregion
    }
}