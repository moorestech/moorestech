using Constant;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Client.Starter
{
    public class GameInitializerSceneLoader : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Init()
        {
            if (SceneManager.GetActiveScene().name == SceneConstant.MainGameSceneName)
            {
                SceneManager.LoadScene(SceneConstant.GameInitializerSceneName);
            }
        }
    }
}