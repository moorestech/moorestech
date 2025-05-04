using UnityEngine;

namespace Client.Game.InGame.UI.ContextMenu
{
    public class MouseCursorContextMenu : MonoBehaviour
    {
        
        public static MouseCursorContextMenu Instance { get; private set; }
        
        private void Awake()
        {
            Instance = this;
        }
    }
}