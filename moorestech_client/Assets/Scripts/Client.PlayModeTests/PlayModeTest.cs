using System.Collections;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Client.PlayModeTests
{
    public class PlayModeTest
    {
        [UnityTest]
        public IEnumerator NewTestScriptSimplePasses()
        {
            yield return PlayModeTestUtil.LoadMainGame().ToCoroutine();
        }
    }
}
