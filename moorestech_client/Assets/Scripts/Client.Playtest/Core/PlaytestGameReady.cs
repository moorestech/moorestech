using System;
using Client.Game.InGame.Player;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Client.Playtest.Core
{
    /// <summary>
    ///     ゲーム初期化完了の判定と条件待機。固定sleepの代わりにフレームポーリングで待つ
    ///     Detects game-initialization completion and waits by frame polling instead of fixed sleeps
    /// </summary>
    public static class PlaytestGameReady
    {
        // MainGameシーンのAwakeで設定されるシングルトンを完了指標にする
        // Uses the singleton assigned in the MainGame scene's Awake as the readiness indicator
        public static bool IsReady => PlayerSystemContainer.Instance != null;

        public static async UniTask WaitUntilReady(float timeoutSeconds)
        {
            var startTime = Time.realtimeSinceStartup;
            while (!IsReady)
            {
                if (timeoutSeconds < Time.realtimeSinceStartup - startTime)
                {
                    throw new TimeoutException($"game not ready within {timeoutSeconds}s");
                }
                await UniTask.Yield();
            }

            // 初期化直後のUI・エンティティ安定待ち
            // Short settle wait for UI and entities right after initialization
            await UniTask.Delay(TimeSpan.FromSeconds(1));
        }
    }
}
