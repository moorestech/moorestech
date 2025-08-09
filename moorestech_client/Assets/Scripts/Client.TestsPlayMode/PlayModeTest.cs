using System.Threading.Tasks;
using Client.PlayModeTests.Util;
using Cysharp.Threading.Tasks;
using NUnit.Framework;

namespace Client.PlayModeTests
{
    public class PlayModeTest
    {
        [Test]
        public async Task  NewTestScriptSimplePasses()
        {
            await PlayModeTestUtil.LoadMainGame();
            
            await UniTask.Delay(60000);
            //await PlayModeTestUtil.GiveItem();
        }
    }
}
