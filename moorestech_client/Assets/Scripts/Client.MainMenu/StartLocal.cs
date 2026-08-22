using Client.Common;
using Client.Starter;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;

namespace Client.MainMenu
{
    public class StartLocal : MonoBehaviour
    {
        [SerializeField] private Button startLocalButton;


        private void Start()
        {
            startLocalButton.onClick.AddListener(() => ConnectLocalServer().Forget());
        }
        
        private async UniTask ConnectLocalServer()
        {
            Debug.Log("Server started");
            
            SceneManager.sceneLoaded += OnMainGameSceneLoaded;
            SceneManager.LoadScene(SceneConstant.GameInitializerSceneName);
        }
        
        private void OnMainGameSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            SceneManager.sceneLoaded -= OnMainGameSceneLoaded;
            var starter = FindObjectOfType<InitializeScenePipeline>();

            starter.SetProperty(InitializeProprieties.CreateLocalServer(PlayerPrefs.GetInt(PlayerPrefsKeys.PlayerIdKey)));
        }
    }
}