// 設置照準1本の最悪時間を測る（ADR 0100 決定9）。占有セルの外接直方体を水平に横切り、何にも当たらない（区間を最後まで走査する）照準を撃つ
// Measure the worst-case cost of one placement aim (ADR 0100 decision 9): horizontal rays that cross the occupied-cell bounds and hit nothing, so the whole span is walked
// 向きは +X・+Z の軸方向と、XZ の対角2本（角の外から反対の角の外へ）。各向きで横ずれと高さを変えて外れる線を探し、1000回の平均 ms を返す
// Directions are +X, +Z and both XZ diagonals (outside one corner to outside the opposite corner); per direction, lateral shifts and heights are searched for a missing line, then the mean ms over 1000 shots is returned
// 設置モード外でも TryTrace を直接呼ぶので、定常後のワールドで実行する
// TryTrace is called directly so it works outside build mode; run it on a settled world
using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
using Client.Game.InGame.Context;
using UnityEngine;

var cells = ClientDIContext.BlockGameObjectDataStore.OccupiedCells;
if (!cells.TryGetBounds(out var minCell, out var maxCell)) return "{\"error\":\"no occupied cells\"}";
var min = new Vector3(minCell.x, minCell.y, minCell.z);
var max = new Vector3(maxCell.x + 1, maxCell.y + 1, maxCell.z + 1);
var centerX = Mathf.Floor((min.x + max.x) * 0.5f) + 0.5f;
var centerZ = Mathf.Floor((min.z + max.z) * 0.5f) + 0.5f;
const int shots = 1000;
const int maxCandidates = 20000;
var inv = System.Globalization.CultureInfo.InvariantCulture;
var sb = new System.Text.StringBuilder("{\"shots\":" + shots + ",\"boundsMin\":\"" + minCell + "\",\"boundsMax\":\"" + maxCell + "\"");

// 横ずれは外接直方体の幅いっぱいまで試す。直方体の外を通る線は走査しないので候補から外す
// Lateral shifts reach the full bounds width; lines passing outside the bounds are not walked, so they are skipped
var lateralRange = Mathf.RoundToInt(Mathf.Max(max.x - min.x, max.z - min.z)) + 1;

// 軸方向は外接直方体の外1マスのセル中心から撃つ。どの行も区間の長さは同じ
// Axis rays start at the cell center one cell outside the bounds; every row has the same span length
ShootAndTime("x", new Vector3(min.x - 0.5f, 0f, centerZ), Vector3.right, Vector3.forward);
ShootAndTime("z", new Vector3(centerX, 0f, min.z - 0.5f), Vector3.forward, Vector3.right);

// 対角は角の外のセル中心から反対の角の外のセル中心へ撃つ。横ずれ 0 が最長で、ずれるほど区間が短くなる
// Diagonals go from the cell center outside one corner to the one outside the opposite corner; shift 0 is longest and shifts shorten the span
var startA = new Vector3(min.x - 0.5f, 0f, min.z - 0.5f);
var startB = new Vector3(max.x + 0.5f, 0f, min.z - 0.5f);
var directionA = (new Vector3(max.x + 0.5f, 0f, max.z + 0.5f) - startA).normalized;
var directionB = (new Vector3(min.x - 0.5f, 0f, max.z + 0.5f) - startB).normalized;
ShootAndTime("diagonalXZ", startA, directionA, Vector3.right);
ShootAndTime("diagonalMinusXZ", startB, directionB, Vector3.left);
sb.Append("}");
return sb.ToString();

#region Internal

void ShootAndTime(string name, Vector3 start, Vector3 direction, Vector3 lateral)
{
    // 横ずれ 0, +1, -1, +2, ... の順（対角では長い線から）に、各ずれで上の層から何にも当たらない線を探す
    // In shift order 0, +1, -1, +2, ... (longest first for diagonals), try each layer from the top until a line hits nothing
    var ray = default(Ray);
    var candidates = 0;
    var found = false;
    for (var i = 0; i <= lateralRange * 2 && !found && candidates < maxCandidates; i++)
    for (var y = maxCell.y; y >= minCell.y && !found && candidates < maxCandidates; y--)
    {
        var shift = (i + 1) / 2 * (i % 2 == 0 ? -1 : 1);
        ray = new Ray(new Vector3(start.x, y + 0.5f, start.z) + lateral * shift, direction);
        if (CellsInBounds(ray) == 0) continue;
        candidates++;
        found = !PlacementAimCellTracer.TryTrace(cells, ray, float.PositiveInfinity, out _, out _);
    }
    var longest = CellsInBounds(new Ray(new Vector3(start.x, minCell.y + 0.5f, start.z), direction));
    if (!found)
    {
        sb.Append(",\"" + name + "\":{\"error\":\"no miss line found\",\"candidates\":" + candidates + ",\"longestCells\":" + longest + "}");
        return;
    }

    // 1回目は JIT を含むので捨てる
    // Discard the first shot since it includes JIT
    PlacementAimCellTracer.TryTrace(cells, ray, float.PositiveInfinity, out _, out _);
    var hits = 0;
    var watch = System.Diagnostics.Stopwatch.StartNew();
    for (var i = 0; i < shots; i++)
        if (PlacementAimCellTracer.TryTrace(cells, ray, float.PositiveInfinity, out _, out _)) hits++;
    watch.Stop();
    sb.Append(",\"" + name + "\":{\"meanMs\":" + (watch.Elapsed.TotalMilliseconds / shots).ToString("F4", inv) + ",\"cellsTraversed\":" + CellsInBounds(ray)
              + ",\"longestCells\":" + longest + ",\"hits\":" + hits + ",\"origin\":\"" + ray.origin.ToString("F2") + "\",\"candidates\":" + candidates + "}");
}

int CellsInBounds(Ray ray)
{
    // 外接直方体の中で入るセルの数は、区間での各軸の移動量の和 Δx+Δy+Δz（軸方向なら辺の長さ、水平な対角なら Δx+Δz）。出口の境界で外へ1回引く分は含めない
    // Cells entered inside the bounds equal the summed per-axis travel Δx+Δy+Δz over the clipped span (edge length for an axis ray, Δx+Δz for a horizontal diagonal); the one lookup past the exit boundary is not counted
    var tEnter = float.NegativeInfinity;
    var tExit = float.PositiveInfinity;
    for (var axis = 0; axis < 3; axis++)
    {
        if (ray.direction[axis] == 0f)
        {
            if (ray.origin[axis] < min[axis] || ray.origin[axis] > max[axis]) return 0;
            continue;
        }
        var t1 = (min[axis] - ray.origin[axis]) / ray.direction[axis];
        var t2 = (max[axis] - ray.origin[axis]) / ray.direction[axis];
        tEnter = Mathf.Max(tEnter, Mathf.Min(t1, t2));
        tExit = Mathf.Min(tExit, Mathf.Max(t1, t2));
    }
    var span = Mathf.Max(0f, tExit - tEnter);
    return Mathf.RoundToInt(span * (Mathf.Abs(ray.direction.x) + Mathf.Abs(ray.direction.y) + Mathf.Abs(ray.direction.z)));
}

#endregion
