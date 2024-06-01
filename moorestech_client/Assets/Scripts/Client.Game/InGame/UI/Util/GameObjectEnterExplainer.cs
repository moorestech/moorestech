using UnityEngine;

namespace Client.Game.InGame.UI.Util
{
    public class GameObjectEnterExplainer : MonoBehaviour
    {
        /// <summary>
        ///     カーソルに表示するテキストのキー
        /// </summary>
        [SerializeField] private string textKey;

        /// <summary>
        ///     表示するかどうか
        /// </summary>
        [SerializeField] private bool displayEnable = true;

        [SerializeField] private int fontSize = IMouseCursorExplainer.DefaultFontSize;


        public void OnCursorEnter()
        {
            if (displayEnable) MouseCursorExplainer.Instance.Show(textKey, fontSize);
        }

        public void OnCursorExit()
        {
            MouseCursorExplainer.Instance.Hide();
        }
    }
}