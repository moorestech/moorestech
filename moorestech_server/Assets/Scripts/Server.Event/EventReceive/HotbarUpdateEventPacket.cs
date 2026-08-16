using System;
using System.Linq;
using Game.Context;
using Game.Hotbar;
using MessagePack;
using UniRx;

namespace Server.Event.EventReceive
{
    /// <summary>
    /// ホットバー割当の変更を該当プレイヤーへ通知する。低頻度操作のため全量9個を送り差分化しない
    /// Notifies the owning player of hotbar assignment changes; not diffed since it's a low-frequency operation, so all 9 slots are sent.
    /// </summary>
    public class HotbarUpdateEventPacket : IBootInitializable
    {
        public const string EventTag = "va:event:hotbarUpdate";

        private readonly EventProtocolProvider _eventProtocolProvider;
        private readonly IHotbarAssignmentLookup _hotbarAssignmentLookup;

        public HotbarUpdateEventPacket(EventProtocolProvider eventProtocolProvider, IHotbarAssignmentLookup hotbarAssignmentLookup)
        {
            _eventProtocolProvider = eventProtocolProvider;
            _hotbarAssignmentLookup = hotbarAssignmentLookup;
        }

        public void Load()
        {
            _hotbarAssignmentLookup.OnAssignmentChanged.Subscribe(OnAssignmentChanged);
        }

        private void OnAssignmentChanged(int playerId)
        {
            var assignments = _hotbarAssignmentLookup.GetAssignments(playerId).ToArray();
            var messagePack = new HotbarUpdateEventMessagePack(assignments);
            var payload = MessagePackSerializer.Serialize(messagePack);

            _eventProtocolProvider.AddEvent(playerId, EventTag, payload);
        }

        #region MessagePack

        [MessagePackObject]
        public class HotbarUpdateEventMessagePack
        {
            [Key(0)] public Guid[] Assignments { get; set; }

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public HotbarUpdateEventMessagePack() { }

            public HotbarUpdateEventMessagePack(Guid[] assignments)
            {
                Assignments = assignments;
            }
        }

        #endregion
    }
}
