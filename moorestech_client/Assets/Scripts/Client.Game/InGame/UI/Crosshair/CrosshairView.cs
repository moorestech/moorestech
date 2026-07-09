using UnityEngine;

namespace Client.Game.InGame.UI.Crosshair
{
    /// <summary>
    ///     FPS建設モードの画面中央クロスヘア
    ///     Center-screen crosshair for the FPS build mode
    /// </summary>
    public class CrosshairView : MonoBehaviour
    {
        private static CrosshairView _instance;
        public static CrosshairView Instance => _instance;

        [SerializeField] private GameObject dotObject;

        private void Awake()
        {
            _instance = this;
            dotObject.SetActive(false);
        }

        public void SetVisible(bool visible)
        {
            dotObject.SetActive(visible);
        }
    }
}
