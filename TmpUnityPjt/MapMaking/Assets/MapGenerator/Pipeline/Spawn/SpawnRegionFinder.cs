using System.Collections.Generic;
using System.Text;
using UnityEngine;
using MapGenerator.Pipeline.Biomes;

namespace MapGenerator.Pipeline.Spawn
{
    /// <summary>
    /// 本番生成前に草原-森林隣接の良地を探索し、スポーン地点Sと中央化オフセットG=S-spawnTarget(描画位置, 既定はgridCenter)を算出する。
    /// 段1(raw粗探索)で候補ペアを発見し、段2(本番一致final検証)で確定する。
    /// </summary>
    public static class SpawnRegionFinder
    {
        // 段1の候補（粗グリッド上、草原CC1つにつき1件）
        sealed class Candidate
        {
            public Vector2 ApproxCenterWorld; // 草原CC重心(粗・ワールドm)
            public Vector2 BBoxCenterWorld;   // 草原CC bbox中心(粗・ワールドm)
            public float Score;               // 段1スコア（降順ソート用）
            public float BBoxW;               // 草原CCのワールドbbox幅(m)
            public float BBoxH;               // 草原CCのワールドbbox高(m)
            public CoarseBiomeGrid SourceGrid;          // この候補が生まれた粗グリッド
            public List<int> Stage1GrassCells;          // 粗グリッド上の草原CCセルindex
        }

        public static SpawnSearchResult Find(
            TerrainGenerationConfig config, BiomeType[] biomeTypes)
        {
            var sb = new StringBuilder();
            var ss = config.spawnSearch;
            // gridCenter を最上部で一度だけ算出し、全フォールバック経路で再利用する
            // （フォールバック時の spawn=spawnTarget により ore帯中心がマップ中心に揃う。zeroだと原点に化ける）
            Vector2 gridCenter = GridCenterWorld(config);
            // 中央化オフセットの打ち消し先（探索で見つけた良地を実際に描画するシーン座標）。
            // 既定はグリッド幾何中心。overrideSpawnScenePosition で任意のシーン座標に変更可能。
            // 段1の探索中心は gridCenter のまま（どのノイズ領域を選ぶかは描画位置と独立）。
            Vector2 spawnTarget = ss.overrideSpawnScenePosition ? ss.spawnScenePosition : gridCenter;

            // 不正パラメータ(<=0)で数学が破綻する2つを防御（[Min]に加えた belt-and-suspenders）
            if (ss.scanCellSize <= 0f || ss.topK <= 0)
            {
                Debug.LogError("[SpawnSearch] scanCellSize/topK が不正(<=0)。探索を中止しフォールバック。");
                return new SpawnSearchResult(false, Vector2.zero, spawnTarget, 0f, "invalid spawnSearch params");
            }

            int grassBi = System.Array.IndexOf(biomeTypes, BiomeType.Grassland);
            int forestBi = System.Array.IndexOf(biomeTypes, BiomeType.Forest);
            if (grassBi < 0 || forestBi < 0)
                return new SpawnSearchResult(false, Vector2.zero, spawnTarget, 0f,
                    "Grassland/Forestが有効バイオームに含まれない");

            float extent = ss.scanExtent > 0f ? ss.scanExtent : DefaultScanExtent(config);

            // spawnTarget が本番サンプル格子上に乗っているか確認（中央化の本番一致保証の前提）。
            // 描画位置がピクセル中心に乗らないと予測(段2)と本番のスポーン地点がサブピクセルずれる。
            // 非デフォルト構成や任意の描画位置指定では格子外になりうるため、throwせず警告＋診断記録で可視化する。
            float pX = config.terrainWidth / (config.Resolution - 1);
            float pZ = config.terrainLength / (config.Resolution - 1);
            bool latticeX = Mathf.Abs(spawnTarget.x / pX - Mathf.Round(spawnTarget.x / pX)) < 1e-3f;
            bool latticeZ = Mathf.Abs(spawnTarget.y / pZ - Mathf.Round(spawnTarget.y / pZ)) < 1e-3f;
            if (!latticeX || !latticeZ)
            {
                string warn = $"[SpawnSearch] 警告: スポーン描画位置({spawnTarget.x:F2},{spawnTarget.y:F2}) が本番サンプル格子(pX={pX:F4},pZ={pZ:F4})に乗っていません。スポーン中央化の本番一致保証が崩れる可能性があります（非デフォルトの terrainWidth/gridSize/解像度、または描画位置指定が原因）。";
                UnityEngine.Debug.LogWarning(warn);
                sb.AppendLine(warn);
            }

            for (int iter = 0; iter <= ss.maxExpandIterations; iter++)
            {
                sb.AppendLine($"[iter {iter}] extent={extent:F0}m cell={ss.scanCellSize:F0}m");

                // ---- 段1: 粗探索 ----
                var grid = TerrainGenerator.ClassifyRawGrid(
                    config, biomeTypes, gridCenter.x, gridCenter.y, extent, ss.scanCellSize);
                var candidates = FindCandidates(grid, grassBi, forestBi, ss, sb);

                // ---- 段2: バッチ検証（topK初期バッチ、validが出るまで次バッチ） ----
                // Score降順、同点はApproxCenterWorld.x→.y昇順で決定的に解決（List.Sortの不安定性を排除）
                candidates.Sort((a, b) =>
                {
                    int c = b.Score.CompareTo(a.Score);
                    if (c != 0) return c;
                    c = a.ApproxCenterWorld.x.CompareTo(b.ApproxCenterWorld.x);
                    if (c != 0) return c;
                    return a.ApproxCenterWorld.y.CompareTo(b.ApproxCenterWorld.y);
                });
                var verified = new List<(SpawnSearchResult res, float finalScore, Vector2 spawn)>();
                int verifiedCount = 0;
                foreach (var cand in candidates)
                {
                    verifiedCount++;
                    var vr = VerifyCandidate(config, biomeTypes, grassBi, forestBi, cand, ss, spawnTarget, sb);
                    if (vr.HasValue) verified.Add(vr.Value);

                    // 初期バッチ消化後にvalidが1つでもあれば打ち切って採用へ（現在範囲優先・性能）
                    if (verifiedCount >= ss.topK && verified.Count > 0) break;
                }

                if (verified.Count > 0)
                {
                    // finalScore降順、同点はS.x→.y昇順で決定的に解決
                    verified.Sort((a, b) =>
                    {
                        int c = b.finalScore.CompareTo(a.finalScore);
                        if (c != 0) return c;
                        c = a.spawn.x.CompareTo(b.spawn.x);
                        if (c != 0) return c;
                        return a.spawn.y.CompareTo(b.spawn.y);
                    });
                    sb.AppendLine($"採用: score={verified[0].finalScore:F1} (検証{verifiedCount}件)");
                    var best = verified[0].res;
                    return new SpawnSearchResult(true, best.WorldOffset, best.SpawnWorldPosition,
                        verified[0].finalScore, sb.ToString());
                }

                if (verifiedCount >= ss.topK)
                    sb.AppendLine($"探索打ち切り: topK={ss.topK}件検証もvalidなし（保証ではない）");
                extent *= ss.expandFactor;
            }

            // フォールバック: オフセット0だが spawn は spawnTarget を返し、本番の中央化基準と整合させる
            // （Fallback(zero) は spawn=0 になり、ore帯中心が誤ったワールド位置を指してしまう）
            sb.AppendLine("最終的に候補ゼロ → オフセット0フォールバック");
            return new SpawnSearchResult(false, Vector2.zero, spawnTarget, 0f, sb.ToString());
        }

        // 段1: CC抽出 + 隣接ペアスコアリング
        static List<Candidate> FindCandidates(
            CoarseBiomeGrid grid, int grassBi, int forestBi, SpawnSearchConfig ss, StringBuilder sb)
        {
            float cellArea = grid.CellSize * grid.CellSize; // m²/cell
            var grassComps = ConnectedComponents.Label(grid.BiomeIndex, grid.Width, grid.Height, v => v == grassBi);
            var forestComps = ConnectedComponents.Label(grid.BiomeIndex, grid.Width, grid.Height, v => v == forestBi);

            var result = new List<Candidate>();
            foreach (var g in grassComps)
            {
                if (g.Area * cellArea < ss.minGrasslandArea) continue;

                // 草原CC1つにつき候補1件: 接触長が最大のForest CCのみを採用してペアを代表させる。
                // （同一草原CCがN個のForestに隣接しても同じ窓を段2でN回回さないようdedupe）
                ConnectedComponents.Component bestForest = null;
                float bestContactM = 0f;
                foreach (var f in forestComps)
                {
                    if (f.Area * cellArea < ss.minForestArea) continue;
                    int contactCells = ConnectedComponents.BorderContactCells(g, f, grid.BiomeIndex, grid.Width, grid.Height);
                    float contactM = contactCells * grid.CellSize;
                    if (contactM > bestContactM) { bestContactM = contactM; bestForest = f; }
                }
                if (bestForest == null || bestContactM < ss.minBorderContact) continue;

                // 草原CC重心（粗・ワールド）
                Vector2 centroid = CentroidWorld(g, grid);
                float grassM2 = g.Area * cellArea;
                float forestM2 = bestForest.Area * cellArea;
                // 段1スコアは wInland を含まない: 粗パスには距離変換がなくクリアランスを測れないため
                // （wInland は段2 finalScore でのみ寄与する）。
                float score = ss.wGrasslandArea * grassM2
                            + ss.wForestArea * forestM2
                            + ss.wBorderContact * bestContactM;
                result.Add(new Candidate
                {
                    ApproxCenterWorld = centroid,
                    BBoxCenterWorld = new Vector2(
                        grid.OriginX + ((g.MinX + g.MaxX) * 0.5f) * grid.CellSize,
                        grid.OriginZ + ((g.MinY + g.MaxY) * 0.5f) * grid.CellSize),
                    Score = score,
                    BBoxW = (g.MaxX - g.MinX + 1) * grid.CellSize,
                    BBoxH = (g.MaxY - g.MinY + 1) * grid.CellSize,
                    SourceGrid = grid,
                    Stage1GrassCells = g.Cells
                });
            }
            sb.AppendLine($"  段1候補: {result.Count}");
            return result;
        }

        // 段2: 本番一致final検証 + pole of inaccessibility
        static (SpawnSearchResult res, float finalScore, Vector2 spawn)? VerifyCandidate(
            TerrainGenerationConfig config, BiomeType[] biomeTypes, int grassBi, int forestBi,
            Candidate cand, SpawnSearchConfig ss, Vector2 spawnTarget, StringBuilder sb)
        {
            // OOM防止はピクセル数依存なのでメートルではなく解像度で頭打ちにする。
            // RunClassificationDetailed の res=ceil(windowSize/pX)+1 を maxDetailedResolution 以下に抑える窓m上限を逆算。
            // （低解像度ではpXが大きくmaxWindowも大きくなり、edgeMarginを満たす十分な実メートル窓を確保できる）
            float pXLocal = config.terrainWidth / (config.Resolution - 1);
            float maxWindow = (ss.maxDetailedResolution - 1) * pXLocal;
            float windowSize = Mathf.Min(WindowSizeFor(cand, ss), maxWindow);
            var win = TerrainGenerator.RunClassificationDetailed(
                config, biomeTypes, cand.BBoxCenterWorld.x, cand.BBoxCenterWorld.y, windowSize);

            int w = win.Resolution, h = win.Resolution;
            int n = w * h;
            float cellAreaM2 = win.PitchX * win.PitchZ;

            // final Grassland / Forest CC
            var grassComps = ConnectedComponents.Label(win.WinnerBiomeIndex, w, h, v => v == grassBi);
            var forestComps = ConnectedComponents.Label(win.WinnerBiomeIndex, w, h, v => v == forestBi);
            if (grassComps.Count == 0 || forestComps.Count == 0) return null;

            // 段1↔段2のCC同一性をオーバーラップで担保:
            // 段1草原CCが覆う窓セル集合を作り、final草原CCの中で最大オーバーラップのものを採用。
            // （非凸領域でも頑健。重心包含判定では凹形状で誤マッチしうる）
            var grid = cand.SourceGrid;
            var stage1Cover = new HashSet<int>();
            foreach (int cidx in cand.Stage1GrassCells)
            {
                int ccx = cidx % grid.Width, ccy = cidx / grid.Width;
                float wx = grid.OriginX + ccx * grid.CellSize;
                float wz = grid.OriginZ + ccy * grid.CellSize;
                int cbx = Mathf.RoundToInt((wx - win.OriginX) / win.PitchX);
                int cby = Mathf.RoundToInt((wz - win.OriginZ) / win.PitchZ);
                if (cbx < 0 || cbx >= w || cby < 0 || cby >= h) continue;
                stage1Cover.Add(cby * w + cbx);
            }

            ConnectedComponents.Component gComp = null;
            int bestOverlap = 0;
            foreach (var gc in grassComps)
            {
                int overlap = 0;
                foreach (int idx in gc.Cells)
                    if (stage1Cover.Contains(idx)) overlap++;
                if (overlap > bestOverlap) { bestOverlap = overlap; gComp = gc; }
            }
            // 段1でスコアされた領域がfinalまで残らなかった → 棄却
            if (gComp == null || bestOverlap == 0) return null;
            if (gComp.Area * cellAreaM2 < ss.minGrasslandArea) return null;

            // 接触する最大のForest CC
            ConnectedComponents.Component bestForest = null;
            float bestContactM = 0f;
            foreach (var f in forestComps)
            {
                if (f.Area * cellAreaM2 < ss.minForestArea) continue;
                int contactCells = ConnectedComponents.BorderContactCells(gComp, f, win.WinnerBiomeIndex, w, h);
                float contactM = contactCells * win.PitchX;
                if (contactM > bestContactM) { bestContactM = contactM; bestForest = f; }
            }
            if (bestForest == null || bestContactM < ss.minBorderContact) return null;

            // edgeMargin内のセルは判定対象外（窓外ピクセル欠落でwinner/距離がズレる）
            float edgeMarginM = EdgeMargin(config, ss);
            int edgeMarginCells = Mathf.CeilToInt(edgeMarginM / win.PitchX);

            // 草原final maskと「非草原 or 海/Beach」マスクを作り距離変換
            var grassMask = new bool[n];
            foreach (int idx in gComp.Cells) grassMask[idx] = true;

            // grassClearance: 草原mask境界までの距離（セル→m）
            var grassDistCell = DistanceTransform.ChebyshevToFalse(grassMask, w, h);
            // waterMask: 陸(landMask>0.5 かつ beachFactor低)を true とし、海/Beachまでの距離を測る
            var landMaskBool = new bool[n];
            for (int i = 0; i < n; i++)
                // 本番(TerrainGenerator:859)はbeachFactor>0.2をビーチ扱い。水/ビーチからの距離なのでビーチ帯も水側に含める
                landMaskBool[i] = win.LandMask[i] > 0.5f && win.BeachFactor[i] <= 0.2f;
            var waterDistCell = DistanceTransform.ChebyshevToFalse(landMaskBool, w, h);

            // m換算 + edgeMargin内を失格化
            var grassClear = new float[n];
            var waterClear = new float[n];
            for (int i = 0; i < n; i++)
            {
                int x = i % w, y = i / w;
                bool inMargin = x < edgeMarginCells || x >= w - edgeMarginCells
                             || y < edgeMarginCells || y >= h - edgeMarginCells;
                if (!grassMask[i] || inMargin)
                {
                    grassClear[i] = 0f; waterClear[i] = 0f; continue;
                }
                grassClear[i] = grassDistCell[i] * win.PitchX;
                waterClear[i] = waterDistCell[i] * win.PitchX;
            }

            int best = DistanceTransform.PickPole(grassClear, waterClear, n,
                ss.grassClearanceMin, ss.waterClearanceMin);
            if (best < 0) return null;

            // スポーン点S（窓サンプル格子点・ワールド座標）
            int bx = best % w, by = best / w;
            float sx = win.OriginX + bx * win.PitchX;
            float sz = win.OriginZ + by * win.PitchZ;
            var S = new Vector2(sx, sz);

            // 中央化: G = S - spawnTarget（探索で見つけた S を、シーン座標 spawnTarget へ描画する）
            Vector2 G = S - spawnTarget;

            // final score（接触長・面積・スポーン点の最小クリアランスを総合）
            float minClear = Mathf.Min(grassClear[best], waterClear[best]);
            float finalScore = ss.wGrasslandArea * (gComp.Area * cellAreaM2)
                             + ss.wForestArea * (bestForest.Area * cellAreaM2)
                             + ss.wBorderContact * bestContactM
                             + ss.wInland * minClear;

            sb.AppendLine($"  段2 OK: S=({sx:F0},{sz:F0}) contact={bestContactM:F0}m minClear={minClear:F0}m");
            return (new SpawnSearchResult(true, G, S, finalScore, ""), finalScore, S);
        }

        // ---- ジオメトリ補助 ----

        static Vector2 CentroidWorld(ConnectedComponents.Component comp, CoarseBiomeGrid grid)
        {
            double sx = 0, sy = 0;
            foreach (int idx in comp.Cells)
            {
                int cx = idx % grid.Width;
                int cy = idx / grid.Width;
                sx += cx; sy += cy;
            }
            float ax = (float)(sx / comp.Cells.Count);
            float ay = (float)(sy / comp.Cells.Count);
            return new Vector2(grid.OriginX + ax * grid.CellSize, grid.OriginZ + ay * grid.CellSize);
        }

        static float DefaultScanExtent(TerrainGenerationConfig config)
        {
            // 生成グリッド外接（gridSize × チャンク幅）
            float w = config.gridSizeX * config.terrainWidth;
            float l = config.gridSizeZ * config.terrainLength;
            return Mathf.Max(w, l);
        }

        static Vector2 GridCenterWorld(TerrainGenerationConfig config)
        {
            // InfiniteTerrainManager は coord ∈ [-half, gridSize-half) でチャンクを配置し、
            // チャンク(coord)は worldOffset = coord*ChunkWidth に置かれる。
            // グリッド全体の幾何中心（オフセット0時）を返す。
            int halfX = config.gridSizeX / 2;
            int halfZ = config.gridSizeZ / 2;
            float minX = -halfX * config.terrainWidth;
            float maxX = (config.gridSizeX - halfX) * config.terrainWidth;
            float minZ = -halfZ * config.terrainLength;
            float maxZ = (config.gridSizeZ - halfZ) * config.terrainLength;
            return new Vector2((minX + maxX) * 0.5f, (minZ + maxZ) * 0.5f);
        }

        static float WindowSizeFor(Candidate cand, SpawnSearchConfig ss)
        {
            // 候補草原CCの粗bboxの長辺にマージンを足す。
            // （円相当直径では細長い領域を取り逃がし、窓切れ＋edgeMargin誤棄却を招く）
            float longSide = Mathf.Max(cand.BBoxW, cand.BBoxH);
            return longSide + 2f * ss.windowMargin;
        }

        static float EdgeMargin(TerrainGenerationConfig config, SpawnSearchConfig ss)
        {
            float pX = config.terrainWidth / (config.Resolution - 1);
            int divisor = Mathf.Max(1, config.boundaryConfig != null ? config.boundaryConfig.blurRadiusDivisor : 2);
            float blendM = (config.biomeBlendRadius + config.biomeBlendRadius / (float)divisor) * pX;
            float beachR = 0f;
            var shore = config.shoreConfig;
            if (shore != null)
                beachR = Mathf.Max(Mathf.Max(shore.beachLandTextureRadius, shore.beachLandTerrainRadius),
                                   Mathf.Max(shore.beachSeaTextureRadius, shore.beachSeaTerrainRadius));
            // beachR は段彩の beach*Radius（ピクセル単位）の合算なので m へ換算（blendM は既に *pX 済み）
            return blendM + ss.waterClearanceMin + beachR * pX;
        }
    }
}
