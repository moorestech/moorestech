using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Client.PlayModeTests
{
    public class PlayModeTest : InputTestFixture
    {
        [Test]
        public async Task  NewTestScriptSimplePasses()
        {
            // 最初のSceneをロード（"Scenes in Build" に含まれている必要があります）
            await SceneManager.LoadSceneAsync("MainGame");
            
        }
    }
}
