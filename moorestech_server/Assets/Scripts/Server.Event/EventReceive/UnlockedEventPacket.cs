using System;
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
        
        public UnlockedEventPacket(EventProtocolProvider eventProtocolProvider, IGameUnlockStateDataController gameUnlockStateDataController)
        {
            _eventProtocolProvider = eventProtocolProvider;
            gameUnlockStateDataController.OnUnlockCraftRecipe.Subscribe(OnUnlockCraftRecipe);
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
        
        [IgnoreMember] public Guid UnlockedCraftRecipeGuid => Guid.Parse(UnlockedCraftRecipeGuidStr);
        
        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public UnlockCraftRecipeEventMessagePack() { }
        
        public UnlockCraftRecipeEventMessagePack(Guid unlockedRecipeGuid)
        {
            UnlockedCraftRecipeGuidStr = unlockedRecipeGuid.ToString();
        }
    }
}