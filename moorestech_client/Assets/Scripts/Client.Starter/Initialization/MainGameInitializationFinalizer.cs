using System.Collections.Generic;
using Client.Common;
using Client.Game.Common;
using Client.Game.InGame.Context;
using Client.Game.InGame.Environment.Terrain;
using Client.Game.InGame.Hotbar;
using Client.Game.InGame.Map.MapVein;
using Client.Game.InGame.Player;
using Client.Game.InGame.Presenter.Player;
using Client.Network.API;
using Cysharp.Threading.Tasks;
using Game.Context;
using UnityEngine;
using VContainer;

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

            // ホットバー初期割当を取得し適用する。メインインベントリと同様イベント購読開始前に適用する
            // Fetch and apply the initial hotbar assignments before event dispatch starts, same as the main inventory
            var hotbarResponse = await _serverResult.VanillaApi.Response.GetHotbar(default);
            resolver.Resolve<ClientHotbarDatastore>().ApplyAssignments(hotbarResponse.Assignments);

            // イベント適用開始を地形構築より前へ戻し、未生成個体宛イベントが捨てられる窓を地形構築時間分広げない（ADR#15）
            // Start event application before terrain build so the drop window for not-yet-spawned targets never widens by build time (ADR#15)
            (_serverResult.VanillaApi.Event as VanillaApiEvent)?.InitializeDispatch();

            // 露頭生成の地表Raycastより前にTerrainを構築し、物理シーンへ反映する
            // Build Terrain before outcrop surface raycasts and synchronize it into the physics scene
            await TerrainRuntimeBuilder.BuildAsync(_serverResult.HandshakeResponse.MapLayout, starter.EnvironmentRoot.transform);

            // 露頭生成はTerrain完成後に明示開始する。完了待ちは下の待機境界が一括で担う（ADR#15）
            // Outcrop instantiation starts explicitly after the terrain is ready; the wait boundary below waits for it with the rest (ADR#15)
            resolver.Resolve<MapVeinObjectDatastore>().StartOutcropInstantiation();

            await InitialEventApplyWaiter.WaitAllAsync(resolver.Resolve<IReadOnlyList<IInitialEventApplyWaitTarget>>());

            // 車両の生成まで終えてから自機を保存座標へ置く。乗車セーブの復帰先が未生成だと支えが無く落下する（ADR#16）
            // Place the player only after the train cars exist, since a riding save would otherwise land on nothing and fall (ADR#16)
            resolver.Resolve<PlayerSystemContainer>().StartPlayerRuntime();
            resolver.Resolve<PlayerPositionSender>().StartSending();

            starter.RestoreLoginState(_serverResult.HandshakeResponse);
        }
    }
}
