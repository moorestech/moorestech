using System.IO;
using Game.Paths;
using UnityEngine;
using UnityEngine.UI;

namespace Client.MainMenu.PopUp
{
    public class ResetAllDataConfirmPopup : MonoBehaviour
    {
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button cancelButton;

        private void Start()
        {
            confirmButton.onClick.AddListener(ResetAllData);
            cancelButton.onClick.AddListener(Close);
        }

        public void Open()
        {
            gameObject.SetActive(true);
        }

        private void Close()
        {
            gameObject.SetActive(false);
        }

        private void ResetAllData()
        {
            // ゲームデータディレクトリを丸ごと削除 / Delete the entire game data directory
            var gameSystemDirectory = GameSystemPaths.GameSystemDirectory;
            if (Directory.Exists(gameSystemDirectory)) Directory.Delete(gameSystemDirectory, true);

            // PlayerPrefsも全削除して初期状態に戻す / Delete all PlayerPrefs to restore initial state
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();

            Debug.Log("全データをリセットしました");
            Close();
        }
    }
}
