using System.Threading.Tasks;
using Client.Common;
using Client.Starter;
using Cysharp.Threading.Tasks;
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
            await PlayModeTestUtil.LoadMainGame();
        }
    }
}
