// 設置照準1本の最悪時間を測る。占有セルの外接直方体の外1マスから、中心の高さ・中心の Z（Z 方向は中心の X）で +X / +Z へ水平に撃ち、全域を走査する
// Measure the worst-case cost of one placement aim: shoot horizontally along +X / +Z from one cell outside the occupied-cell bounds at the center height, so the whole box is traversed
// 各1000回の平均 ms と、たどるセル数の目安（外接直方体の辺の長さ）を返す。設置モード外でも TryTrace を直接呼ぶので、定常後のワールドで実行する
// Returns the mean ms over 1000 shots each and the cells traversed (bounds edge length); TryTrace is called directly so it works outside build mode, run it on a settled world
using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
using Client.Game.InGame.Context;
using UnityEngine;
var cells = ClientDIContext.BlockGameObjectDataStore.OccupiedCells;
if (!cells.TryGetBounds(out var minCell, out var maxCell)) return "{\"error\":\"no occupied cells\"}";
var min = new Vector3(minCell.x, minCell.y, minCell.z);
var max = new Vector3(maxCell.x + 1, maxCell.y + 1, maxCell.z + 1);
var center = (min + max) * 0.5f;
const int shots = 1000;
var rayX = new Ray(new Vector3(min.x - 1f, center.y, center.z), Vector3.right);
var rayZ = new Ray(new Vector3(center.x, center.y, min.z - 1f), Vector3.forward);
var sb = new System.Text.StringBuilder("{\"shots\":" + shots);
ShootAndTime("x", rayX, max.x - min.x + 1f);
ShootAndTime("z", rayZ, max.z - min.z + 1f);
sb.Append("}");
return sb.ToString();

#region Internal

void ShootAndTime(string axis, Ray ray, float cellsTraversed)
{
    // 1回目は JIT を含むので捨てる
    // Discard the first shot since it includes JIT
    PlacementAimCellTracer.TryTrace(cells, ray, float.PositiveInfinity, out _, out _);
    var hits = 0;
    var watch = System.Diagnostics.Stopwatch.StartNew();
    for (var i = 0; i < shots; i++)
        if (PlacementAimCellTracer.TryTrace(cells, ray, float.PositiveInfinity, out _, out _)) hits++;
    watch.Stop();
    sb.Append(",\"" + axis + "\":{\"meanMs\":" + (watch.Elapsed.TotalMilliseconds / shots).ToString("F4", System.Globalization.CultureInfo.InvariantCulture) + ",\"cellsTraversed\":" + cellsTraversed + ",\"hits\":" + hits + "}");
}

#endregion
