using System;
using System.Linq;
using Client.Playtest.Core;
using Cysharp.Threading.Tasks;
using Game.Challenge;
using Game.Context;
using UnityEngine;

namespace Client.Playtest.Operations
{
    // チャレンジの可変リストをサーバースレッド内で判定する
    // Inspect mutable challenge lists only on the server thread
    public static class PlaytestChallengeSnapshots
    {
        public static async UniTask<bool> ReadChallenge(this PlaytestDriver driver, Guid guid, bool completed)
        {
            var found = false;
            await PlaytestServerThreadAction.RunAsync("チャレンジ状態を読む", () =>
            {
                var info = ServerContext.GetService<ChallengeDatastore>().CurrentChallengeInfo;
                found = completed ? info.CompletedChallenges.Any(item => item.ChallengeGuid == guid) :
                    info.CurrentChallenges.Any(item => item.ChallengeMasterElement.ChallengeGuid == guid);
            });
            return found;
        }

        public static async UniTask<bool> WaitForChallenge(this PlaytestDriver driver, Guid guid, bool completed, float timeoutSeconds)
        {
            var deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (await driver.ReadChallenge(guid, completed)) return true;
                await UniTask.Yield();
            }
            Debug.LogWarning($"[Playtest] チャレンジ待ちが時間切れです guid:{guid} completed:{completed}");
            return false;
        }
    }
}
