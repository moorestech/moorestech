using System.Threading;
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
            // このオブジェクト破棄に追従する CancellationToken を渡し、Forget の onError で失敗をログに残す
            // Use a CancellationToken tied to this object and report failures via Forget's onError callback
            var ct = this.GetCancellationTokenOnDestroy();
            saveButton.onClick.AddListener(() =>
            {
                ClientContext.VanillaApi.Response.SaveAsync(ct)
                    .Forget(e => Debug.LogError($"[SaveButton] Save failed: {e}"));
            });
        }
    }
}