using MainGame.Network.Send;
using MainGame.UnityView.Control;
using MainGame.UnityView.UI.UIState.UIObject;
using VContainer.Unity;

namespace MainGame.Presenter.Inventory.Send
{
    public class CraftPacketSend : IInitializable
    {
        private readonly SendCraftProtocol _sendCraftProtocol;

        public CraftPacketSend(PlayerInventoryObject playerInventoryObject,SendCraftProtocol sendCraftProtocol)
        {
            _sendCraftProtocol = sendCraftProtocol;
            playerInventoryObject.OnResultSlotClick += OnCraft;
        }

        private void OnCraft()
        {
            if (InputManager.UI.AllCraft.GetKey)
            {
                _sendCraftProtocol.SendAllCraft();
                return;
            }

            if (InputManager.UI.OneStackCraft.GetKey)
            {
                _sendCraftProtocol.SendOneStackCraft();
                return;
            }
            
            _sendCraftProtocol.SendOneCraft();
        }

        public void Initialize() { }
    }
}