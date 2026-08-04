using System;
using System.Collections.Generic;
using Client.Common.Asset;
using Client.Game.Common;
using Client.Game.InGame.BlockSystem;
using Client.Network.API;
using Core.Master;
using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer;

namespace Client.Game.InGame.Map.MapVein
{
    /// <summary>
    ///     鉱脈の露頭をLayout応答から実行時Instantiateする。純ビジュアルで状態同期も破壊処理も持たない
    ///     Instantiates vein outcrops at runtime from the layout response; purely visual, with no state sync or destruction
    /// </summary>
    public class MapVeinObjectDatastore : MonoBehaviour, IInitialEventApplyWaitTarget
    {
        // 露頭名にveinGuidを付与
        // Append the vein GUID to outcrop names
        public const string OutcropObjectNamePrefix = "VeinOutcrop_";

        // v8ワールドは1772本規模。1体がmapObjectより重いのでmapObject側の100より短い間隔でフレームを跨ぐ
        // The v8 world holds ~1772 veins; each outcrop is heavier than a map object, so cross frames more often than that path's 100
        private const int FrameYieldObjectInterval = 50;

        // 同一アドレスを複数のveinが共有するため、guidではなくアドレスでキャッシュする
        // Several veins share one address, so cache by address rather than by guid
        private readonly Dictionary<string, GameObject> _prefabCacheByAddress = new();
        private InitialHandshakeResponse _handshakeResponse;
        private UniTask? _initializationTask;

        [Inject]
        public void Construct(InitialHandshakeResponse handshakeResponse)
        {
            // 生成はTerrain構築後にFinalizerが明示開始する。DI解決の副作用で地表Raycastを走らせない（ADR#15）
            // Instantiation starts explicitly from the finalizer after terrain build; DI resolution must not fire ground raycasts (ADR#15)
            _handshakeResponse = handshakeResponse;
        }

        public void StartOutcropInstantiation()
        {
            // 二重開始は露頭を全数重複させ、1本目のタスクを待機不能にする順序バグ
            // A second start duplicates every outcrop and orphans the first task, so it is an ordering bug
            if (_initializationTask != null)
                throw new InvalidOperationException("[MapVeinObjectDatastore] StartOutcropInstantiationが二重に呼ばれました");

            // 完了と例外を待機機構がawaitできる形で保持する
            // Retain completion and exceptions in an awaitable form for the wait mechanism
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
                    var prefab = ResolveOutcropPrefab(veinGuid);

                    // min/maxは内包セル座標なのでmax側に1セル分足してAABB中心XZを出す
                    // min/max are inclusive cell coords, so add one cell on the max side to get the AABB center XZ
                    var centerX = (layout.MinX + layout.MaxX + 1) * 0.5f;
                    var centerZ = (layout.MinZ + layout.MaxZ + 1) * 0.5f;

                    processedCount++;
                    if (processedCount % FrameYieldObjectInterval == 0) await UniTask.Yield(cancellationToken);

                    // 仮マップは地形範囲外にも鉱脈があるため、地表未解決はベイク済みAABB中心高さへ置く（裁定2026-08-04）
                    // The interim map keeps veins beyond the terrain, so unresolved ground falls back to the baked AABB center height (ruling 2026-08-04)
                    if (!TryResolveGroundHeight(centerX, centerZ, out var groundHeight))
                    {
                        groundHeight = (layout.MinY + layout.MaxY + 1) * 0.5f;
                        groundFallbackCount++;
                    }

                    // veinGuidを名前に残し、どの鉱脈の露頭かをシーン上で辿れるようにする
                    // Keep the veinGuid in the name so each outcrop can be traced back to its vein in the scene
                    var instance = Instantiate(prefab, new Vector3(centerX, groundHeight, centerZ), Quaternion.identity, transform);
                    instance.name = $"{OutcropObjectNamePrefix}{layout.VeinGuid}";
                }

                // フォールバック件数は仮マップ対応の観測用。異常系ではないのでInfoに留める
                // The fallback count only observes the interim-map workaround; it is not a failure, so Info suffices
                if (0 < groundFallbackCount) Debug.Log($"[MapVeinObjectDatastore] 地表未解決の露頭をAABB中心高さへ設置 件数:{groundFallbackCount}");
            }

            GameObject ResolveOutcropPrefab(Guid veinGuid)
            {
                // master欠落・アドレス空・load失敗はいずれもデータ不正。露頭だけ黙って欠けさせず起動時に落とす
                // Missing master, empty address, and load failure are all data faults; fail at startup instead of silently dropping the outcrop
                var element = MasterHolder.MapVeinMaster.GetElementOrNull(veinGuid);
                if (element == null) throw new InvalidOperationException($"[MapVeinObjectDatastore] mapVeinsマスタにveinGuid:{veinGuid}がありません");

                var address = element.OutcropAddressablePath;
                if (string.IsNullOrEmpty(address)) throw new InvalidOperationException($"[MapVeinObjectDatastore] outcropAddressablePathが空です VeinGuid:{veinGuid} VeinName:{element.VeinName}");

                if (_prefabCacheByAddress.TryGetValue(address, out var cachedPrefab)) return cachedPrefab;

                var loaded = AddressableLoader.LoadDefault<GameObject>(address);
                if (loaded == null) throw new InvalidOperationException($"[MapVeinObjectDatastore] 露頭プレハブをロードできません VeinGuid:{veinGuid} Address:{address}");

                _prefabCacheByAddress[address] = loaded;
                return loaded;
            }

            bool TryResolveGroundHeight(float x, float z, out float groundHeight)
            {
                // 地表判定は設置系と同じ単一エントリポイントへ委譲する（ADR#14: 集約）
                // Ground probing delegates to the placement systems' single entry point (ADR#14)
                if (SlopeBlockPlaceSystem.TryGetGroundPoint(x, z, out var groundPoint))
                {
                    groundHeight = groundPoint.y;
                    return true;
                }

                groundHeight = 0f;
                return false;
            }

            #endregion
        }

        public UniTask WaitForInitialApplyAsync()
        {
            // 開始前の待機要求は順序バグ。既定値タスク（完了扱い）で素通りさせず失敗させる
            // Waiting before the start is an ordering bug; never let the default (completed) task slip through
            if (_initializationTask == null)
                throw new InvalidOperationException("[MapVeinObjectDatastore] StartOutcropInstantiation前に待機が要求されました");
            return _initializationTask.Value;
        }
    }
}
