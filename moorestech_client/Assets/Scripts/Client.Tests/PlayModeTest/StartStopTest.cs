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
            // ログ一覧を取得
            logEntries = new();
            Application.logMessageReceived += (condition, stackTrace, type) =>
            {
                logEntries.Add((condition, stackTrace, type));
            };
            
            yield return new EnterPlayMode(expectDomainReload: true);
            
            yield return LoadGame("First Login").ToCoroutine();
            
            yield return new ExitPlayMode();
            
            // ログをクリア
            Debug.ClearDeveloperConsole();
            logEntries.Clear();
            
            
            // 3秒待機
            yield return new WaitForSeconds(3f);
            
            // 再びプレイモードに入る
            yield return new EnterPlayMode(expectDomainReload: false);
            
            yield return LoadGame("Second Login").ToCoroutine();
            
            
            // エラーログがないことを確認
            var isErrorLog = logEntries.Exists(entry => entry.type is LogType.Error or LogType.Exception);
            Assert.IsFalse(isErrorLog, "There are error logs.");
            
            yield return new ExitPlayMode();
            
            
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