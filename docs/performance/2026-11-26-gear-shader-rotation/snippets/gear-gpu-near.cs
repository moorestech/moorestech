// 画面に写っていて RPM が 0 より大きいシェーダー回転の歯車を近い順に挙げ、画面上の位置と表の行の有無を返す
using System.Linq;
using System.Reflection;
using Client.Game.InGame.Block;
using Client.Game.InGame.Block.GearRotation.Gpu;
using Client.Game.InGame.BlockSystem.StateProcessor;
var cam = Camera.main;
var table = typeof(GearRotationGpuTable).GetField("Table", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null) as GearRotationGpuHashTable;
var rows = Object.FindObjectsByType<GearStateChangeProcessor>(FindObjectsSortMode.None)
    .Where(p => p.RotationInfos.Any(i => i != null && i.IsGpuDriven) && p.DebugCurrentGearState != null && p.DebugCurrentGearState.CurrentRpm > 0)
    .Select(p => {
        var host = p.GetComponentsInChildren<MeshRenderer>().FirstOrDefault(r => r.name.EndsWith(" (GearRotation)"));
        var pos = host != null ? host.bounds.center : p.transform.position;
        var sp = cam.WorldToScreenPoint(pos);
        var block = p.GetComponentInParent<BlockGameObject>();
        var found = table.TryFind(GearRotationGpuHashTable.ToKey(block.transform.position), out var row);
        return new { p, host, sp, dist = Vector3.Distance(cam.transform.position, pos), found, row, block };
    })
    .Where(x => x.sp.z > 0 && x.sp.x > 0 && x.sp.x < Screen.width && x.sp.y > 0 && x.sp.y < Screen.height)
    .OrderBy(x => x.dist).Take(8)
    .Select(x => x.block.name + " host=" + (x.host == null ? "none" : x.host.name) + " rpm=" + x.p.DebugCurrentGearState.CurrentRpm + " cw=" + x.p.DebugCurrentGearState.IsClockwise
        + " screen=(" + x.sp.x.ToString("F0") + "," + (Screen.height - x.sp.y).ToString("F0") + ") dist=" + x.dist.ToString("F1") + " row=" + x.found + " dps=" + x.row.SignedDegreesPerSecond + " base=" + x.row.BaseTime.ToString("F1"));
return "screen=" + Screen.width + "x" + Screen.height + " time=" + Time.time.ToString("F1") + "\n" + string.Join("\n", rows);
