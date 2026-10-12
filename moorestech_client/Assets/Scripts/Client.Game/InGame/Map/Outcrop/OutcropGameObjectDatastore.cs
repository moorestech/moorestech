using System;
using System.Collections.Generic;
using Client.Game.InGame.Environment.Terrain;
using Client.Common;
using Client.Game.Common;
using Client.Game.InGame.Map.NearestSearch;
using Client.Network.API;
using CommandForgeGenerator.Command;
using Core.Master;
using Cysharp.Threading.Tasks;
using Mooresmaster.Model.MapModule;
using Server.Protocol.PacketResponse.MapData;
using Game.MapGeneration.Surface;
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

        private readonly OutcropPrefabCache _prefabCache = new();

        // 露頭は破壊されないので、生成のたび索引へ登録すれば最初の探索で1回だけ木が焼かれる
        // Outcrops are never destroyed, so registering each one as it spawns bakes the tree exactly once, on the first search
        private readonly NearestTargetIndex<OutcropGameObject> _nearestIndex = new();
        private InitialHandshakeResponse _handshakeResponse;
        private UniTask? _initializationTask;

        [Inject]
        public void Initialize(InitialHandshakeResponse handshakeResponse)
        {
            // 露頭の生成はTerrain完成後のStartOutcropInstantiationまで待つ
            // Outcrop creation waits for StartOutcropInstantiation after the Terrain is complete
            _handshakeResponse = handshakeResponse;
        }

        public void StartOutcropInstantiation(TerrainSurfacePresentation presentation)
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

                // 全prefabの接地契約を先に検査し、違反があれば1体も置かずに一括で止める
                // Check every prefab's grounding contract first and stop in bulk, placing nothing, on any violation
                var resolved = new List<(VeinLayoutMessagePack Layout, Guid VeinGuid, MapVeinMasterElement Element, OutcropPrefab Outcrop)>();
                var violationCount = 0;
                foreach (var layout in _handshakeResponse.MapLayout.MapVeins)
                {
                    var veinGuid = new Guid(layout.VeinGuid);
                    var element = MasterHolder.MapVeinMaster.GetElementOrNull(veinGuid);
                    if (element == null)
                        throw new InvalidOperationException($"[OutcropGameObjectDatastore] mapVeinsマスタにveinGuid:{veinGuid}がありません");

                    var outcrop = _prefabCache.Resolve(veinGuid, element, presentation);
                    if (outcrop == null) continue;
                    if (outcrop.ContractViolation != null)
                    {
                        violationCount++;
                        continue;
                    }
                    resolved.Add((layout, veinGuid, element, outcrop));
                }
                if (0 < violationCount)
                    throw SurfaceContractFailure.Create(
                        $"[OutcropGameObjectDatastore] {violationCount} veins use outcrop prefabs violating the grounding contract; see the [OutcropPrefab] errors.");

                var processedCount = 0;
                foreach (var (layout, veinGuid, element, outcrop) in resolved)
                {
                    // セルは元AABB中心、表示だけ接地
                    // Cells keep the original AABB center; only presentation is grounded
                    InstantiateOutcrop(outcrop, veinGuid, element, layout, CalculateInclusiveCenter(layout));

                    processedCount++;
                    if (processedCount % FrameYieldObjectInterval == 0) await UniTask.Yield(cancellationToken);
                }
            }

            void InstantiateOutcrop(OutcropPrefab outcrop, Guid veinGuid, MapVeinMasterElement element, VeinLayoutMessagePack layout, Vector3 center)
            {
                var instance = Instantiate(outcrop.Prefab, center, Quaternion.identity, transform);
                instance.name = $"{OutcropObjectNamePrefix}{layout.VeinGuid}";

                // 完成した表示契約に合わせて接地する
                // Ground against the completed presentation contract
                var bounds = new Bounds(center, new Vector3(layout.MaxX - layout.MinX + 1,
                    layout.MaxY - layout.MinY + 1, layout.MaxZ - layout.MinZ + 1));
                OutcropSurfacePlacement.Place(instance, outcrop, bounds, presentation);

                // 全階層を採掘レイヤー化
                // Apply mining layer to all children
                foreach (var child in instance.GetComponentsInChildren<Transform>(true))
                    child.gameObject.layer = LayerConst.MapObjectLayer;

                var outcropObject = instance.GetComponent<OutcropGameObject>();
                if (outcropObject == null) outcropObject = instance.AddComponent<OutcropGameObject>();
                _nearestIndex.Register(veinGuid, outcropObject);

                // 不可の鉱脈も提示対象なので初期化する
                // An unmineable vein still has to say so
                outcropObject.Initialize(element, veinGuid, CalculateMinePosition(layout, center));
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
            return _nearestIndex.TrySearchNearest(veinGuid, position, out var outcrop, out _) ? outcrop : null;
        }

        public void SetActive(bool enable)
        {
            gameObject.SetActive(enable);
        }
    }
}
