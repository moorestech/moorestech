// [uGUI廃止Phase1] uGUI描画は恒久停止・ビューは未メンテ。ただし本クラスは外部（Web UIブリッジ等）から参照中のため削除前に整理が必要（docs/webui/ugui-retirement-plan.md）
// [uGUI retirement Phase1] uGUI rendering is permanently disabled and the view is unmaintained, but this class is still referenced externally (e.g. Web UI bridge); untangle before deletion (docs/webui/ugui-retirement-plan.md)
using System.Threading;
using Client.Common;
using Client.Game.Common;
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
        
        public void Back()
        {
            Disconnect();
            SceneManager.LoadScene(SceneConstant.MainMenuSceneName);
        }
        
        
        private void Disconnect()
        {
            ClientContext.VanillaApi.SendOnly.Save();
            Thread.Sleep(50);
            ClientContext.VanillaApi.Disconnect();
            // Web UI と内蔵サーバーへゲーム終了を通知する。内蔵サーバーは保存を消化してから自壊する
            // Notify the Web UI and the embedded server; the server folds itself after flushing pending saves
            GameShutdownEvent.FireGameShutdown();
        }
    }
}
