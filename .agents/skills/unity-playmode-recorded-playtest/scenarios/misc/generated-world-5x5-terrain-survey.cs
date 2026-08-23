// generatedワールド5x5の実機調査: 25タイル構築・タイル境界の高さ連続性・splat/detailの実データ・スポーン接地を通しで確認する
// 見た目の判定は録画とスクショで行い、境界シームは数値(隣接タイルの高さ差)でも測って目視の当て推量を排除する
// Field survey of a generated 5x5 world: 25-tile build, height continuity across tile seams, real splat/detail data, and spawn grounding.
// Looks are judged from the recording and screenshots, while seams are also measured numerically (adjacent-tile height delta) so nothing rests on eyeballing.
using Client.Game.Common;
using Client.Game.InGame.BlockSystem;
using Client.Game.InGame.Context;
using Client.Network.API;
using Client.Playtest;
using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using VContainer;

var options = new PlaytestRunOptions { Record = true };
return PlaytestRunner.Run("generated-world-5x5-terrain-survey", options, async p =>
{
    p.Note("generatedワールド(5x5)の地形調査を開始する");

    // 1: ワールドメタ。25タイルで来ていなければ以降の境界調査に意味が無い
    // 1: World meta; without 25 tiles arriving, none of the seam survey below means anything
    var mapLayout = await ClientContext.VanillaApi.Response.GetMapData(default);
    var meta = mapLayout.TerrainMeta;
    p.Note($"meta mapMode={meta.MapMode} tiles={meta.TerrainTileCount} resolution={meta.TerrainResolution} seed={meta.WorldSeed} sceneOrigin=({meta.SceneOrigin.X},{meta.SceneOrigin.Y}) noiseOrigin=({meta.NoiseOrigin.X},{meta.NoiseOrigin.Y})");
    p.Assert(meta.MapMode == "generated", "generatedモードで起動している");
    p.Assert(meta.TerrainTileCount == 25, $"地形タイル数が25 (actual={meta.TerrainTileCount})");

    // 2: シーンに実際に生えたTerrainを名前で引く。構築ログのtiles=は下でActiveTerrainsと突き合わせる
    // 2: Look up the terrains actually standing in the scene by name; the build log's tiles= is cross-checked against them below
    var side = Mathf.RoundToInt(Mathf.Sqrt(meta.TerrainTileCount));
    var terrains = new Dictionary<Vector2Int, UnityEngine.Terrain>();
    foreach (var terrain in UnityEngine.Terrain.activeTerrains)
    {
        var parts = terrain.name.Split('_');
        if (parts.Length != 3 || parts[0] != "Terrain") continue;
        terrains[new Vector2Int(int.Parse(parts[1]), int.Parse(parts[2]))] = terrain;
    }
    p.Note($"scene terrains: activeTerrains={UnityEngine.Terrain.activeTerrains.Length} named Terrain_x_z={terrains.Count} gridSide={side}");
    p.Assert(terrains.Count == 25, $"シーンにTerrain_x_zが25枚立っている (actual={terrains.Count})");

    var tile00 = terrains[new Vector2Int(0, 0)];
    var tileWidth = tile00.terrainData.size.x;
    var tileLength = tile00.terrainData.size.z;
    p.Note($"tile size={tile00.terrainData.size} heightmapRes={tile00.terrainData.heightmapResolution} alphamapRes={tile00.terrainData.alphamapResolution} detailRes={tile00.terrainData.detailResolution} layers={tile00.terrainData.terrainLayers.Length} detailPrototypes={tile00.terrainData.detailPrototypes.Length}");

    // 3: スポーン。ハンドシェイク座標に立ち、その真下に地形があること
    // 3: Spawn; the player stands at the handshake position with terrain underneath it
    var handshake = ClientDIContext.DIContainer.DIContainerResolver.Resolve<InitialHandshakeResponse>();
    var playerPosition = p.PlayerPosition;
    var hasGround = SlopeBlockPlaceSystem.TryGetGroundPoint(playerPosition.x, playerPosition.z, out var groundPoint);
    p.Note($"spawn handshake={handshake.PlayerPos} player={playerPosition} ground={hasGround}/{groundPoint}");
    p.Assert(hasGround, $"自機の真下に地形がある pos={playerPosition}");
    p.Assert(Mathf.Abs(playerPosition.y - groundPoint.y) < 5f, $"自機が地表付近にいる playerY={playerPosition.y} groundY={groundPoint.y}");
    await p.Screenshot("01-spawn-view");

    // 4: 高さの連続性。隣接タイルの共有辺で同じ世界座標をそれぞれのTerrainからサンプルし、差分の最大/平均を出す
    //    木の高さ摂動がhalo無しSliceで落ちていれば、木の生えた境界だけが数m跳ねる（bd moorestech-edd.5）
    // 4: Height continuity; sample the same world position from both terrains along each shared edge and report max/mean delta.
    //    A halo-less slice dropping the tree perturbation makes only the tree-bearing seams jump meters (bd moorestech-edd.5)
    p.Note("タイル境界の高さ連続性を数値で測る");
    var seamReport = new StringBuilder();
    var worstSeamDelta = 0f;
    var worstSeamLabel = "none";
    for (var tileZ = 0; tileZ < side; tileZ++)
    {
        for (var tileX = 0; tileX < side; tileX++)
        {
            if (tileX + 1 < side) MeasureSeam(new Vector2Int(tileX, tileZ), new Vector2Int(tileX + 1, tileZ), true);
            if (tileZ + 1 < side) MeasureSeam(new Vector2Int(tileX, tileZ), new Vector2Int(tileX, tileZ + 1), false);
        }
    }
    p.Note($"seam worst: {worstSeamLabel} delta={worstSeamDelta:F3}m");
    p.Note("seam detail: " + seamReport.ToString());
    p.Assert(worstSeamDelta < 0.5f, $"タイル境界の高さ差が0.5m未満 (worst={worstSeamDelta:F3}m at {worstSeamLabel})");

    // 5: splatとdetailの実データ。R6の岩まわり裸地とR9の草がタイルに焼かれているかを層ごとの被覆率で見る
    // 5: Real splat and detail data; per-layer coverage shows whether R6's bare rock skirt and R9's grass were baked into the tile
    p.Note("splat/detailの実データを層ごとに集計する（spawnタイルと中央タイル）");
    var spawnTileX = Mathf.Clamp(Mathf.FloorToInt((playerPosition.x - meta.SceneOrigin.X) / tileWidth), 0, side - 1);
    var spawnTileZ = Mathf.Clamp(Mathf.FloorToInt((playerPosition.z - meta.SceneOrigin.Y) / tileLength), 0, side - 1);
    ReportTileVisuals(new Vector2Int(spawnTileX, spawnTileZ), "spawn");
    ReportTileVisuals(new Vector2Int(side / 2, side / 2), "center");
    ReportTileVisuals(new Vector2Int(0, 0), "corner00");

    // 6: 調査用カメラ。ゲームカメラはCinemachineが毎フレーム上書きするので、depthを上げた別カメラで俯瞰を撮る
    // 6: Survey camera; Cinemachine overwrites the game camera every frame, so a separate higher-depth camera takes the overhead shots
    var surveyCameraObject = new GameObject("PlaytestSurveyCamera");
    var surveyCamera = surveyCameraObject.AddComponent<Camera>();
    surveyCamera.depth = 100f;
    surveyCamera.clearFlags = CameraClearFlags.Skybox;
    surveyCamera.farClipPlane = 20000f;
    surveyCamera.nearClipPlane = 1f;

    var worldOriginX = meta.SceneOrigin.X;
    var worldOriginZ = meta.SceneOrigin.Y;
    var worldCenter = new Vector3(worldOriginX + side * tileWidth * 0.5f, 0f, worldOriginZ + side * tileLength * 0.5f);

    // 6-1: 世界全体の俯瞰。海岸線の形とタイル格子が絵として出るか
    // 6-1: Whole-world overhead; whether the coastline shape and the tile lattice show up in the picture
    p.Note("世界全体(5000m角)を真上から撮る");
    await LookDown(worldCenter, 4200f, true, side * tileWidth * 0.52f);
    await p.Screenshot("02-world-overhead-ortho");

    p.Note("世界全体を斜めから撮る");
    await LookFrom(worldCenter + new Vector3(0f, 2600f, -4200f), worldCenter, false, 0f);
    await p.Screenshot("03-world-oblique");

    // 6-2: 内部境界の真上。splat/detailの直線シームはここでしか見えない
    // 6-2: Straight overhead on the internal seams, the only place a splat/detail seam line shows
    for (var boundary = 1; boundary < side; boundary++)
    {
        var seamX = worldOriginX + boundary * tileWidth;
        p.Note($"X={seamX:F0} のタテ境界を真上から撮る");
        await LookDown(new Vector3(seamX, 0f, worldCenter.z), 700f, true, 320f);
        await p.Screenshot($"04-seam-x{boundary}-overhead");

        var seamZ = worldOriginZ + boundary * tileLength;
        p.Note($"Z={seamZ:F0} のヨコ境界を真上から撮る");
        await LookDown(new Vector3(worldCenter.x, 0f, seamZ), 700f, true, 320f);
        await p.Screenshot($"05-seam-z{boundary}-overhead");
    }

    // 6-3: 中央の十字交点を斜めから。高さの段差は真上からは見えないので斜俯瞰で見る
    // 6-3: Oblique on the central crossing; a height step is invisible from straight above
    var crossing = new Vector3(worldOriginX + 3f * tileWidth, 0f, worldOriginZ + 3f * tileLength);
    var crossingGroundY = SampleWorldHeight(crossing);
    p.Note($"中央の境界交点({crossing.x:F0},{crossing.z:F0}) を斜め低空から撮る groundY={crossingGroundY:F1}");
    await LookFrom(crossing + new Vector3(-260f, crossingGroundY + 130f, -260f), crossing + new Vector3(0f, crossingGroundY, 0f), false, 0f);
    await p.Screenshot("06-seam-crossing-oblique");

    // 6-4: 地表すれすれ。detail(草)はdetailObjectDistance=200m以内でしか描かれないので接地高度から撮る
    // 6-4: Just above the ground; details are only drawn within detailObjectDistance=200m, so shoot from ground level
    p.Note("スポーン地点の地表すれすれから草(detail)を撮る");
    var groundEye = new Vector3(playerPosition.x, groundPoint.y + 3f, playerPosition.z - 25f);
    await LookFrom(groundEye, new Vector3(playerPosition.x, groundPoint.y + 1f, playerPosition.z + 30f), false, 0f);
    await p.Screenshot("07-spawn-ground-detail");

    // 6-5: 岩まわり。guidでグルーピングし最大スケールの個体へ寄る（R6の裸地帯の目視）
    // 6-5: Around the rocks; group by guid and close in on the largest-scale instance (visual check of R6's bare skirt)
    var rockLike = mapLayout.MapObjects.GroupBy(o => o.MapObjectGuid).OrderByDescending(g => g.Max(o => o.ScaleY)).ToList();
    p.Note($"mapObjects total={mapLayout.MapObjects.Count} guidGroups={rockLike.Count}");
    if (0 < rockLike.Count)
    {
        var rock = rockLike.First().OrderByDescending(o => o.ScaleY).First();
        var rockPosition = new Vector3(rock.X, rock.Y, rock.Z);
        p.Note($"最大スケール個体 guid={rock.MapObjectGuid} pos={rockPosition} scale=({rock.ScaleX},{rock.ScaleY},{rock.ScaleZ})");
        await LookFrom(rockPosition + new Vector3(-45f, 30f, -45f), rockPosition, false, 0f);
        await p.Screenshot("08-rock-surround-oblique");
        await LookDown(rockPosition, SampleWorldHeight(rockPosition) + 140f, true, 70f);
        await p.Screenshot("09-rock-surround-overhead");
    }

    // 6-6: 海岸線がタイル境界を横切る箇所。海面高さ付近の地表を境界線上で探して寄る（bd moorestech-edd.1の判定材料）
    // 6-6: Where the coastline crosses a tile seam; search the seam line for near-sea-level ground and close in (evidence for bd moorestech-edd.1)
    var coastPoint = FindCoastOnSeam();
    if (coastPoint.HasValue)
    {
        p.Note($"海岸がタイル境界を横切る点 {coastPoint.Value} を斜め低空から撮る");
        await LookFrom(coastPoint.Value + new Vector3(-160f, 90f, -160f), coastPoint.Value, false, 0f);
        await p.Screenshot("10-coast-on-seam-oblique");
        await LookDown(coastPoint.Value, 400f, true, 200f);
        await p.Screenshot("11-coast-on-seam-overhead");
    }
    else
    {
        p.Note("海岸がタイル境界を横切る点が見つからなかった");
    }

    // 7: 調査カメラを畳んでゲーム画面へ戻す
    // 7: Fold the survey camera away and return to the game view
    UnityEngine.Object.DestroyImmediate(surveyCameraObject);
    await p.WaitSeconds(1f);
    await p.Screenshot("12-back-to-game-view");
    p.Note("地形調査を終了する");

    #region Internal

    void MeasureSeam(Vector2Int a, Vector2Int b, bool alongX)
    {
        var terrainA = terrains[a];
        var terrainB = terrains[b];
        var maxDelta = 0f;
        var sumDelta = 0.0;
        var samples = 0;

        // 共有辺の座標。alongX ならタイルAの+X側の辺、そうでなければ+Z側の辺
        // Shared edge coordinates: the +X edge of tile A when alongX, otherwise its +Z edge
        var seamX = terrainA.transform.position.x + (alongX ? tileWidth : 0f);
        var seamZ = terrainA.transform.position.z + (alongX ? 0f : tileLength);
        for (var step = 0; step <= 100; step++)
        {
            var t = step / 100f;
            var position = alongX
                ? new Vector3(seamX, 0f, terrainA.transform.position.z + t * tileLength)
                : new Vector3(terrainA.transform.position.x + t * tileWidth, 0f, seamZ);

            var heightA = terrainA.SampleHeight(position) + terrainA.transform.position.y;
            var heightB = terrainB.SampleHeight(position) + terrainB.transform.position.y;
            var delta = Mathf.Abs(heightA - heightB);
            maxDelta = Mathf.Max(maxDelta, delta);
            sumDelta += delta;
            samples++;
        }

        var label = $"{a.x},{a.y}->{b.x},{b.y}";
        seamReport.Append($"[{label} max={maxDelta:F3} mean={(sumDelta / samples):F3}]");
        if (worstSeamDelta < maxDelta)
        {
            worstSeamDelta = maxDelta;
            worstSeamLabel = label;
        }
    }

    void ReportTileVisuals(Vector2Int tile, string label)
    {
        var terrainData = terrains[tile].terrainData;

        // 全画素を読むとGB級になるので、タイル中央の窓だけを読んで被覆率を出す
        // Reading every pixel would run into gigabytes, so only a window at the tile center is read for coverage
        var alphaWindow = Mathf.Min(256, terrainData.alphamapResolution);
        var alphaStart = (terrainData.alphamapResolution - alphaWindow) / 2;
        var alphamaps = terrainData.GetAlphamaps(alphaStart, alphaStart, alphaWindow, alphaWindow);
        var layerNames = terrainData.terrainLayers.Select(layer => layer == null ? "<null>" : layer.name).ToArray();
        var layerSums = new double[layerNames.Length];
        for (var y = 0; y < alphaWindow; y++)
            for (var x = 0; x < alphaWindow; x++)
                for (var layer = 0; layer < layerNames.Length; layer++)
                    layerSums[layer] += alphamaps[y, x, layer];

        var pixels = (double)alphaWindow * alphaWindow;
        var splatText = string.Join(" ", Enumerable.Range(0, layerNames.Length)
            .Where(layer => 0.0001 < layerSums[layer] / pixels)
            .OrderByDescending(layer => layerSums[layer])
            .Select(layer => $"{layerNames[layer]}={(layerSums[layer] / pixels * 100.0):F2}%"));
        p.Note($"splat[{label} tile{tile.x},{tile.y}] layers={layerNames.Length} : {splatText}");

        var detailWindow = Mathf.Min(256, terrainData.detailResolution);
        var detailStart = (terrainData.detailResolution - detailWindow) / 2;
        var detailTotals = new List<string>();
        var detailGrandTotal = 0L;
        for (var prototype = 0; prototype < terrainData.detailPrototypes.Length; prototype++)
        {
            var layer = terrainData.GetDetailLayer(detailStart, detailStart, detailWindow, detailWindow, prototype);
            var total = 0L;
            for (var y = 0; y < detailWindow; y++)
                for (var x = 0; x < detailWindow; x++)
                    total += layer[y, x];
            detailGrandTotal += total;
            if (0 < total) detailTotals.Add($"proto{prototype}={total}");
        }
        p.Note($"detail[{label} tile{tile.x},{tile.y}] prototypes={terrainData.detailPrototypes.Length} total={detailGrandTotal} : {string.Join(" ", detailTotals)}");
    }

    float SampleWorldHeight(Vector3 position)
    {
        return SlopeBlockPlaceSystem.TryGetGroundPoint(position.x, position.z, out var point) ? point.y : 0f;
    }

    // タイル境界線上を歩いて、海面(y=0付近)をまたぐ地表の点を探す
    // Walks the seam lines looking for ground that straddles sea level (around y=0)
    Vector3? FindCoastOnSeam()
    {
        for (var boundary = 1; boundary < side; boundary++)
        {
            var seamX = worldOriginX + boundary * tileWidth;
            for (var step = 0; step <= 400; step++)
            {
                var z = worldOriginZ + step / 400f * (side * tileLength);
                var height = SampleWorldHeight(new Vector3(seamX, 0f, z));
                if (0.2f < height && height < 6f) return new Vector3(seamX, height, z);
            }

            var seamZ = worldOriginZ + boundary * tileLength;
            for (var step = 0; step <= 400; step++)
            {
                var x = worldOriginX + step / 400f * (side * tileWidth);
                var height = SampleWorldHeight(new Vector3(x, 0f, seamZ));
                if (0.2f < height && height < 6f) return new Vector3(x, height, seamZ);
            }
        }
        return null;
    }

    async UniTask LookDown(Vector3 target, float altitude, bool orthographic, float orthographicSize)
    {
        surveyCamera.orthographic = orthographic;
        surveyCamera.orthographicSize = orthographicSize;
        surveyCameraObject.transform.position = new Vector3(target.x, altitude, target.z);
        surveyCameraObject.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        await p.WaitSeconds(0.7f);
    }

    async UniTask LookFrom(Vector3 eye, Vector3 target, bool orthographic, float orthographicSize)
    {
        surveyCamera.orthographic = orthographic;
        surveyCamera.orthographicSize = orthographicSize;
        surveyCameraObject.transform.position = eye;
        surveyCameraObject.transform.rotation = Quaternion.LookRotation((target - eye).normalized, Vector3.up);
        await p.WaitSeconds(0.7f);
    }

    #endregion
});
