using System;
using System.Collections.Generic;
using Client.Common;
using Client.Common.Asset;
using Client.Game.Common;
using Client.Game.InGame.BlockSystem;
using Client.Network.API;
using Core.Master;
using Cysharp.Threading.Tasks;
using Mooresmaster.Model.MapModule;
using Server.Protocol.PacketResponse.MapData;
using UnityEngine;
using VContainer;

namespace Client.Game.InGame.Map.Outcrop
{
    /// <summary>
    ///     Layout応答の全鉱脈から露頭を生成し、手掘り可能な露頭だけを採掘対象化する
    ///     Instantiates every layout outcrop and makes only hand-minable ones mining targets
    /// </summary>
    public class OutcropGameObjectDatastore : MonoBehaviour, IInitialEventApplyWaitTarget
    {
        public const string OutcropObjectNamePrefix = "VeinOutcrop_";

        // 千件規模の生成負荷を分散しつつ起動時間を過度に伸ばさない
        // Spread the thousand-scale instantiation load without extending startup excessively
        private const int FrameYieldObjectInterval = 100;
        // 同一アドレスを共有する鉱脈では成功したAddressables解決を再利用する
        // Reuse successful Addressables resolutions for veins sharing one address
        private readonly Dictionary<string, GameObject> _prefabCacheByAddress = new();
        private readonly OutcropGuidIndex _outcropGuidIndex = new();
        private InitialHandshakeResponse _handshakeResponse;
        private UniTask? _initializationTask;

        [Inject]
        public void Construct(InitialHandshakeResponse handshakeResponse)
        {
            // 地表判定はTerrain完成後にFinalizerから開始する
            // Ground probing starts from the finalizer after Terrain is ready
            _handshakeResponse = handshakeResponse;
        }

        public void StartOutcropInstantiation()
        {
            if (_initializationTask != null)
                throw new InvalidOperationException("[OutcropGameObjectDatastore] StartOutcropInstantiationが二重に呼ばれました");

            // 完了と例外を起動待機境界へ伝播させる
            // Propagate completion and exceptions to the startup wait boundary
            _initializationTask = InstantiateOutcropsFromLayoutAsync().Preserve();

            #region Internal

            async UniTask InstantiateOutcropsFromLayoutAsync()
            {
                var cancellationToken = this.GetCancellationTokenOnDestroy();
                var groundFallbackCount = 0;
                var processedCount = 0;

                foreach (var layout in _handshakeResponse.MapLayout.MapVeins)
                {
                    var veinGuid = new Guid(layout.VeinGuid);
                    var element = MasterHolder.MapVeinMaster.GetElementOrNull(veinGuid);
                    if (element == null)
                        throw new InvalidOperationException($"[OutcropGameObjectDatastore] mapVeinsマスタにveinGuid:{veinGuid}がありません");

                    var prefab = ResolveOutcropPrefab(veinGuid, element);
                    var center = CalculateInclusiveCenter(layout);

                    // 遠方の未ロード地形では裁定済みAABB中心高さへ置き、全露頭の生成を継続する
                    // On distant unloaded terrain, use the ruled AABB-center height and keep creating every outcrop
                    var groundResolved = TryResolveGroundPosition(center, out var groundPosition);
                    var position = SelectOutcropPosition(layout, center, groundResolved, groundPosition);
                    if (!groundResolved) groundFallbackCount++;
                    InstantiateOutcrop(prefab, veinGuid, element, layout, position, center);

                    processedCount++;
                    if (processedCount % FrameYieldObjectInterval == 0) await UniTask.Yield(cancellationToken);
                }

                // 仮v8マップの地形外件数は診断用に残すが、既知の正常フォールバックなのでInfoに留める
                // Keep the interim v8 map's off-terrain count for diagnosis, but log only Info for this known fallback
                if (0 < groundFallbackCount)
                    Debug.Log($"[OutcropGameObjectDatastore] 地表未解決の露頭をAABB中心高さへ設置 件数:{groundFallbackCount}");
            }

            GameObject ResolveOutcropPrefab(Guid veinGuid, MapVeinMasterElement element)
            {
                var address = element.OutcropAddressablePath;
                if (string.IsNullOrEmpty(address))
                    throw new InvalidOperationException($"[OutcropGameObjectDatastore] outcropAddressablePathが空です VeinGuid:{veinGuid} VeinName:{element.VeinName}");
                if (_prefabCacheByAddress.TryGetValue(address, out var cachedPrefab)) return cachedPrefab;

                // ロード失敗はビジュアル欠落を隠さず起動時に顕在化させる
                // Surface load failures at startup instead of hiding missing visuals
                var loaded = AddressableLoader.LoadDefault<GameObject>(address);
                if (loaded == null)
                    throw new InvalidOperationException($"[OutcropGameObjectDatastore] 露頭プレハブをロードできません VeinGuid:{veinGuid} Address:{address}");

                _prefabCacheByAddress[address] = loaded;
                return loaded;
            }

            void InstantiateOutcrop(GameObject prefab, Guid veinGuid, MapVeinMasterElement element, VeinLayoutMessagePack layout, Vector3 position, Vector3 center)
            {
                var instance = Instantiate(prefab, position, Quaternion.identity, transform);
                instance.name = $"{OutcropObjectNamePrefix}{layout.VeinGuid}";

                // 採掘レイの対象レイヤーをプレハブ階層全体へ統一する
                // Apply the mining-ray layer to the complete prefab hierarchy
                foreach (var child in instance.GetComponentsInChildren<Transform>(true))
                    child.gameObject.layer = LayerConst.MapObjectLayer;

                var outcrop = instance.GetComponent<OutcropGameObject>();
                if (outcrop == null) outcrop = instance.AddComponent<OutcropGameObject>();
                _outcropGuidIndex.Add(veinGuid, outcrop);

                // none鉱脈はビジュアルだけ残しコライダマーカーを注入しない
                // Keep none veins visual-only without injecting collider markers
                if (element.HandMiningParam is not MinableHandMiningParam) return;
                outcrop.Initialize(element, CalculateMinePosition(layout, center));
            }

            Vector3 CalculateInclusiveCenter(VeinLayoutMessagePack layout)
            {
                return new Vector3(
                    (layout.MinX + layout.MaxX + 1) * 0.5f,
                    (layout.MinY + layout.MaxY + 1) * 0.5f,
                    (layout.MinZ + layout.MaxZ + 1) * 0.5f);
            }

            Vector3Int CalculateMinePosition(VeinLayoutMessagePack layout, Vector3 center)
            {
                var rounded = Vector3Int.RoundToInt(center);
                return new Vector3Int(
                    Mathf.Clamp(rounded.x, layout.MinX, layout.MaxX),
                    Mathf.Clamp(rounded.y, layout.MinY, layout.MaxY),
                    Mathf.Clamp(rounded.z, layout.MinZ, layout.MaxZ));
            }

            bool TryResolveGroundPosition(Vector3 center, out Vector3 position)
            {
                // 地表探査を共有入口へ集約し、他種コライダを誤って地面扱いしない
                // Delegate to the shared probe so unrelated colliders are never treated as ground
                if (SlopeBlockPlaceSystem.TryGetGroundPoint(center.x, center.z, out var groundPoint))
                {
                    position = groundPoint;
                    return true;
                }

                position = default;
                return false;
            }

            #endregion
        }

        internal static Vector3 SelectOutcropPosition(
            VeinLayoutMessagePack layout,
            Vector3 center,
            bool groundResolved,
            Vector3 groundPosition)
        {
            if (groundResolved) return groundPosition;

            // 仮マップは地形範囲外にも鉱脈があるため、未解決時はベイク済みAABB中心高さを使う
            // The interim map has veins beyond terrain bounds, so unresolved surfaces use the baked AABB-center height
            var fallbackHeight = (layout.MinY + layout.MaxY + 1) * 0.5f;
            return new Vector3(center.x, fallbackHeight, center.z);
        }

        public UniTask WaitForInitialApplyAsync()
        {
            if (_initializationTask == null)
                throw new InvalidOperationException("[OutcropGameObjectDatastore] StartOutcropInstantiation前に待機が要求されました");
            return _initializationTask.Value;
        }

        public OutcropGameObject SearchNearestOutcrop(Guid veinGuid, Vector3 position)
        {
            return _outcropGuidIndex.SearchNearest(veinGuid, position);
        }
    }
}
