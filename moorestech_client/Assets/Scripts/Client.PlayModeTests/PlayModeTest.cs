using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine.InputSystem;

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
