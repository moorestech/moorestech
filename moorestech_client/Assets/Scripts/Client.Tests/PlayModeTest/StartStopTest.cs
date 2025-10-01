using System;
using System.Collections;
using System.Collections.Generic;
using Client.Game.InGame.UI.Challenge;
using Client.Tests.PlayModeTest.Util;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using Server.Boot;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests
{
    public class StartStopTest
    {
        private static List<(string condition, string stackTrace, LogType type)> logEntries = new();
        
        [UnityTest]
        public IEnumerator PlayAndStartStop()
        {
            Debug.Log("[StartStopTest] Step 1: Initialize log entries");
            logEntries = new();
            Application.logMessageReceived += (condition, stackTrace, type) =>
            {
                logEntries.Add((condition, stackTrace, type));
            };

            Debug.Log("[StartStopTest] Step 2: Enter play mode (First time, expectDomainReload: true)");
            yield return new EnterPlayMode(expectDomainReload: true);

            Debug.Log("[StartStopTest] Step 3: Load game (First Login)");
            yield return LoadGame("First Login").ToCoroutine();

            Debug.Log("[StartStopTest] Step 4: Exit play mode (First time)");
            yield return new ExitPlayMode();

            Debug.Log("[StartStopTest] Step 5: Clear logs");
            Debug.ClearDeveloperConsole();
            logEntries.Clear();

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
                var isErrorLog = logEntries.Exists(entry => entry.type is LogType.Error or LogType.Exception);
                var allLogs = string.Empty;
                foreach (var (condition, stackTrace, logType) in logEntries)
                {
                    allLogs += $"-----------------------------\n{condition}\n{stackTrace}\n{logType}\n";
                }
                
                Debug.Log($"{context}\n{allLogs}");
                Assert.IsFalse(isErrorLog, $"There are error logs. Context: {context}");
            }
            
            #endregion
        }
    }
}