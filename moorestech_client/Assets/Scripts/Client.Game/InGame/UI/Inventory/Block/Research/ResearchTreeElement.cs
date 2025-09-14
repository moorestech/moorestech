using Client.Game.InGame.UI.Challenge;
using UnityEngine;

namespace Client.Game.InGame.UI.Inventory.Block.Research
{
    public class ResearchTreeElement : MonoBehaviour, ITreeViewElement
    {
        public RectTransform RectTransform => rectTransform;
        
        [SerializeField] private RectTransform rectTransform;
    }
}