using System;
using Game.Block.Interface.MachineUpgrade;
using Game.Context;
using MessagePack;
using UniRx;

namespace Server.Event.EventReceive.MachineUpgrade
{
    /// <summary>
    ///     継承レシピの製作上限の解放を全プレイヤーへ配信する
    ///     Broadcasts craft-cap unlocks of inheritance recipes to every player
    /// </summary>
    public class LevelInheritanceCraftCapUnlockEventPacket : IBootInitializable
    {
        public const string EventTag = "va:event:levelInheritanceCraftCapUnlock";

        private readonly EventProtocolProvider _eventProtocolProvider;
        private readonly ILevelInheritanceCraftCapLookup _craftCapLookup;

        public LevelInheritanceCraftCapUnlockEventPacket(EventProtocolProvider eventProtocolProvider, ILevelInheritanceCraftCapLookup craftCapLookup)
        {
            _eventProtocolProvider = eventProtocolProvider;
            _craftCapLookup = craftCapLookup;
        }

        public void Load()
        {
            // 上限の解放を購読し、継承先の基準GUIDとレベルを配信する
            // Subscribe to cap unlocks and broadcast the target base GUID with its level
            _craftCapLookup.OnCraftCapUnlocked.Subscribe(data =>
            {
                var payload = MessagePackSerializer.Serialize(new LevelInheritanceCraftCapMessagePack(data.familyBaseItemGuid, data.level));
                _eventProtocolProvider.AddBroadcastEvent(EventTag, payload);
            });
        }

        [MessagePackObject]
        public class LevelInheritanceCraftCapMessagePack
        {
            [Key(0)] public Guid FamilyBaseItemGuid { get; set; }
            [Key(1)] public int Level { get; set; }

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public LevelInheritanceCraftCapMessagePack() { }

            public LevelInheritanceCraftCapMessagePack(Guid familyBaseItemGuid, int level)
            {
                FamilyBaseItemGuid = familyBaseItemGuid;
                Level = level;
            }
        }
    }
}
