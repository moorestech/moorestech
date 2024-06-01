using Client.Game.InGame.Context;
using UnityEngine;
using UnityEngine.UI;

namespace Client.Game.InGame.Presenter.PauseMenu
{
    public class SaveButton : MonoBehaviour
    {
        [SerializeField] private Button saveButton;

        private void Start()
        {
            saveButton.onClick.AddListener(ClientContext.VanillaApi.SendOnly.Save);
        }
    }
}