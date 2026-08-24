// [uGUI廃止Phase1] uGUI描画は恒久停止・ビューは未メンテ。ただし本クラスは外部（Web UIブリッジ等）から参照中のため削除前に整理が必要（docs/webui/ugui-retirement-plan.md）
// [uGUI retirement Phase1] uGUI rendering is permanently disabled and the view is unmaintained, but this class is still referenced externally (e.g. Web UI bridge); untangle before deletion (docs/webui/ugui-retirement-plan.md)
using System;
using Client.Game.InGame.Context;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace Client.Game.InGame.Presenter.PauseMenu
{
    public class SaveButton : MonoBehaviour
    {
        [SerializeField] private Button saveButton;
        
        private void Start()
        {
            saveButton.onClick.AddListener(Save);
        }

        public void Save()
        {
            // 応答は要求番号のみで待ち合わせ先が無いため、失敗のログだけ観測する
            // The response carries only the generation and nobody waits on it, so just observe failures
            ClientContext.VanillaApi.Response.Save(default).Forget(LogSaveFailure);
        }

        private void LogSaveFailure(Exception exception)
        {
            Debug.LogError($"セーブ要求に失敗しました: {exception.GetType()} {exception.Message}\n{exception.StackTrace}");
        }
    }
}
