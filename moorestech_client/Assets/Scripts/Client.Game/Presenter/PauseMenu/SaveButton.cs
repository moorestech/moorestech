using Client.Network.NewApi;
using MainGame.Network.Send;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace MainGame.Control.UI.PauseMenu
{
    public class SaveButton : MonoBehaviour
    {
        [SerializeField] private Button saveButton;

        private void Start()
        {
            saveButton.onClick.AddListener(VanillaApi.SendOnly.Save);
        }
    }
}