using System.Threading;
using Client.Common;
using Client.Game.InGame.Context;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Client.Game.InGame.Presenter.PauseMenu
{
    //ゲームが終了したときかメインメニューに戻るときはサーバーを終了させます
    public class BackToMainMenu : MonoBehaviour
    {
        [SerializeField] private Button backToMainMenuButton;
        
        private void Start()
        {
            backToMainMenuButton.onClick.AddListener(Back);
        }
        
        private void OnDestroy()
        {
            Disconnect();
        }
        
        private void OnApplicationQuit()
        {
            Disconnect();
        }
        
        private void Back()
        {
            Disconnect();
            SceneManager.LoadScene(SceneConstant.MainMenuSceneName);
        }
        
        
        private void Disconnect()
        {
            ClientContext.VanillaApi.SendOnly.Save();
            Thread.Sleep(50);
            ClientContext.VanillaApi.Disconnect();
        }
    }
}