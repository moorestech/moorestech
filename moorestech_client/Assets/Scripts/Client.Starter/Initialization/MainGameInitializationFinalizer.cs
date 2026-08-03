using System;
using System.Collections.Generic;
using System.Linq;
using Client.Common;
using Client.Game.Common;
using Client.Game.InGame.Context;
using Client.Game.InGame.Environment.Terrain;
using Client.Game.InGame.Map.MapVein;
using Client.Network.API;
using Cysharp.Threading.Tasks;
using Game.Context;
using UnityEngine;
using VContainer;
using Debug = UnityEngine.Debug;

namespace Client.Starter.Initialization
{
    public class MainGameInitializationFinalizer
    {
        private readonly ServerConnectionResult _serverResult;

        public MainGameInitializationFinalizer(ServerConnectionResult serverResult)
        {
            _serverResult = serverResult;
        }

        public async UniTask RunAsync()
        {
            await FinalizeAsync();
            GameInitializedEvent.FireGameInitialized();
        }

        private async UniTask FinalizeAsync()
        {
            var starter = UnityEngine.Object.FindFirstObjectByType<MainGameStarter>();

            var resolver = starter.StartGame(_serverResult.HandshakeResponse);
            new ClientDIContext(new DIContainer(resolver));
            WebUiHost.Game.WebUiGameBinder.Bind();

            // イベント適用開始を地形構築より前へ戻し、未生成個体宛イベントが捨てられる窓を地形構築時間分広げない（ADR#15）
            // Start event application before terrain build so the drop window for not-yet-spawned targets never widens by build time (ADR#15)
            (_serverResult.VanillaApi.Event as VanillaApiEvent)?.InitializeDispatch();

            // 露頭生成の地表Raycastより前にTerrainを構築し、物理シーンへ反映する
            // Build Terrain before outcrop surface raycasts and synchronize it into the physics scene
            await TerrainRuntimeBuilder.BuildAsync(_serverResult.HandshakeResponse.MapLayout, starter.EnvironmentRoot.transform);

            // 露頭生成はTerrain完成後に明示開始する。完了待ちは下のWhenAllが一括で担う（ADR#15）
            // Outcrop instantiation starts explicitly after the terrain is ready; the WhenAll below waits for it with the rest (ADR#15)
            resolver.Resolve<MapVeinObjectDatastore>().StartOutcropInstantiation();

            await WaitAllInitialApplyAsync(resolver);
            starter.RestoreLoginState(_serverResult.HandshakeResponse);
        }

        private static async UniTask WaitAllInitialApplyAsync(IObjectResolver resolver)
        {
            var targets = resolver.Resolve<IReadOnlyList<IInitialEventApplyWaitTarget>>();
            var waits = targets.Select(target => (target, task: target.WaitForInitialApplyAsync().Preserve())).ToList();
            var allApplied = UniTask.WhenAll(waits.Select(wait => wait.task));

            // 対象タスクはWhenAllで一度だけawaitする。警告側でも待つとUniTaskの二重await例外になる
            // Await the targets once through WhenAll; awaiting them again in the warning path throws UniTask's double-await error
            WarnStuckTargetsAsync().Forget();
            await allApplied;

            #region Internal

            // 5秒未完了で詰まっている対象を顕在化し、適用待機自体は継続する
            // Surface targets stuck past five seconds while continuing to wait for their application
            async UniTaskVoid WarnStuckTargetsAsync()
            {
                await UniTask.Delay(TimeSpan.FromSeconds(5));

                // 未完了(Pending)だけを並べる。faultedは例外として上がるので警告に載せない
                // List only Pending targets; faulted ones surface as exceptions instead
                var pending = string.Join(", ", waits.Where(wait => wait.task.Status == UniTaskStatus.Pending).Select(wait => wait.target.GetType().Name));
                if (pending.Length == 0) return;
                Debug.LogWarning($"[MainGameInitializationFinalizer] 初期イベント適用が未完了のまま待機中: {pending}");
            }

            #endregion
        }
    }
}
