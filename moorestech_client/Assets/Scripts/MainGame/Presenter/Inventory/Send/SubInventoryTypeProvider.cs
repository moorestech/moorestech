using MainGame.UnityView.Control.MouseKeyboard;
using MainGame.UnityView.UI.UIState;
using Server.Protocol.PacketResponse.Util.InventoryMoveUitl;
using UnityEngine;

namespace MainGame.Presenter.Inventory.Send
{
    public class SubInventoryTypeProvider
    {
        public ItemMoveInventoryType CurrentSubInventory => _currentSubInventoryType;
        private ItemMoveInventoryType _currentSubInventoryType;
        public Vector2Int BlockPos => _blockPos;
        private Vector2Int _blockPos;
        
        
        private readonly IBlockClickDetect _blockClickDetect;

        public SubInventoryTypeProvider(UIStateControl uiStateControl,IBlockClickDetect blockClickDetect)
        {
            uiStateControl.OnStateChanged += OnStateChanged;
            _blockClickDetect = blockClickDetect;
        }
        
        
        private void OnStateChanged(UIStateEnum state)
        {
            //今開いているサブインベントリのタイプを設定する
            _currentSubInventoryType = state switch
            {
                //プレイヤーインベントリを開いているということは、サブインベントリはCraftInventoryなのでそれを設定する
                UIStateEnum.PlayerInventory => ItemMoveInventoryType.CraftInventory,
                UIStateEnum.BlockInventory => ItemMoveInventoryType.BlockInventory,
                _ => _currentSubInventoryType
            };
            
            //ブロックだった場合のために現在の座標を取得しておく
            _blockClickDetect.TryGetCursorOnBlockPosition(out _blockPos);
        }
    }
}