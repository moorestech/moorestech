using Client.Playtest.Core;
using Cysharp.Threading.Tasks;
using Game.Block.Blocks.TrainRail;
using Game.Block.Interface;
using Game.Block.Interface.Extension;

namespace Client.Playtest.Operations
{
    public static class PlaytestTrainStationSnapshots
    {
        // 駅名をサーバーの tick で読む
        // Read the station name on the server tick
        public static async UniTask<string> ReadStationName(this PlaytestDriver driver, IBlock station)
        {
            var name = "";
            await PlaytestServerThreadAction.RunAsync(driver.CancellationToken, "駅名を複製する", () => name = station.GetComponent<TrainStationComponent>().StationName);
            return name;
        }

        public static async UniTask WaitForStationName(this PlaytestDriver driver, IBlock station, string expected, float timeoutSeconds, string label)
        {
            var wait = driver.BeginWait(timeoutSeconds, label);
            while (true)
            {
                var name = await driver.ReadStationName(station);
                if (name == expected) { wait.Complete(); return; }
                wait.CheckTimeout($"expected={expected}; actual={name}");
                await UniTask.Yield();
            }
        }
    }
}
