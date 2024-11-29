using Client.Game.InGame.UI.Inventory.Element;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Client.Game.InGame.UI.Inventory.Block
{
    public class CraftChainerMainComputerSelectRequestItemModal : MonoBehaviour
    {
        [SerializeField] private ItemSlotObject itemSlotObjectPrefab;
        
        [SerializeField] private RectTransform itemsParent;
        [SerializeField] private TMP_InputField countInputField;
        
        [SerializeField] private Button okButton;
        [SerializeField] private Button cancelButton;

    }
}