using UnityEngine;

namespace Client.Game.Story.UI
{
    public class StartSkitUI : MonoBehaviour
    {
        [SerializeField] private GameObject startStoryPanel;

        public void ShowStartStoryUI(bool enable)
        {
            startStoryPanel.SetActive(enable);
        }
    }
}