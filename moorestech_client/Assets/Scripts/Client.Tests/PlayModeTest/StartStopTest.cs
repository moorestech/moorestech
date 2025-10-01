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
            
            AssertNoErrorLog("Before entering play mode second time");
            
            yield return new EnterPlayMode(expectDomainReload: true);
            
            AssertNoErrorLog("After entering play mode second time");

            Debug.Log("[StartStopTest] Step 8: Load game (Second Login)");
            yield return LoadGame("Second Login").ToCoroutine();

            Debug.Log("[StartStopTest] Step 9: Check for error logs");
            AssertNoErrorLog("After second game load");

            Debug.Log("[StartStopTest] Step 10: Exit play mode (Second time)");
            yield return new ExitPlayMode();

            Debug.Log("[StartStopTest] Test completed successfully");
            
            
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
            
            void AssertNoErrorLog(string context)
            {
                var logEntries = LogBuffer.EnumerateEditorConsoleEntries().ToList();
                var isErrorLog = logEntries.Exists(entry => entry.type is LogType.Error or LogType.Exception);
                var allLogs = string.Empty;
                foreach (var log in logEntries)
                {
                    if (string.IsNullOrWhiteSpace(log.message)) continue;
                    if (log.type is LogType.Log) continue; // 通常のログは無視
                    
                    allLogs += $"----------------\n{log.type}\n{log.message}\n";
                }
                
                Debug.Log($"{context}\n{allLogs}");
                Assert.IsFalse(isErrorLog, $"There are error logs. Context: {context}");
                
                Debug.ClearDeveloperConsole();
            }
            
            #endregion
        }
    }
}