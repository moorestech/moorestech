using MainGame.Network.Send;
using MainGame.UnityView.UI.UIState;
using UnityEngine;
using VContainer.Unity;

namespace MainGame.Presenter.Inventory.Send
{
    public class BlockInventoryRequestPacketSend : IInitializable
    {
        private readonly RequestBlockInventoryProtocol _requestBlockInventoryProtocol;
        private readonly SendBlockInventoryOpenCloseControlProtocol _blockInventoryOpenCloseControlProtocol;

        private Vector2Int _openBlockPos;

        public BlockInventoryRequestPacketSend(BlockInventoryState blockInventoryState, RequestBlockInventoryProtocol requestBlockInventoryProtocol, SendBlockInventoryOpenCloseControlProtocol blockInventoryOpenCloseControlProtocol)
        {
            _requestBlockInventoryProtocol = requestBlockInventoryProtocol;
            _blockInventoryOpenCloseControlProtocol = blockInventoryOpenCloseControlProtocol;

            blockInventoryState.OnOpenBlockInventory += OpenInventory;
            blockInventoryState.OnCloseBlockInventory += CloseInventory;
        }

        private void OpenInventory(Vector2Int blockPos)
        {
            //その位置のブロックインベントリを取得するパケットを送信する
            _openBlockPos = blockPos;
            
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