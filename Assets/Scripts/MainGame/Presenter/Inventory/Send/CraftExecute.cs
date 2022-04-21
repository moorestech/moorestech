using MainGame.Network.Send;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace MainGame.Presenter.Inventory.Send
{
    public class CraftExecute : MonoBehaviour
    {
        [SerializeField] Button craftExecuteButton;

        [Inject]
        public void Construct(SendCraftProtocol sendCraftProtocol)
        {
            craftExecuteButton.onClick.AddListener(sendCraftProtocol.Send);
        }
    }
}