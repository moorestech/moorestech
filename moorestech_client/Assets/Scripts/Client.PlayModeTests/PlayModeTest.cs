using System.Threading.Tasks;
using NUnit.Framework;

namespace Client.PlayModeTests
{
    public class PlayModeTest
    {
        [Test]
        public async Task  NewTestScriptSimplePasses()
        {
            await PlayModeTestUtil.LoadMainGame();
        }
    }
}
