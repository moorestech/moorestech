using UnityEngine;

namespace Client.Game.InGame.UI.Util
{
    public class GameObjectTooltipTarget : MonoBehaviour
    {
        /// <summary>
        ///     カーソルに表示するテキストのキー
        /// </summary>
        [SerializeField] private string textKey;
        
        /// <summary>
        ///     表示するかどうか
        /// </summary>
        [SerializeField] private bool displayEnable = true;
        
        [SerializeField] private int fontSize = IMouseCursorTooltip.DefaultFontSize;
        
        
        public void OnCursorEnter()
        {
            if (displayEnable) MouseCursorTooltip.Instance.Show(textKey, fontSize);
        }
        
        public void OnCursorExit()
        {
            MouseCursorTooltip.Instance.Hide();
        }
    }
}