using UnityEngine;
using UnityEngine.UI;

namespace MainGame.UnityView.UI.Inventory.View.HotBar
{
    public class SelectHotBarView : MonoBehaviour
    {
        [SerializeField] private Image selectImage;
        [SerializeField] private HotBarItemView hotBarItemView;

        public void SetSelect(int selectIndex)
        {
            selectImage.transform.position = hotBarItemView.Slots[selectIndex].transform.position;
        }
        
        
        public void SetActiveSelectHotBar(bool isActive)
        {
            selectImage.gameObject.SetActive(isActive);
        }
    }
}