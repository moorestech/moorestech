using System;
using System.Collections.Generic;
using Client.Common;
using Client.Common.Asset;
using Client.Game.Common;
using Client.Network.API;
using CommandForgeGenerator.Command;
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
    public class OutcropGameObjectDatastore : MonoBehaviour, IInitialEventApplyWaitTarget, ISkitWorldObjectControl
    {
        // 露頭名にveinGuidを付与し、どの鉱脈の露頭かをシーン上で辿れるようにする
        // Append the vein GUID to outcrop names so each one can be traced back to its vein in the scene
        public const string OutcropObjectNamePrefix = "VeinOutcrop_";

        // 露頭数は鉱脈密度に比例し、露頭1体はmapObjectより重いのでmapObject側の100より短い間隔でフレームを跨ぐ
        // Outcrop count scales with vein density and one outcrop is heavier than a map object, so cross frames more often than that path's 100
        private const int FrameYieldObjectInterval = 50;

        // 同一アドレスを複数のveinが共有するため、guidではなくアドレスでキャッシュする
        // Several veins share one address, so cache by address rather than by guid
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
                UnityEngine.Debug.Log($"[BOOTPROF] outcrop.instantiateStart {System.DateTime.UtcNow:O}");
                var bootprofWatch = System.Diagnostics.Stopwatch.StartNew();
                var cancellationToken = this.GetCancellationTokenOnDestroy();
                var processedCount = 0;

                foreach (var layout in _handshakeResponse.MapLayout.MapVeins)
                {
                    var veinGuid = new Guid(layout.VeinGuid);
                    var element = MasterHolder.MapVeinMaster.GetElementOrNull(veinGuid);
                    if (element == null)
                        throw new InvalidOperationException($"[OutcropGameObjectDatastore] mapVeinsマスタにveinGuid:{veinGuid}がありません");

                    var prefab = ResolveOutcropPrefab(veinGuid, element);
                    var center = CalculateInclusiveCenter(layout);

                    // 露頭は地形状態に依存させず鉱脈AABB中心へ配置する
                    // Place outcrops at vein AABB centers without depending on terrain state
                    if (prefab != null) InstantiateOutcrop(prefab, veinGuid, element, layout, center);

                    processedCount++;
                    if (processedCount % FrameYieldObjectInterval == 0) await UniTask.Yield(cancellationToken);
                }
                Debug.Log($"[BOOTPROF] outcrop.instantiateEnd count={processedCount} wallMs={bootprofWatch.Elapsed.TotalMilliseconds:F0} {System.DateTime.UtcNow:O}");
            }

            GameObject ResolveOutcropPrefab(Guid veinGuid, MapVeinMasterElement element)
            {
                var address = element.OutcropAddressablePath;
                if (_prefabCacheByAddress.TryGetValue(address, out var cachedPrefab)) return cachedPrefab;

                // 1本の失敗で全鉱脈を巻き添えにしない
                // One failed load must not take every vein down
                var loaded = AddressableLoader.LoadDefault<GameObject>(address);
                if (loaded == null)
                    // 失敗もキャッシュし、同じアドレスを共有する残りのveinで再試行とログを繰り返さない
                    // Cache the failure too so the remaining veins sharing this address neither retry nor re-log
                    Debug.LogError($"[OutcropGameObjectDatastore] 露頭プレハブをロードできません VeinGuid:{veinGuid} VeinName:{element.VeinName} Address:{address}");

                _prefabCacheByAddress[address] = loaded;
                return loaded;
            }

            void InstantiateOutcrop(GameObject prefab, Guid veinGuid, MapVeinMasterElement element, VeinLayoutMessagePack layout, Vector3 center)
            {
                var instance = Instantiate(prefab, center, Quaternion.identity, transform);
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
                // min/maxは内包セル座標なのでmax側に1セル分足してAABB中心を出す
                // min/max are inclusive cell coords, so add one cell on the max side to get the AABB center
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

            #endregion
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

        public void SetActive(bool enable)
        {
            gameObject.SetActive(enable);
        }
    }
}
