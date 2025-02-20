using System;
using Game.UnlockState;
using MessagePack;
using UniRx;

namespace Server.Event.EventReceive
{
    public class UnlockedRecipeEventPacket
    {
        public const string EventTag = "va:event:unlockedRecipe";
        
        private readonly EventProtocolProvider _eventProtocolProvider;
        
        public UnlockedRecipeEventPacket(EventProtocolProvider eventProtocolProvider, IGameUnlockStateDatastore gameUnlockStateDatastore)
        {
            _eventProtocolProvider = eventProtocolProvider;
            gameUnlockStateDatastore.OnUnlockRecipe.Subscribe(OnUnlockRecipe);
        }
        
        private void OnUnlockRecipe(Guid recipeGuid)
        {
            var messagePack = new UnlockRecipeEventMessage(recipeGuid);
            var payload = MessagePackSerializer.Serialize(messagePack);
            
            _eventProtocolProvider.AddBroadcastEvent(EventTag, payload);
        }
    }
    
    [MessagePackObject]
    public class UnlockRecipeEventMessage
    {
        [Key(0)] public string UnlockedRecipeGuidStr { get; set; }
        
        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public UnlockRecipeEventMessage() { }
        
        public UnlockRecipeEventMessage(Guid unlockedRecipeGuid)
        {
            UnlockedRecipeGuidStr = unlockedRecipeGuid.ToString();
        }
    }
}