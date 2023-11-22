using System;
using System.Collections.Generic;
using Game.PlayerInventory.Interface;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Game.Crafting.Interface;

namespace Server.Protocol.PacketResponse
{
    public class OneClickCraft: IPacketResponse
    {
        public const string Tag = "va:oneClickCraft";
        
        private readonly IPlayerInventoryDataStore _playerInventoryDataStore; 
        private readonly ICraftingConfig _craftingConfig;

        public OneClickCraft(ServiceProvider serviceProvider)
        {
            _playerInventoryDataStore = serviceProvider.GetService<IPlayerInventoryDataStore>();
            _craftingConfig = serviceProvider.GetService<ICraftingConfig>();
        }
        
        
        public List<List<byte>> GetResponse(List<byte> payload)
        {
            var data = MessagePackSerializer.Deserialize<RequestOneClickCraftProtocolMessagePack>(payload.ToArray());
            
            var craftConfig = _craftingConfig.GetCraftingConfigData(data.CraftRecipeId);
            //プレイヤーインベントリを取得
            var playerMainInventory = _playerInventoryDataStore.GetInventoryData(data.PlayerId).MainOpenableInventory;

            //クラフト可能かどうかを確認
            if (!playerMainInventory.IsCraftable(craftConfig))
            {
                //クラフト不可能な場合は何もしない
                return new List<List<byte>>();
            }
            
            //クラフト可能な場合はクラフトを実行
            
            //クラフトに必要なアイテムを消費
            playerMainInventory.SubItem(craftConfig);
            //クラフト結果をプレイヤーインベントリに追加
            playerMainInventory.InsertItem(craftConfig.Result);
            
            
            return new List<List<byte>>();
        }
    }
    
    [MessagePackObject(true)]
    public class RequestOneClickCraftProtocolMessagePack : ProtocolMessagePackBase
    {
        public int PlayerId { get; set; }
        public int CraftRecipeId { get; set; }
        
        public RequestOneClickCraftProtocolMessagePack(int playerId, int craftRecipeId)
        {
            Tag = OneClickCraft.Tag;
            PlayerId = playerId;
            CraftRecipeId = craftRecipeId;
        }

        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public RequestOneClickCraftProtocolMessagePack() { }
    }
}