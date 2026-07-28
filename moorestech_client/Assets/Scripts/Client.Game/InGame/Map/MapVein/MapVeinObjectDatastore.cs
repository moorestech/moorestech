using System;
using System.Collections.Generic;
using Client.Common;
using Client.Common.Asset;
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
    public class MapVeinObjectDatastore : MonoBehaviour
    {
        // 露頭インスタンス名の接頭辞。この後ろにveinGuidが続く
        // Prefix of an outcrop instance name, followed by the veinGuid
        public const string OutcropObjectNamePrefix = "VeinOutcrop_";

        // v8ワールドは1772本規模。1体がmapObjectより重いのでmapObject側の100より短い間隔でフレームを跨ぐ
        // The v8 world holds ~1772 veins; each outcrop is heavier than a map object, so cross frames more often than that path's 100
        private const int FrameYieldObjectInterval = 50;

        // 地表探査レイの開始高度と最大長。地形の全高度域を上下に跨げる値
        // Start altitude and max length of the ground probe ray, spanning the whole terrain height range
        private const float GroundProbeStartHeight = 1000f;
        private const float GroundProbeDistance = 2000f;

        // 同一アドレスを複数のveinが共有するため、guidではなくアドレスでキャッシュする
        // Several veins share one address, so cache by address rather than by guid
        private readonly Dictionary<string, GameObject> _prefabCacheByAddress = new();

        [Inject]
        public void Construct(InitialHandshakeResponse handshakeResponse)
        {
            // 生成本体はフレーム分散のfire-and-forgetへ委譲する
            // Delegate the instantiation itself to a frame-distributed fire-and-forget
            InstantiateOutcropsFromLayoutAsync().Forget();

            #region Internal

            async UniTask InstantiateOutcropsFromLayoutAsync()
            {
                var cancellationToken = this.GetCancellationTokenOnDestroy();

                var processedCount = 0;
                foreach (var layout in handshakeResponse.MapLayout.MapVeins)
                {
                    var veinGuid = new Guid(layout.VeinGuid);
                    var prefab = ResolveOutcropPrefab(veinGuid);

                    // min/maxは内包セル座標なのでmax側に1セル分足してAABB中心XZを出す
                    // min/max are inclusive cell coords, so add one cell on the max side to get the AABB center XZ
                    var centerX = (layout.MinX + layout.MaxX + 1) * 0.5f;
                    var centerZ = (layout.MinZ + layout.MaxZ + 1) * 0.5f;
                    var position = new Vector3(centerX, ResolveGroundHeight(centerX, centerZ), centerZ);

                    // veinGuidを名前に残し、どの鉱脈の露頭かをシーン上で辿れるようにする
                    // Keep the veinGuid in the name so each outcrop can be traced back to its vein in the scene
                    var instance = Instantiate(prefab, position, Quaternion.identity, transform);
                    instance.name = $"{OutcropObjectNamePrefix}{layout.VeinGuid}";

                    processedCount++;
                    if (processedCount % FrameYieldObjectInterval == 0) await UniTask.Yield(cancellationToken);
                }
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

            float ResolveGroundHeight(float x, float z)
            {
                // 地面判定は設置系と同じGroundGameObjectで行う。手前の非地面コライダーに遮られないよう全ヒットから選ぶ
                // Identify ground by GroundGameObject as the placement systems do; scan every hit so a nearer non-ground collider cannot mask it
                var origin = new Vector3(x, GroundProbeStartHeight, z);
                var hits = Physics.RaycastAll(origin, Vector3.down, GroundProbeDistance, LayerConst.Without_Player_MapObject_Block_LayerMask);

                var groundHeight = float.NegativeInfinity;
                foreach (var hit in hits)
                {
                    if (!hit.transform.TryGetComponent<GroundGameObject>(out _)) continue;
                    if (hit.point.y > groundHeight) groundHeight = hit.point.y;
                }

                // 地表が無いveinはワールドデータ不正。Y=0へ落とさず起動時に顕在化させる
                // A vein with no surface beneath it is invalid world data; surface it at startup instead of falling back to Y=0
                if (float.IsNegativeInfinity(groundHeight)) throw new InvalidOperationException($"[MapVeinObjectDatastore] 露頭を立てる地表がありません X:{x} Z:{z}");

                return groundHeight;
            }

            #endregion
        }
    }
}
