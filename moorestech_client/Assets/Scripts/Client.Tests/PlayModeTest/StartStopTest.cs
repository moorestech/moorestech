using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.UI.Challenge;
using Client.Tests.PlayModeTest.Util;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using Server.Boot;
using Tools.Logging;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests
{
    public class StartStopTest
    {
        [UnityTest]
        public IEnumerator PlayAndStartStop()
        {
            Debug.Log("[StartStopTest] Step 2: Enter play mode (First time, expectDomainReload: true)");
            yield return new EnterPlayMode(expectDomainReload: true);

            Debug.Log("[StartStopTest] Step 3: Load game (First Login)");
            yield return LoadGame("First Login").ToCoroutine();

            var logs = LogBuffer.EnumerateEditorConsoleEntries().ToList();
            Assert.IsTrue(logs.Count > 0, "There are no logs after first game load.");
            
            Debug.Log("[StartStopTest] Step 4: Exit play mode (First time)");
            yield return new ExitPlayMode();

            Debug.Log("[StartStopTest] Step 5: Clear logs");
            Debug.ClearDeveloperConsole();

            Debug.Log("[StartStopTest] Step 6: Wait for 3 seconds");
            yield return new WaitForSeconds(3f);

            Debug.Log("[StartStopTest] Step 7: Enter play mode (Second time, expectDomainReload: false)");
            
            
            yield return new EnterPlayMode(expectDomainReload: true);
            
            var errorLogs = LogBuffer.EnumerateEditorConsoleEntries()
                .Where(entry => entry.type is LogType.Error or LogType.Exception)
                .ToList();
            
            Assert.IsTrue(errorLogs.Count >= 2, $"Expected at least 2 error logs after entering play mode again, but found {errorLogs.Count}.\nLogs:\n{string.Join("\n", errorLogs.Select(e => e.message))}");
            
            
            #region Internal
            
            async UniTask LoadGame(string loadTitle)
            {
                try
                {
                    var serverDirectory = ServerDirectory.GetDirectory();
                    await PlayModeTestUtil.LoadMainGame(serverDirectory);
                    
                    await UniTask.Delay(1000);
                }
                catch (Exception e)
                {
                    Debug.LogError(e);
                    Assert.Fail($"Title:{loadTitle} Exception occurred during LoadGame: {e.Message}\n{e.StackTrace}");
                }
            }
            
            #endregion
        }
    }
}