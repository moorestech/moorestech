using System;
using Core.Master;
using Game.UnlockState;
using MessagePack;
using UniRx;

namespace Server.Event.EventReceive
{
    /// <summary>
    /// ゲーム内のアンロックイベントを送信するためのクラス。アンロックされるものは複数あるが、そのためにイベントを複数用意するのは面倒なので統合して扱う。
    /// 分割したい需要が発生したら分割するかも。
    /// A class for sending in-game unlock events. There are multiple things that can be unlocked, but it would be tedious to prepare multiple events for that, so we will handle them together.
    /// We may split it up if there is a demand to do so.
    /// </summary>
    public class UnlockedEventPacket
    {
        public const string EventTag = "va:event:unlocked";
        
        private readonly EventProtocolProvider _eventProtocolProvider;
        
        public UnlockedEventPacket(EventProtocolProvider eventProtocolProvider, IGameUnlockStateDataController unlockState)
        {
            _eventProtocolProvider = eventProtocolProvider;
            
            unlockState.OnUnlockCraftRecipe.Subscribe(c => AddBroadcastEvent(new UnlockEventMessagePack(UnlockEventType.CraftRecipe ,c)));
            unlockState.OnUnlockItem.Subscribe(i => AddBroadcastEvent(new UnlockEventMessagePack(i)));
            unlockState.OnUnlockChallengeCategory.Subscribe(c => AddBroadcastEvent(new UnlockEventMessagePack(UnlockEventType.Challenge ,c)));
        }
        
        private void AddBroadcastEvent(UnlockEventMessagePack unlockEventMessagePack)
        {
            var payload = MessagePackSerializer.Serialize(unlockEventMessagePack);
            
            _eventProtocolProvider.AddBroadcastEvent(EventTag, payload);
        }
    }
    
    [MessagePackObject]
    public class UnlockEventMessagePack
    {
        [IgnoreMember] public UnlockEventType UnlockEventType => (UnlockEventType)UnlockEventTypeInt; 
        [IgnoreMember] public Guid UnlockedCraftRecipeGuid => Guid.Parse(UnlockedCraftRecipeGuidStr);
        [IgnoreMember] public ItemId UnlockedItemId => new(UnlockedItemIdInt);
        [IgnoreMember] public Guid UnlockedChallengeGuid => Guid.Parse(UnlockedChallengeGuidStr);
        
        [Key(0)] public int UnlockEventTypeInt { get; set; }
        [Key(1)] public string UnlockedCraftRecipeGuidStr { get; set; }
        [Key(2)] public int UnlockedItemIdInt { get; set; } 
        [Key(3)] public string UnlockedChallengeGuidStr { get; set; }
        
        
        
        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public UnlockEventMessagePack() { }
        
        public UnlockEventMessagePack(ItemId itemId)
        {
            UnlockEventTypeInt = (int)UnlockEventType.Item;
            UnlockedItemIdInt = (int)itemId;
        }
        
        public UnlockEventMessagePack(UnlockEventType unlockEventType,Guid unlockedChallengeGuid)
        {
            UnlockEventTypeInt = (int)unlockEventType;
            switch (unlockEventType)
            {
                case UnlockEventType.Challenge:
                    UnlockedChallengeGuidStr = unlockedChallengeGuid.ToString();
                    break;
                case UnlockEventType.CraftRecipe:
                    UnlockedCraftRecipeGuidStr = unlockedChallengeGuid.ToString();
                    break;
            }
        }
    }
    
    public enum UnlockEventType
    {
        CraftRecipe,
        Item,
        Challenge
    }
}