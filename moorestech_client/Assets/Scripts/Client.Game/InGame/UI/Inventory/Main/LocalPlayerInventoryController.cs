using System;
using Client.Game.InGame.Context;
using Core.Item.Interface;
using Core.Master;
using Game.Context;
using Game.PlayerInventory.Interface;
using Server.Protocol.PacketResponse.Util.InventoryMoveUtil;

namespace Client.Game.InGame.UI.Inventory.Main
{
    public class LocalPlayerInventoryController
    {
        public ILocalPlayerInventory LocalPlayerInventory => _mainAndSubCombine;
        public IItemStack GrabInventory { get; private set; }
        
        private readonly LocalPlayerInventory _mainAndSubCombine;
        private ISubInventory _subInventory;
        
        public LocalPlayerInventoryController(ILocalPlayerInventory localPlayerInventoryMainAndSubCombine)
        {
            _mainAndSubCombine = (LocalPlayerInventory)localPlayerInventoryMainAndSubCombine;
            GrabInventory = ServerContext.ItemStackFactory.Create(new ItemId(0), 0);
        }
        
        public void MoveItem(LocalMoveInventoryType from, int fromSlot, LocalMoveInventoryType to, int toSlot, int count, bool isMoveSendData = true)
        {
            var fromInvItem = from switch
            {
                LocalMoveInventoryType.MainOrSub => LocalPlayerInventory[fromSlot],
                LocalMoveInventoryType.Grab => GrabInventory,
                _ => throw new ArgumentOutOfRangeException(nameof(from), from, null),
            };
            
            if (fromInvItem.Count < count) return;
            
            SetInventory();
            
            if (isMoveSendData) SendMoveItemData();
            
            #region InternalMethod
            
            void SetInventory()
            {
                var itemStackFactory = ServerContext.ItemStackFactory;
                
                var toInvItem = to switch
                {
                    LocalMoveInventoryType.MainOrSub => LocalPlayerInventory[toSlot],
                    LocalMoveInventoryType.Grab => GrabInventory,
                    _ => throw new ArgumentOutOfRangeException(nameof(to), to, null),
                };
                var moveItem = itemStackFactory.Create(fromInvItem.Id, count);
                
                var add = toInvItem.AddItem(moveItem);
                switch (to)
                {
                    case LocalMoveInventoryType.MainOrSub:
                        _mainAndSubCombine[toSlot] = add.ProcessResultItemStack;
                        break;
                    case LocalMoveInventoryType.Grab:
                        GrabInventory = add.ProcessResultItemStack;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(to), to, null);
                }
                
                var fromItemCount = fromInvItem.Count - count + add.RemainderItemStack.Count;
                var fromItem = itemStackFactory.Create(fromInvItem.Id, fromItemCount);
                switch (from)
                {
                    case LocalMoveInventoryType.Grab:
                        GrabInventory = fromItem;
                        break;
                    default:
                        _mainAndSubCombine[fromSlot] = fromItem;
                        break;
                }
            }
            
            void SendMoveItemData()
            {
                var fromInfo = GetServerInventoryInfo(from, fromSlot);
                var toInfo = GetServerInventoryInfo(to, toSlot);
                ClientContext.VanillaApi.SendOnly.ItemMove(count, ItemMoveType.SwapSlot, fromInfo, fromSlot, toInfo, toSlot);
            }
            
            ItemMoveInventoryInfo GetServerInventoryInfo(LocalMoveInventoryType localType, int localSlot)
            {
                return localType switch
                {
                    LocalMoveInventoryType.MainOrSub => localSlot < PlayerInventoryConst.MainInventorySize ? new ItemMoveInventoryInfo(ItemMoveInventoryType.MainInventory) : _subInventory.ItemMoveInventoryInfo,
                    LocalMoveInventoryType.Grab => new ItemMoveInventoryInfo(ItemMoveInventoryType.GrabInventory),
                    _ => throw new ArgumentOutOfRangeException(nameof(localType), localType, null),
                };
            }
            
            #endregion
        }
        
        public void SetGrabItem(IItemStack itemStack)
        {
            GrabInventory = itemStack;
        }
        
        public void SetMainItem(int slot, IItemStack itemStack)
        {
            _mainAndSubCombine[slot] = itemStack;
        }
        
        public void SetSubInventory(ISubInventory subInventory)
        {
            _mainAndSubCombine.SetSubInventory(subInventory);
            _subInventory = subInventory;
        }
    }
    
    public enum LocalMoveInventoryType
    {
        MainOrSub, //メインインベントリとサブインベントリの両方（ドラッグアンドドロップなどでは統一して扱うから
        Grab, //持ち手のインベントリ
    }
}