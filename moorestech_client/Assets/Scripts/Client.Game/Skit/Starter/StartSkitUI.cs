using Client.Game.InGame.Player;
using UniRx;
using UnityEngine;

namespace Client.Game.Skit.Starter
{
    public class StartSkitUI : MonoBehaviour
    {
        [SerializeField] private GameObject startStoryPanel;
        
        private void Start()
        {
            PlayerSystemContainer.Instance.PlayerSkitStarterDetector.OnStateChange.Subscribe(ShowStartStoryUI).AddTo(this);
        }
        
        private void ShowStartStoryUI(bool enable)
        {
            startStoryPanel.SetActive(enable);
        }
    }
}