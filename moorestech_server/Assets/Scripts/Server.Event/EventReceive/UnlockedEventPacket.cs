using System;
using Core.Master;
using Game.Context;
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
    public class UnlockedEventPacket : IBootInitializable
    {
        public const string EventTag = "va:event:unlocked";
        
        private readonly EventProtocolProvider _eventProtocolProvider;
        private readonly IGameUnlockStateDataController _unlockState;

        public UnlockedEventPacket(EventProtocolProvider eventProtocolProvider, IGameUnlockStateDataController unlockState)
        {
            _eventProtocolProvider = eventProtocolProvider;
            _unlockState = unlockState;
        }

        public void Load()
        {
            _unlockState.OnUnlockCraftRecipe.Subscribe(c => AddBroadcastEvent(new UnlockEventMessagePack(UnlockEventType.CraftRecipe, c)));
            _unlockState.OnUnlockItem.Subscribe(i => AddBroadcastEvent(new UnlockEventMessagePack(i)));
            _unlockState.OnUnlockChallengeCategory.Subscribe(c => AddBroadcastEvent(new UnlockEventMessagePack(UnlockEventType.ChallengeCategory, c)));
            _unlockState.OnUnlockMachineRecipe.Subscribe(m => AddBroadcastEvent(new UnlockEventMessagePack(UnlockEventType.MachineRecipe, m)));
            _unlockState.OnUnlockBlock.Subscribe(b => AddBroadcastEvent(new UnlockEventMessagePack(UnlockEventType.Block, b)));
            _unlockState.OnUnlockTrainCar.Subscribe(t => AddBroadcastEvent(new UnlockEventMessagePack(UnlockEventType.TrainCar, t)));
            _unlockState.OnUnlockConnectTool.Subscribe(c => AddBroadcastEvent(new UnlockEventMessagePack(UnlockEventType.ConnectTool, c)));
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
        [IgnoreMember] public Guid UnlockedChallengeCategoryGuid => Guid.Parse(UnlockedChallengeCategoryGuidStr);
        [IgnoreMember] public Guid UnlockedMachineRecipeGuid => Guid.Parse(UnlockedMachineRecipeGuidStr);
        [IgnoreMember] public Guid UnlockedBlockGuid => Guid.Parse(UnlockedBlockGuidStr);
        [IgnoreMember] public Guid UnlockedTrainCarGuid => Guid.Parse(UnlockedTrainCarGuidStr);
        [IgnoreMember] public Guid UnlockedConnectToolGuid => Guid.Parse(UnlockedConnectToolGuidStr);

        [Key(0)] public int UnlockEventTypeInt { get; set; }
        [Key(1)] public string UnlockedCraftRecipeGuidStr { get; set; }
        [Key(2)] public int UnlockedItemIdInt { get; set; }
        [Key(3)] public string UnlockedChallengeCategoryGuidStr { get; set; }
        [Key(4)] public string UnlockedMachineRecipeGuidStr { get; set; }
        [Key(5)] public string UnlockedBlockGuidStr { get; set; }
        [Key(6)] public string UnlockedTrainCarGuidStr { get; set; }
        [Key(7)] public string UnlockedConnectToolGuidStr { get; set; }
        
        
        
        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public UnlockEventMessagePack() { }
        
        public UnlockEventMessagePack(ItemId itemId)
        {
            UnlockEventTypeInt = (int)UnlockEventType.Item;
            UnlockedItemIdInt = (int)itemId;
        }
        
        public UnlockEventMessagePack(UnlockEventType unlockEventType, Guid guid)
        {
            UnlockEventTypeInt = (int)unlockEventType;
            switch (unlockEventType)
            {
                case UnlockEventType.ChallengeCategory:
                    UnlockedChallengeCategoryGuidStr = guid.ToString();
                    break;
                case UnlockEventType.CraftRecipe:
                    UnlockedCraftRecipeGuidStr = guid.ToString();
                    break;
                case UnlockEventType.MachineRecipe:
                    UnlockedMachineRecipeGuidStr = guid.ToString();
                    break;
                case UnlockEventType.Block:
                    UnlockedBlockGuidStr = guid.ToString();
                    break;
                case UnlockEventType.TrainCar:
                    UnlockedTrainCarGuidStr = guid.ToString();
                    break;
                case UnlockEventType.ConnectTool:
                    UnlockedConnectToolGuidStr = guid.ToString();
                    break;
            }
        }
    }

    public enum UnlockEventType
    {
        CraftRecipe,
        Item,
        ChallengeCategory,
        MachineRecipe,
        Block,
        TrainCar,
        ConnectTool,
    }
}