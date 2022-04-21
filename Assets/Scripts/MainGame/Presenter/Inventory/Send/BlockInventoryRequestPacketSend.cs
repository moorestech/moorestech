using MainGame.Model.Network.Send;
using MainGame.Network.Send;
using MainGame.UnityView.Control.MouseKeyboard;
using MainGame.UnityView.UI.UIState;
using UnityEngine;
using VContainer.Unity;

namespace MainGame.Presenter.Inventory.Send
{
    public class BlockInventoryRequestPacketSend : IInitializable
    {
        private readonly IBlockClickDetect _blockClickDetect;
        private readonly RequestBlockInventoryProtocol _requestBlockInventoryProtocol;
        private readonly SendBlockInventoryOpenCloseControlProtocol _blockInventoryOpenCloseControlProtocol;

        private Vector2Int _openBlockPos;

        public BlockInventoryRequestPacketSend(IBlockClickDetect blockClickDetect, BlockInventoryState blockInventoryState, RequestBlockInventoryProtocol requestBlockInventoryProtocol, SendBlockInventoryOpenCloseControlProtocol blockInventoryOpenCloseControlProtocol)
        {
            _blockClickDetect = blockClickDetect;
            _requestBlockInventoryProtocol = requestBlockInventoryProtocol;
            _blockInventoryOpenCloseControlProtocol = blockInventoryOpenCloseControlProtocol;

            blockInventoryState.OnOpenBlockInventory += OpenInventory;
            blockInventoryState.OnCloseBlockInventory += CloseInventory;
        }

        private void OpenInventory()
        {
            //その位置のブロックインベントリを取得するパケットを送信する
            _openBlockPos = _blockClickDetect.GetClickPosition();
            
            _requestBlockInventoryProtocol.Send(_openBlockPos.x, _openBlockPos.y);
            _blockInventoryOpenCloseControlProtocol.Send(_openBlockPos.x,_openBlockPos.y,true);
        }

        private void CloseInventory()
        {
            _blockInventoryOpenCloseControlProtocol.Send(_openBlockPos.x,_openBlockPos.y,false);
        }

        public void Initialize() { }
    }
}