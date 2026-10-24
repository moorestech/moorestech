using System;
using Game.Block.Interface.MachineUpgrade;
using Game.Context;
using MessagePack;
using UniRx;

namespace Server.Event.EventReceive.MachineUpgrade
{
    /// <summary>
    ///     モジュールスロットのスタック上限レベルの変化を全プレイヤーへ配信する
    ///     Broadcasts module-slot stack limit level changes to every player
    /// </summary>
    public class ModuleSlotStackLevelEventPacket : IBootInitializable
    {
        public const string EventTag = "va:event:moduleSlotStackLevel";

        private readonly EventProtocolProvider _eventProtocolProvider;
        private readonly IModuleSlotStackLimitLookup _stackLimitLookup;

        public ModuleSlotStackLevelEventPacket(EventProtocolProvider eventProtocolProvider, IModuleSlotStackLimitLookup stackLimitLookup)
        {
            _eventProtocolProvider = eventProtocolProvider;
            _stackLimitLookup = stackLimitLookup;
        }

        public void Load()
        {
            // 上限の変化を購読し、変化後のレベルを配信する
            // Subscribe to limit changes and broadcast the level after the change
            _stackLimitLookup.OnStackLimitChanged.Subscribe(_ =>
            {
                var payload = MessagePackSerializer.Serialize(new ModuleSlotStackLevelMessagePack(_stackLimitLookup.CurrentLevel));
                _eventProtocolProvider.AddBroadcastEvent(EventTag, payload);
            });
        }

        [MessagePackObject]
        public class ModuleSlotStackLevelMessagePack
        {
            [Key(0)] public int Level { get; set; }

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public ModuleSlotStackLevelMessagePack() { }

            public ModuleSlotStackLevelMessagePack(int level)
            {
                Level = level;
            }
        }
    }
}
