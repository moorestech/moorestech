using System;
using System.Collections.Generic;
using System.Linq;
using Client.Common;
using Client.Game.Common;
using Client.Game.InGame.Context;
using Client.Game.InGame.Environment.Terrain;
using Client.Network.API;
using Cysharp.Threading.Tasks;
using Game.Context;
using UnityEngine;
using UnityEngine.SceneManagement;
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
            // Forgetされる非同期境界で例外を観測し、DI未構築のMainGameへ取り残さない
            // Observe exceptions at the forgotten async boundary to avoid stranding MainGame without DI
            try
            {
                await FinalizeAsync();
            }
            catch (Exception e)
            {
                Debug.LogError($"初期化処理中にエラーが発生しました: {e.GetType()} {e.Message}\n{e.StackTrace}");
                SceneManager.LoadScene(SceneConstant.MainMenuSceneName);
                return;
            }

            // 購読者の例外で完了済みゲームをMainMenuへ戻さないため、発火は例外境界の外に置く
            // Fire outside the exception boundary so subscriber failures cannot return an initialized game to MainMenu
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

            // 初期イベント適用とログイン復元までを初期化完了契約に含める
            // Include initial-event application and login restoration in the initialization completion contract
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
                if (!warned && Time.realtimeSinceStartup >= warnAt)
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
