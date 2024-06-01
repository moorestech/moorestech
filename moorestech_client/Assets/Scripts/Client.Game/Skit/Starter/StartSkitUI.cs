using UnityEngine;

namespace Client.Game.Skit.Starter
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