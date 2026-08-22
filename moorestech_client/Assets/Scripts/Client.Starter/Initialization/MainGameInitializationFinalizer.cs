using System.Collections.Generic;
using Client.Common;
using Client.Game.Common;
using Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint;
using Client.Game.InGame.Context;
using Client.Game.InGame.Environment.Terrain;
using Client.Game.InGame.Construction;
using Client.Game.InGame.Hotbar;
using Client.Game.InGame.Map.Outcrop;
using Client.Game.InGame.Player;
using Client.Game.InGame.Presenter.Player;
using Client.Game.InGame.UI.Challenge;
using Client.Network.API;
using Core.Master;
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

            // ホットバー初期割当はhandshakeへ同梱済み。メインインベントリと同様イベント購読開始前に適用する
            // The initial hotbar assignments ride along with the handshake; applied before event dispatch starts, same as the main inventory
            resolver.Resolve<ClientHotbarDatastore>().ApplyAssignments(_serverResult.HandshakeResponse.HotbarAssignments);

            // 残り設置数もhandshake同梱。イベント購読開始前に適用する。生intからtyped BlockIdへの変換はここ(ワイヤ境界)で行う
            // Remaining placements ride along with the handshake too; applied before event dispatch starts. Raw int → typed BlockId conversion happens here, at the wire boundary
            var remainingPlacementCounts = new Dictionary<BlockId, int>();
            foreach (var count in _serverResult.HandshakeResponse.RemainingPlacementCounts)
            {
                remainingPlacementCounts[new BlockId(count.WalletBlockId)] = count.RemainingCount;
            }
            resolver.Resolve<ClientRemainingPlacementCountDatastore>().ApplyAll(remainingPlacementCounts);

            // BP割当の解決元をログイン時に1度満たす。ビルドメニュー入場までBP枠が未解決に見えるのを防ぐ
            // Fill the blueprint assignments' resolution source once at login so blueprint slots are not unresolved until the build menu is opened
            await resolver.Resolve<ClientBlueprintLibrary>().Refresh(default);

            // イベント適用開始を地形構築より前へ戻し、未生成個体宛イベントが捨てられる窓を地形構築時間分広げない（ADR#15）
            // Start event application before terrain build so the drop window for not-yet-spawned targets never widens by build time (ADR#15)
            (_serverResult.VanillaApi.Event as VanillaApiEvent)?.InitializeDispatch();

            // 露頭を含むワールドオブジェクトの生成前にTerrainを構築する
            // Build Terrain before instantiating world objects including outcrops
            await TerrainRuntimeBuilder.BuildAsync(_serverResult.HandshakeResponse.MapLayout, starter.EnvironmentRoot.transform);

            // 露頭生成はTerrain完成後に明示開始する。完了待ちは下の待機境界が一括で担う（ADR#15）
            // Outcrop instantiation starts explicitly after the terrain is ready; the wait boundary below waits for it with the rest (ADR#15)
            resolver.Resolve<OutcropGameObjectDatastore>().StartOutcropInstantiation();

            await InitialEventApplyWaiter.WaitAllAsync(resolver.Resolve<IReadOnlyList<IInitialEventApplyWaitTarget>>());

            // ピンが探す対象の生成後に適用する
            // Apply only once the objects a pin searches for exist
            resolver.Resolve<ChallengeManager>().ApplyInitialTutorials();

            // 車両の生成まで終えてから自機を保存座標へ置く。乗車セーブの復帰先が未生成だと支えが無く落下する（ADR#16）
            // Place the player only after the train cars exist, since a riding save would otherwise land on nothing and fall (ADR#16)
            resolver.Resolve<PlayerSystemContainer>().StartPlayerRuntime();
            resolver.Resolve<PlayerPositionSender>().StartSending();

            starter.RestoreLoginState(_serverResult.HandshakeResponse);
        }
    }
}
