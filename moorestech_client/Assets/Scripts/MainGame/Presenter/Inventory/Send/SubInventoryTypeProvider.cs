using MainGame.UnityView.Control.MouseKeyboard;
using MainGame.UnityView.UI.UIState;
using Server.Protocol.PacketResponse.Util.InventoryMoveUtil;
using UnityEngine;

namespace MainGame.Presenter.Inventory.Send
{
    public class SubInventoryTypeProvider
    {
        private readonly IBlockClickDetect _blockClickDetect;
        private Vector2Int _blockPos;

        public SubInventoryTypeProvider(UIStateControl uiStateControl, IBlockClickDetect blockClickDetect)
        {
            uiStateControl.OnStateChanged += OnStateChanged;
            _blockClickDetect = blockClickDetect;
        }

        public ItemMoveInventoryType CurrentSubInventory { get; private set; }

        public Vector2Int BlockPos => _blockPos;


        private void OnStateChanged(UIStateEnum state)
        {
            //今開いているサブインベントリのタイプを設定する
            CurrentSubInventory = state switch
            {
                //プレイヤーインベントリを開いているということは、サブインベントリはCraftInventoryなのでそれを設定する
                UIStateEnum.PlayerInventory => ItemMoveInventoryType.CraftInventory,
                UIStateEnum.BlockInventory => ItemMoveInventoryType.BlockInventory,
                _ => CurrentSubInventory
            };

            //ブロックだった場合のために現在の座標を取得しておく
            _blockClickDetect.TryGetCursorOnBlockPosition(out _blockPos);
        }
    }
}