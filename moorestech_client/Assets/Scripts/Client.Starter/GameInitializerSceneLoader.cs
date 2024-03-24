using Client.Common;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Client.Starter
{
    /// <summary>
    /// <see cref="GameInitializerSceneLoader"/>があるシーンの場合はゲーム初期化シーンをロードする
    /// </summary>
    public class GameInitializerSceneLoader : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        public static void Init()
        {
            var loader = FindObjectOfType<GameInitializerSceneLoader>();
            if (loader != null)
            {
                SceneManager.LoadScene(SceneConstant.GameInitializerSceneName);
            }
        }
    }
}