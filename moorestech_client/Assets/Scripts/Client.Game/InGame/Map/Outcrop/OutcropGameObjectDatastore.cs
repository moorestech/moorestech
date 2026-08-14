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
    ///     全鉱脈の露頭を生成
    ///     Create outcrops for all veins
    /// </summary>
    public class OutcropGameObjectDatastore : MonoBehaviour, IInitialEventApplyWaitTarget
    {
        internal const string OutcropObjectNamePrefix = "VeinOutcrop_";

        // v8ワールドは約1772本の鉱脈を持ち、露頭1体はmapObjectより重いのでmapObject側の100より短い間隔でフレームを跨ぐ
        // The v8 world holds ~1772 veins and one outcrop is heavier than a map object, so cross frames more often than that path's 100
        private const int FrameYieldObjectInterval = 50;
        // 解決済みPrefabを再利用
        // Reuse resolved prefabs
        private readonly Dictionary<string, GameObject> _prefabCacheByAddress = new();
        private readonly OutcropGuidIndex _outcropGuidIndex = new();
        private InitialHandshakeResponse _handshakeResponse;
        private UniTask? _initializationTask;

        [Inject]
        public void Initialize(InitialHandshakeResponse handshakeResponse)
        {
            // Terrain完成後に地表判定
            // Probe ground after Terrain is ready
            _handshakeResponse = handshakeResponse;
        }

        public void StartOutcropInstantiation()
        {
            // 二重開始は露頭を重ねるので落とす
            // A second start would stack duplicate outcrops
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

                    // 地形未解決でも生成を止めない
                    // Keep creating even when the ground is unresolved
                    var groundResolved = TryResolveGroundPosition(center, out var groundPosition);
                    var position = SelectOutcropPosition(center, groundResolved, groundPosition);
                    if (!groundResolved) groundFallbackCount++;
                    if (prefab != null) InstantiateOutcrop(prefab, veinGuid, element, layout, position, center);

                    processedCount++;
                    if (processedCount % FrameYieldObjectInterval == 0) await UniTask.Yield(cancellationToken);
                }

                // 既知の正常系なので件数はInfoに留める
                // A known normal case, so the count stays at Info
                if (0 < groundFallbackCount)
                    Debug.Log($"[OutcropGameObjectDatastore] 地表未解決の露頭をAABB中心高さへ設置 件数:{groundFallbackCount}");
            }

            GameObject ResolveOutcropPrefab(Guid veinGuid, MapVeinMasterElement element)
            {
                var address = element.OutcropAddressablePath;
                if (_prefabCacheByAddress.TryGetValue(address, out var cachedPrefab)) return cachedPrefab;

                // 1本の失敗で全鉱脈を巻き添えにしない
                // One failed load must not take every vein down
                var loaded = AddressableLoader.LoadDefault<GameObject>(address);
                if (loaded == null)
                {
                    Debug.LogError($"[OutcropGameObjectDatastore] 露頭プレハブをロードできません VeinGuid:{veinGuid} VeinName:{element.VeinName} Address:{address}");
                    return null;
                }

                _prefabCacheByAddress[address] = loaded;
                return loaded;
            }

            void InstantiateOutcrop(GameObject prefab, Guid veinGuid, MapVeinMasterElement element, VeinLayoutMessagePack layout, Vector3 position, Vector3 center)
            {
                var instance = Instantiate(prefab, position, Quaternion.identity, transform);
                instance.name = $"{OutcropObjectNamePrefix}{layout.VeinGuid}";

                // 全階層を採掘レイヤー化
                // Apply mining layer to all children
                foreach (var child in instance.GetComponentsInChildren<Transform>(true))
                    child.gameObject.layer = LayerConst.MapObjectLayer;

                var outcrop = instance.GetComponent<OutcropGameObject>();
                if (outcrop == null) outcrop = instance.AddComponent<OutcropGameObject>();
                _outcropGuidIndex.Add(veinGuid, outcrop);

                // 不可の鉱脈も提示対象なので初期化する
                // An unmineable vein still has to say so
                outcrop.Initialize(element, veinGuid, CalculateMinePosition(layout, center));
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

        internal static Vector3 SelectOutcropPosition(Vector3 center, bool groundResolved, Vector3 groundPosition)
        {
            // 地形範囲外の鉱脈はAABB中心へ置く
            // Veins beyond the terrain use the baked AABB center
            return groundResolved ? groundPosition : center;
        }

        public UniTask WaitForInitialApplyAsync()
        {
            // 開始前の待機は保証できないので落とす
            // Waiting before the start guarantees nothing
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
