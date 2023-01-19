using MainGame.Network.Send;
using MainGame.UnityView.UI.UIState;
using VContainer.Unity;

namespace MainGame.Presenter.Inventory.Send
{
    public class PlayerInventoryRequestPacketSend : IInitializable
    {
        private readonly RequestPlayerInventoryProtocol _requestPlayerInventoryProtocol;

        public PlayerInventoryRequestPacketSend(PlayerInventoryState playerInventoryState, RequestPlayerInventoryProtocol requestPlayerInventoryProtocol)
        {
            _requestPlayerInventoryProtocol = requestPlayerInventoryProtocol;
            playerInventoryState.OnOpenInventory += OpenInventory;
        }

        private void OpenInventory()
        {
            //インベントリの取得パケットの送信
            _requestPlayerInventoryProtocol.Send();
        }
        public void Initialize() { }
    
    }
}