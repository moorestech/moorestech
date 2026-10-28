using System;
using System.Collections.Generic;
using System.Linq;
using Client.Playtest.Core;
using Cysharp.Threading.Tasks;
using Game.Context;
using Game.Research;
using UnityEngine;

namespace Client.Playtest.Operations
{
    // 可変研究表をtick内で複製し、シナリオには値だけを返す
    // Copy the mutable research table inside the tick and return only values to scenarios
    public static class PlaytestResearchSnapshots
    {
        public static async UniTask<HashSet<Guid>> ReadCompletedResearch(this PlaytestDriver driver)
        {
            HashSet<Guid> result = null;
            await PlaytestServerThreadAction.RunAsync("研究の完了状態を複製する", () =>
                result = new HashSet<Guid>(ServerContext.GetService<IResearchDataStore>().GetSaveJsonObject().CompletedResearchGuids.Select(Guid.Parse)));
            return result;
        }

        public static async UniTask WaitForResearch(this PlaytestDriver driver, Guid[] required, Guid[] excluded, float timeoutSeconds, string label)
        {
            var deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (true)
            {
                var completed = await driver.ReadCompletedResearch();
                if (required.All(completed.Contains) && excluded.All(guid => !completed.Contains(guid))) return;
                if (deadline < Time.realtimeSinceStartup) throw new TimeoutException(label);
                await UniTask.Yield();
            }
        }
    }
}
