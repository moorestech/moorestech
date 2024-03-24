using Client.Game.Context;
using UnityEngine;
using UnityEngine.UI;

namespace MainGame.Control.UI.PauseMenu
{
    public class SaveButton : MonoBehaviour
    {
        [SerializeField] private Button saveButton;

        private void Start()
        {
            saveButton.onClick.AddListener(MoorestechContext.VanillaApi.SendOnly.Save);
        }
    }
}