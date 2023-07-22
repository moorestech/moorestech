using System;
using UnityEngine;

namespace MainGame.UnityView.UI.Util
{
    public class GameObjectEnterExplainer : MonoBehaviour
    {
        /// <summary>
        /// カーソルに表示するテキスト
        /// </summary>
        [Multiline(5)]
        [SerializeField] private string currentText;
        /// <summary>
        /// 表示するかどうか
        /// </summary>
        [SerializeField] private bool displayEnable = true;
         
        [SerializeField] private int fontSize = IMouseCursorExplainer.DefaultFontSize;
        

        public void OnCursorEnter()
        {
            if (displayEnable)
            {
                MouseCursorExplainer.Instance.Show(currentText,fontSize);
            }
        }

        public void OnCursorExit()
        {
            MouseCursorExplainer.Instance.Hide();
        }
    }
}