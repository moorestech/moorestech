using UnityEngine;

namespace MainGame.UnityView.UI.UIState.UIObject
{
    public class CraftInventoryObject : MonoBehaviour
    {
        public void SetActive(bool isActive)
        {
            gameObject.SetActive(isActive);
        }
    }
}