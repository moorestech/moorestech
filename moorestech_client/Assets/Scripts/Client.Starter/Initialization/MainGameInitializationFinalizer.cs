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

            // 露頭生成の地表Raycastより前にTerrainを構築し、物理シーンへ反映する
            // Build Terrain before outcrop surface raycasts and synchronize it into the physics scene
            await TerrainRuntimeBuilder.BuildAsync(_serverResult.HandshakeResponse.MapLayout, starter.EnvironmentRoot.transform);

            var resolver = starter.StartGame(_serverResult.HandshakeResponse);
            new ClientDIContext(new DIContainer(resolver));
            WebUiHost.Game.WebUiGameBinder.Bind();
            (_serverResult.VanillaApi.Event as VanillaApiEvent)?.InitializeDispatch();

            // 露頭生成と初期イベント適用の例外をこの境界へ戻し、完了前のGameInitialized発火を防ぐ
            // Return outcrop and initial-event failures to this boundary, preventing GameInitialized before completion
            await resolver.Resolve<MapVeinObjectDatastore>().WaitForInitializationAsync();
            await WaitAllInitialEventApplyAsync(resolver);
            starter.RestoreLoginState(_serverResult.HandshakeResponse);
        }

        private static async UniTask WaitAllInitialEventApplyAsync(IObjectResolver resolver)
        {
            var targets = resolver.Resolve<IReadOnlyList<IInitialEventApplyWaitTarget>>();
            var warnAt = Time.realtimeSinceStartup + 5f;
            var warned = false;
            while (!targets.All(target => target.IsInitialEventApplied))
            {
                // 長時間未完了なら詰まっている対象を顕在化し、適用待機自体は継続する
                // Surface targets stuck for too long while continuing to wait for their application
                if (!warned && warnAt <= Time.realtimeSinceStartup)
                {
                    warned = true;
                    var pending = string.Join(", ", targets.Where(target => !target.IsInitialEventApplied).Select(target => target.GetType().Name));
                    Debug.LogWarning($"[InitializeScenePipeline] 初期イベント適用が未完了のまま待機中: {pending}");
                }
                await UniTask.Yield();
            }
        }
    }
}
