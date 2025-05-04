using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Client.Game.InGame.UI.ContextMenu
{
    public class ContextMenuViewItem : MonoBehaviour
    {
        [SerializeField] private TMP_Text barTitle;
        [SerializeField] private Button itemButton;
        
        private ContextMenuBarInfo _contextMenuBarInfo;
        private void Awake()
        {
            itemButton.onClick.AddListener( () =>
            {
                if (_contextMenuBarInfo == null) return;
                _contextMenuBarInfo.OnClick.Invoke();
            });
        }
        
        
        public void Initialize(ContextMenuBarInfo contextMenuBarInfo)
        {
            _contextMenuBarInfo = contextMenuBarInfo;
            barTitle.text = contextMenuBarInfo.Title;
        }
    }
}