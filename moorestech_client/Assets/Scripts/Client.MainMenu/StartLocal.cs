using Client.Starter;
using UnityEngine;
using UnityEngine.UI;

namespace Client.MainMenu
{
    public class StartLocal : MonoBehaviour
    {
        [SerializeField] private Button startLocalButton;

        private void Start()
        {
            startLocalButton.onClick.AddListener(LocalGameLauncher.StartLocalGame);
        }
    }
}
