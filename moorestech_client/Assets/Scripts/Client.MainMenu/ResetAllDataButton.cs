using System.IO;
using Game.Paths;
using UnityEngine;
using UnityEngine.UI;

namespace Client.MainMenu
{
    public class ResetAllDataButton : MonoBehaviour
    {
        [SerializeField] private Button resetAllDataButton;

        private void Start()
        {
            resetAllDataButton.onClick.AddListener(ResetAllData);
        }

        private void ResetAllData()
        {
            // セーブデータディレクトリを丸ごと削除 / Delete the entire save data directory
            var saveDirectory = GameSystemPaths.SaveFileDirectory;
            if (Directory.Exists(saveDirectory)) Directory.Delete(saveDirectory, true);

            // PlayerPrefsも全削除して初期状態に戻す / Delete all PlayerPrefs to restore initial state
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();

            Debug.Log("全データをリセットしました");
        }
    }
}
