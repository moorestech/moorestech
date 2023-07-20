using System;
using TMPro;
using UnityEngine;

namespace MainGame.UnityView.UI.Util
{
    public interface IMouseCursorExplainer
    {
        public void Hide();
        public void Show(string description);

    }
    /// <summary>
    /// マウスカーソルのそばにアイテム名やTips、その他文章を表示するシステム
    /// </summary>
    public class MouseCursorExplainer : MonoBehaviour,IMouseCursorExplainer
    {
        [SerializeField] private GameObject itemNameBar;
        [SerializeField] private TMP_Text itemName;
        
        /// <summary>
        /// 基本的には使わない
        /// Controller系のみアクセスしてOKって感じ
        /// </summary>
        [Obsolete("基本は使わないでね コントローラーを介して使ってね")]
        public static IMouseCursorExplainer Instance { get; private set; }

        private void Awake()
        {
            Instance = this;
        }
        
        public void Show(string description)
        {
            itemNameBar.SetActive(true);
            itemName.text = description;
        }

        
        public void Hide()
        {
            itemNameBar.SetActive(false);
        }
    }
}