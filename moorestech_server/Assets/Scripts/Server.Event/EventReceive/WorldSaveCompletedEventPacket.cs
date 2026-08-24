using System;
using Game.Context;
using Game.SaveLoad.Interface;
using MessagePack;
using UniRx;

namespace Server.Event.EventReceive
{
    /// <summary>
    /// セーブの書き出し完了をクライアントへ通知するイベントパケット
    /// Event packet notifying clients that a save finished writing
    /// </summary>
    public class WorldSaveCompletedEventPacket : IBootInitializable
    {
        public const string EventTag = "va:event:worldSaveCompleted";

        private readonly EventProtocolProvider _eventProtocolProvider;
        private readonly IWorldSaveCompletionNotifier _worldSaveCompletionNotifier;

        public WorldSaveCompletedEventPacket(EventProtocolProvider eventProtocolProvider, IWorldSaveCompletionNotifier worldSaveCompletionNotifier)
        {
            _eventProtocolProvider = eventProtocolProvider;
            _worldSaveCompletionNotifier = worldSaveCompletionNotifier;
        }

        public void Load()
        {
            // 書き出し完了を購読し全プレイヤーへ配信する。リモート接続の終了待ちがこれを待つ
            // Subscribe to write completion and broadcast it; a remote client's shutdown wait depends on this
            _worldSaveCompletionNotifier.OnWorldSaveCompleted.Subscribe(OnWorldSaveCompleted);

            #region Internal

            void OnWorldSaveCompleted(long completedGeneration)
            {
                var payload = MessagePackSerializer.Serialize(new WorldSaveCompletedMessagePack(completedGeneration));
                _eventProtocolProvider.AddBroadcastEvent(EventTag, payload);
            }

            #endregion
        }

        [MessagePackObject]
        public class WorldSaveCompletedMessagePack
        {
            [Key(0)] public long CompletedSaveGeneration { get; set; }

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public WorldSaveCompletedMessagePack() { }

            public WorldSaveCompletedMessagePack(long completedSaveGeneration)
            {
                CompletedSaveGeneration = completedSaveGeneration;
            }
        }
    }
}
