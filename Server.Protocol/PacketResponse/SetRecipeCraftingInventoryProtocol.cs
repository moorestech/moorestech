using System;
using System.Collections.Generic;
using Core.Const;
using Core.Inventory;
using Core.Item;
using Core.Item.Config;
using Game.PlayerInventory.Interface;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Server.Protocol.PacketResponse.Util;
using Server.Protocol.PacketResponse.Util.InventoryService;
using Server.Util.MessagePack;

namespace Server.Protocol.PacketResponse
{
    public class SetRecipeCraftingInventoryProtocol: IPacketResponse
    {
        public const string Tag = "va:setRecipeCraftingInventory";
        private readonly IPlayerInventoryDataStore _playerInventoryDataStore;
        private readonly ItemStackFactory _itemStackFactory;
        private readonly IItemConfig _itemConfig;
        
        public SetRecipeCraftingInventoryProtocol(ServiceProvider serviceProvider)
        {
            _playerInventoryDataStore = serviceProvider.GetService<IPlayerInventoryDataStore>();
            _itemStackFactory = serviceProvider.GetService<ItemStackFactory>();
            _itemConfig = serviceProvider.GetService<IItemConfig>();
        }
        public List<List<byte>> GetResponse(List<byte> payload)
        {
            var data = MessagePackSerializer.Deserialize<SetRecipeCraftingInventoryProtocolMessagePack>(payload.ToArray());

            var mainInventory = _playerInventoryDataStore.GetInventoryData(data.PlayerId).MainOpenableInventory;
            var craftingInventory = _playerInventoryDataStore.GetInventoryData(data.PlayerId).CraftingOpenableInventory;
            var grabInventory = _playerInventoryDataStore.GetInventoryData(data.PlayerId).GrabInventory;
            
            
            //アイテムの移動
            MoveToMainInventory(mainInventory,craftingInventory,grabInventory);
            
            //移動できるかチェック
            var (isReplaceable,mainInventoryRequiredItemCount) = IsReplaceable(mainInventory,data.Recipe);
            if (!isReplaceable)
            {
                return new List<List<byte>>();
            }
            
            //実際に移動するアイテム数を計算
            var moveItem = CalcCraftInventoryPlaceItem(_itemStackFactory,_itemConfig,data.Recipe,mainInventoryRequiredItemCount);
            
            //実際に移動する
            MoveRecipeToCraftInventory(_itemStackFactory, mainInventory, craftingInventory, moveItem);
            
            return new List<List<byte>>();
        }

        //--------- アイテム設置のロジック用メソッド　各メソッドがプロパティにアクセスしないことを明示するためにstaticにしておく ----------
        
        
        private static void MoveToMainInventory(IOpenableInventory main, IOpenableInventory craft, IOpenableInventory grab)
        {
            //クラフトインベントリ、グラブインベントリのアイテムを全てメインインベントリに移動
            for (int i = 0; i < PlayerInventoryConst.CraftingSlotSize; i++)
            {
                var itemCount = craft.GetItem(i).Count;
                InventoryItemInsertService.Insert(craft,i,main,itemCount);
            }
            var grabItemCount = grab.GetItem(0).Count;
            InventoryItemInsertService.Insert(grab,0,main,grabItemCount);
        }

        private static (bool isReplacable,Dictionary<int,int> mainInventoryRequiredItemCount) IsReplaceable(IOpenableInventory mainInventory,ItemMessagePack[] recipeItem)
        {
            //必要なアイテムがMainインベントリにあるかチェックするための必要アイテム数辞書を作成
            var requiredItemCount = new Dictionary<int, int>();
            foreach (var item in recipeItem)
            {
                if (requiredItemCount.ContainsKey(item.Id))
                {
                    requiredItemCount[item.Id] += item.Count;
                }
                else
                {
                    requiredItemCount.Add(item.Id, item.Count);
                }
            }
            //必要なアイテム数があるかチェックするためにMainインベントリを走査
            var mainInventoryRequiredItemCount = new Dictionary<int, int>();
            for (int i = 0; i < PlayerInventoryConst.MainInventorySize; i++)
            {
                var itemId = mainInventory.GetItem(i).Id;
                if (!requiredItemCount.ContainsKey(itemId)) continue;

                if (mainInventoryRequiredItemCount.ContainsKey(itemId))
                {
                    mainInventoryRequiredItemCount[itemId] += mainInventory.GetItem(i).Count;
                }
                else
                {
                    mainInventoryRequiredItemCount.Add(itemId, mainInventory.GetItem(i).Count);
                }
            }
            
            //アイテム数が足りているかチェックする
            foreach (var item in requiredItemCount)
            {
                if (!mainInventoryRequiredItemCount.ContainsKey(item.Key)) return (false,null);
                
                if (mainInventoryRequiredItemCount[item.Key] < item.Value)
                {
                    return (false,null);
                }
            }
            return (true,mainInventoryRequiredItemCount);
        }

        private static IItemStack[] CalcCraftInventoryPlaceItem(ItemStackFactory itemStackFactory,IItemConfig itemConfig,ItemMessagePack[] recipe,Dictionary<int,int> mainInventoryRequiredItemCount)
        {
            //そのアイテムIDが必要なスロットがいくつあるか求める
            var requiredItemSlotCount = new Dictionary<int, int>();
            foreach (var item in recipe)
            {
                if (item.Id == ItemConst.EmptyItemId) continue;
                
                if (requiredItemSlotCount.ContainsKey(item.Id))
                {
                    requiredItemSlotCount[item.Id] += item.Count;
                }
                else
                {
                    requiredItemSlotCount.Add(item.Id, item.Count);
                }
            }
            
            
            //そのスロットに入るアイテム数を計算する
            var craftInventoryPlaceItem = new IItemStack[PlayerInventoryConst.CraftingSlotSize];
            for (int i = 0; i < PlayerInventoryConst.CraftingSlotSize; i++)
            {
                var id = recipe[i].Id;
                if (id == ItemConst.EmptyItemId)
                {
                    craftInventoryPlaceItem[i] = itemStackFactory.CreatEmpty();
                    continue;
                }
                
                //一旦あまりを考慮しないアイテム数を計算する
                //メインインベントリに入っているアイテム数 / 必要とするスロット数 = スロットに入るアイテム数　となる
                var count = mainInventoryRequiredItemCount[id] / requiredItemSlotCount[id];
                count = Math.Clamp(count,0, itemConfig.GetItemConfig(id).MaxStack);
                
                craftInventoryPlaceItem[i] = itemStackFactory.Create(id,count);
            }
            
            
            //あまり分を足す
            //アイテムIDのループを回し、一番最初にそのアイテムIDが入っているスロットを探す
            //そのスロットにあまりを入れる
            foreach (var id in requiredItemSlotCount.Keys)
            {
                for (int i = 0; i < PlayerInventoryConst.CraftingSlotSize; i++)
                {
                    if (id != craftInventoryPlaceItem[i].Id) continue;
                    
                    var remainder = mainInventoryRequiredItemCount[id] % requiredItemSlotCount[id];
                    var count = craftInventoryPlaceItem[i].Count + remainder;
                    count = Math.Clamp(count,0, itemConfig.GetItemConfig(id).MaxStack);
                    
                    craftInventoryPlaceItem[i] = itemStackFactory.Create(id,count);
                    break;
                }
            }

            return craftInventoryPlaceItem;
        }

        private static void MoveRecipeToCraftInventory(ItemStackFactory itemStackFactory,IOpenableInventory main,IOpenableInventory craft,IItemStack[] moveItem)
        {
            for (int i = 0; i < PlayerInventoryConst.CraftingSlotSize; i++)
            {
                //メインインベントリから欲しいアイテムを収集してくる
                if (moveItem[i].Id == ItemConst.EmptyItemId)  continue; 
                
                var collectedItem = itemStackFactory.CreatEmpty();
                for (int j = 0; j < PlayerInventoryConst.MainInventorySize; j++)
                {
                    if (main.GetItem(j).Id != moveItem[i].Id) continue;
                    //入ってるアイテムが目的のアイテムなので収集する
                    var addedItemResult = collectedItem.AddItem(main.GetItem(i));
                    
                    //足した結果アイテムが足りなかったらそのまま続ける
                    if (addedItemResult.ProcessResultItemStack.Count < moveItem[i].Count)
                    {
                        collectedItem = addedItemResult.ProcessResultItemStack;
                        main.SetItem(j,addedItemResult.RemainderItemStack);
                        continue;
                    }
                    //ピッタリだったらそのまま終了
                    if (addedItemResult.ProcessResultItemStack.Count == moveItem[i].Count)
                    {
                        main.SetItem(j,addedItemResult.RemainderItemStack);
                        break;
                    }
                    
                    //多かったら余りをメインインベントリに戻す
                    //本来入れるべきアイテムと、実際に入った数の差を計算
                    var reminderCount = addedItemResult.ProcessResultItemStack.Count - moveItem[i].Count;
                    //メインインベントリに入れる分のアイテムす　差分とあまりの数を足す
                    var mainItemCount = reminderCount + addedItemResult.RemainderItemStack.Count;
                    
                    var item = itemStackFactory.Create(moveItem[i].Id, mainItemCount);
                    main.SetItem(j,item);
                }
                
                
                //クラフトインベントリに入れる
                craft.SetItem(i,moveItem[i]);
            }
        }
    }
    
    [MessagePackObject(keyAsPropertyName :true)]
    public class SetRecipeCraftingInventoryProtocolMessagePack : ProtocolMessagePackBase
    {
        public SetRecipeCraftingInventoryProtocolMessagePack(int playerId,ItemMessagePack[] recipe)
        {
            Tag = SetRecipeCraftingInventoryProtocol.Tag;
            Recipe = recipe;
        }
        
        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public SetRecipeCraftingInventoryProtocolMessagePack() { }

        public ItemMessagePack[] Recipe { get; set; }
        public int PlayerId { get; set; }

    }
}