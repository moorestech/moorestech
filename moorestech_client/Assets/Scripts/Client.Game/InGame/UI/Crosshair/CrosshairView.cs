using UnityEngine;

namespace Client.Game.InGame.UI.Crosshair
{
    /// <summary>
    ///     FPS視点の画面中央クロスヘア
    ///     Center-screen crosshair for the first-person view
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
