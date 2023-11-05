using TMPro;
using UnityEngine;

namespace MainGame.UnityView.UI.Tutorial
{
    /// <summary>
    ///     チュートリアルなどに使用する、マウスカーソルに説明文を表示するクラス
    /// </summary>
    public class MouseCursorDescription : MonoBehaviour
    {
        [SerializeField] private RectTransform descriptionBox;
        [SerializeField] private TMP_Text descriptionText;

        /// <summary>
        ///     簡易的に使えることがこのクラスの理念なので、簡単にアクセスできるようにしておく
        ///     何か良くないこと（同時編集によるバグとか）が発生したら、セマフォにするか都度生成するかなどの対策を考える
        /// </summary>
        public static MouseCursorDescription Instance { get; private set; }

        private void Start()
        {
            Instance = this;
        }

        private void Update()
        {
            // マウスカーソルの位置に説明文を表示する
            var mousePosition = Input.mousePosition;
            descriptionBox.position = mousePosition;
        }

        public void SetDescription(string text)
        {
            descriptionText.text = text;
        }

        public void SetEnable(bool enable)
        {
            descriptionBox.gameObject.SetActive(enable);
        }
    }
}