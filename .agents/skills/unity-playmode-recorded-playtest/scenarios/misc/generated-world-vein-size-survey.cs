// generatedワールドの鉱脈調査: 本数・veinGuid別内訳(item/fluid)・AABBサイズ分布を実機で測り、露頭の見た目を録画とスクショで残す
// 数値はクライアントが実際に受け取ったMapVeinsから取るため、生成器の内部状態ではなく配信結果を見ている
// Field survey of a generated world's veins: counts, per-veinGuid breakdown (item/fluid), and AABB size distribution, with the outcrops on record.
// The numbers come from the MapVeins the client actually received, so this observes the delivered result rather than generator internals.
using Client.Game.InGame.Context;
using Client.Network.API;
using Client.Playtest;
using Core.Master;
using Cysharp.Threading.Tasks;
using Mooresmaster.Model.MapModule;
using Server.Protocol.PacketResponse.MapData;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

var options = new PlaytestRunOptions { Record = true };
return PlaytestRunner.Run("generated-world-vein-size-survey", options, async p =>
{
    p.Note("generatedワールドの鉱脈調査を開始する");

    var mapLayout = await ClientContext.VanillaApi.Response.GetMapData(default);
    p.Assert(mapLayout.TerrainMeta.MapMode == "generated", "generatedモードで起動している");

    var veins = mapLayout.MapVeins;
    p.Note($"鉱脈総数: {veins.Count}");

    // サイズ分布。全件が (2,2,2) で揃っているかを本数の内訳ごと出す
    // Size distribution, printed with its per-size counts so a stray size stands out
    var sizes = new Dictionary<Vector3Int, int>();
    foreach (var vein in veins)
    {
        var size = new Vector3Int(vein.MaxX - vein.MinX, vein.MaxY - vein.MinY, vein.MaxZ - vein.MinZ);
        sizes[size] = sizes.TryGetValue(size, out var count) ? count + 1 : 1;
    }
    foreach (var pair in sizes.OrderByDescending(pair => pair.Value))
        p.Note($"size {pair.Key}: {pair.Value}件");

    // veinGuidごとの本数とitem/fluid種別ラベル。マスタに無いguidはunknownとして数える
    // Per-veinGuid count with its item/fluid type label; a guid missing from the master counts as unknown
    var guidCounts = veins.GroupBy(vein => vein.VeinGuid).OrderByDescending(group => group.Count());
    foreach (var group in guidCounts)
        p.Note($"veinGuid {group.Key} type={VeinTypeLabel(group.Key)}: {group.Count()}件");

    // 種別ごとの合計本数。item鉱脈とfluid鉱脈の離隔ルールが実質縮んでいないかの裏取り材料
    // Total count per type, backing evidence for whether item/fluid separation rules effectively shrank
    var typeTotals = veins.GroupBy(vein => VeinTypeLabel(vein.VeinGuid)).OrderByDescending(group => group.Count());
    foreach (var group in typeTotals)
        p.Note($"type={group.Key} 合計: {group.Count()}件");

    p.Assert(sizes.Count == 1 && sizes.ContainsKey(new Vector3Int(2, 2, 2)), "全鉱脈のAABBサイズが(2,2,2)で揃っている");

    // 露頭の見た目は録画とスクショで判断する。カメラ位置はスポーン地点のまま数秒回す
    // The outcrops are judged from the recording and screenshots; the camera stays at spawn and rolls for a few seconds
    await p.Screenshot("01-outcrops-before-wait");
    await UniTask.Delay(3000);
    await p.Screenshot("02-outcrops-after-wait");
    p.Note("露頭の見た目確認用の待機を終了する");

    // AimAtは配置カメラのシャロウピッチ固定視界内でしか成立しないため、現在のカメラ水平方向を基準に「対象の手前」へワープする
    // AimAt only resolves inside the placement camera's fixed shallow-pitch view, so warps stand on the near side of the current camera heading
    var cameraForward = Vector3.ProjectOnPlane(Camera.main.transform.forward, Vector3.up).normalized;
    if (cameraForward.sqrMagnitude < 0.1f) cameraForward = Vector3.forward;

    // 露頭密集地点。各鉱脈AABB中心を点とし、半径30m以内の他鉱脈数が最大の点へ寄って密度を見た目で確認する
    // The outcrop-dense spot: the vein-center point with the most other veins within a 30m radius, visited to see the density
    var centers = veins.Select(CalculateInclusiveCenter).ToList();
    var denseIndex = FindDenseIndex(centers, 30f);
    var densePoint = centers[denseIndex];
    var denseNeighborCount = CountWithinRadius(centers, denseIndex, 30f);
    var spawnPosition = p.PlayerPosition;
    p.Note($"密集地点: {densePoint} 半径30m内の他鉱脈数={denseNeighborCount} スポーンからの距離={Vector3.Distance(spawnPosition, densePoint):F1}m");
    p.WarpPlayer(densePoint - cameraForward * 22f + Vector3.up * 6f);
    await p.WaitSeconds(0.5f);
    await p.AimAt(densePoint);
    await p.WaitSeconds(1.5f);
    await p.Screenshot("03-dense-outcrops");

    // スポーンに最も近い鉱脈。1本を近接で見て2x2x2の見え方とサイズ感を確認する
    // The vein nearest to spawn: a close-up look at one to judge the 2x2x2 outcrop's apparent size
    var nearestIndex = FindNearestIndex(centers, spawnPosition);
    var nearestPoint = centers[nearestIndex];
    p.Note($"スポーン最寄り鉱脈: veinGuid={veins[nearestIndex].VeinGuid} 中心={nearestPoint} スポーンからの距離={Vector3.Distance(spawnPosition, nearestPoint):F1}m");
    p.WarpPlayer(nearestPoint - cameraForward * 5f + Vector3.up * 1.6f);
    await p.WaitSeconds(0.5f);
    await p.AimAt(nearestPoint);
    await p.WaitSeconds(1.5f);
    await p.Screenshot("04-nearest-outcrop");

    #region Internal

    string VeinTypeLabel(string veinGuidText)
    {
        if (!System.Guid.TryParse(veinGuidText, out var veinGuid)) return "unknown";
        var element = MasterHolder.MapVeinMaster.GetElementOrNull(veinGuid);
        if (element == null) return "unknown";
        if (element.VeinParam is ItemVeinParam) return "item";
        if (element.VeinParam is FluidVeinParam) return "fluid";
        return "unknown";
    }

    // min/maxは内包セル座標なのでmax側に1セル分足してAABB中心を出す（OutcropGameObjectDatastoreと同じ式）
    // min/max are inclusive cell coords, so add one cell on the max side (same formula as OutcropGameObjectDatastore)
    Vector3 CalculateInclusiveCenter(VeinLayoutMessagePack layout)
    {
        return new Vector3(
            (layout.MinX + layout.MaxX + 1) * 0.5f,
            (layout.MinY + layout.MaxY + 1) * 0.5f,
            (layout.MinZ + layout.MaxZ + 1) * 0.5f);
    }

    int CountWithinRadius(IReadOnlyList<Vector3> points, int originIndex, float radius)
    {
        var origin = points[originIndex];
        return points.Count(point => Vector3.Distance(origin, point) <= radius) - 1;
    }

    int FindDenseIndex(IReadOnlyList<Vector3> points, float radius)
    {
        var bestIndex = 0;
        var bestCount = -1;
        for (var i = 0; i < points.Count; i++)
        {
            var count = CountWithinRadius(points, i, radius);
            if (bestCount < count)
            {
                bestCount = count;
                bestIndex = i;
            }
        }
        return bestIndex;
    }

    int FindNearestIndex(IReadOnlyList<Vector3> points, Vector3 from)
    {
        var bestIndex = 0;
        var bestDistance = float.MaxValue;
        for (var i = 0; i < points.Count; i++)
        {
            var distance = Vector3.Distance(from, points[i]);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestIndex = i;
            }
        }
        return bestIndex;
    }

    #endregion
});
