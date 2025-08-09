using System.Threading.Tasks;
using Client.PlayModeTests.Util;
using NUnit.Framework;

namespace Client.PlayModeTests
{
    public class PlayModeTest
    {
        [Test]
        public async Task  NewTestScriptSimplePasses()
        {
            await PlayModeTestUtil.LoadMainGame();
            
            //await PlayModeTestUtil.GiveItem();
        }
    }
}
