using System;
using Game.UnlockState;
using MessagePack;
using UniRx;

namespace Server.Event.EventReceive
{
    public class UnlockedCraftRecipeEventPacket
    {
        public const string EventTag = "va:event:unlockedCraftRecipe";
        
        private readonly EventProtocolProvider _eventProtocolProvider;
        
        public UnlockedCraftRecipeEventPacket(EventProtocolProvider eventProtocolProvider, IGameUnlockStateDatastore gameUnlockStateDatastore)
        {
            _eventProtocolProvider = eventProtocolProvider;
            gameUnlockStateDatastore.OnUnlockCraftRecipe.Subscribe(OnUnlockCraftRecipe);
        }
        
        private void OnUnlockCraftRecipe(Guid recipeGuid)
        {
            var messagePack = new UnlockCraftRecipeEventMessagePack(recipeGuid);
            var payload = MessagePackSerializer.Serialize(messagePack);
            
            _eventProtocolProvider.AddBroadcastEvent(EventTag, payload);
        }
    }
    
    [MessagePackObject]
    public class UnlockCraftRecipeEventMessagePack
    {
        [Key(0)] public string UnlockedCraftRecipeGuidStr { get; set; }
        
        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public UnlockCraftRecipeEventMessagePack() { }
        
        public UnlockCraftRecipeEventMessagePack(Guid unlockedRecipeGuid)
        {
            UnlockedCraftRecipeGuidStr = unlockedRecipeGuid.ToString();
        }
    }
}