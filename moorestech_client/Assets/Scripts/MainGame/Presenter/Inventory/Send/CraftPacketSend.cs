using MainGame.Network.Send;
using MainGame.UnityView.Control;
using MainGame.UnityView.UI.UIState.UIObject;
using VContainer.Unity;

namespace MainGame.Presenter.Inventory.Send
{
    public class CraftPacketSend : IInitializable
    {
        private readonly SendCraftProtocol _sendCraftProtocol;

        public CraftPacketSend(CraftInventoryObjectCreator craftInventoryObjectCreator, SendCraftProtocol sendCraftProtocol)
        {
            _sendCraftProtocol = sendCraftProtocol;
            craftInventoryObjectCreator.OnResultSlotClick += OnCraft;
        }

        public void Initialize()
        {
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
    }
}