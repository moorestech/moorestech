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
            saveButton.onClick.AddListener(() =>
            {
                // サーバーの保存完了まで待つ
                // Await the server's save completion
                ClientContext.VanillaApi.Response.SaveAsync().Forget();
            });
        }
    }
}