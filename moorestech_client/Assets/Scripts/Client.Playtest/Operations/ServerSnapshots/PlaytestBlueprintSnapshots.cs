using System;
using System.Linq;
using Client.Playtest.Core;
using Cysharp.Threading.Tasks;
using Game.Blueprint;
using Game.Context;
using Newtonsoft.Json;
using UnityEngine;

namespace Client.Playtest.Operations
{
    // 可変の登録表と図面の内容をtick内で複製する
    // Copy the mutable registry and blueprint contents inside the tick
    public static class PlaytestBlueprintSnapshots
    {
        public static async UniTask<BlueprintJsonObject> WaitForBlueprint(this PlaytestDriver driver, string name, bool present, float timeoutSeconds)
        {
            var deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (true)
            {
                BlueprintJsonObject snapshot = null;
                await PlaytestServerThreadAction.RunAsync("BP登録状態を複製する", () =>
                {
                    var found = ServerContext.GetService<IBlueprintDatastore>().Blueprints.FirstOrDefault(item => item.Name == name);
                    if (found != null) snapshot = JsonConvert.DeserializeObject<BlueprintJsonObject>(JsonConvert.SerializeObject(found));
                });
                if ((snapshot != null) == present) return snapshot;
                if (deadline < Time.realtimeSinceStartup) throw new TimeoutException($"BP登録待ちが時間切れです name:{name} present:{present}");
                await UniTask.Yield();
            }
        }
    }
}
