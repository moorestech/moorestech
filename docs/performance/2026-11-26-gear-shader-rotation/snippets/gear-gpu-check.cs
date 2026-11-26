// ゲームの中の歯車が、シェーダー回転へ移っているかを数える（部品・まとめたRenderer・表の行・回転版マテリアル）
using System.Linq;
using System.Reflection;
using Client.Game.InGame.Block.GearRotation.Gpu;
using Client.Game.InGame.BlockSystem.StateProcessor;
var all = Object.FindObjectsByType<GearStateChangeProcessor>(FindObjectsInactive.Include, FindObjectsSortMode.None);
var infos = all.SelectMany(p => p.RotationInfos).OfType<TransformRotationInfo>().ToList();
var gpu = infos.Count(i => i.IsGpuDriven);
var hosts = Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(r => r.name.EndsWith(" (GearRotation)")).ToList();
var rotatingMat = hosts.Count(r => r.sharedMaterials.All(m => m != null && m.shader.name == "Moorestech/GearRotatingLit"));
var table = typeof(GearRotationGpuTable).GetField("Table", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
var slots = (System.Collections.ICollection)table.GetType().GetField("_slotByKey", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(table);
var probe = table.GetType().GetProperty("ProbeLimit").GetValue(table);
var rows = (System.Array)table.GetType().GetProperty("Rows").GetValue(table);
var listCount = ((System.Collections.ICollection)typeof(Client.Game.InGame.Block.GearRotation.GearRotationUpdateList).GetField("Processors", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null)).Count;
return "processors=" + all.Length + " transformInfos=" + infos.Count + " gpuDriven=" + gpu + " hosts=" + hosts.Count + " hostsWithRotatingMat=" + rotatingMat
    + " tableRows=" + slots.Count + " capacity=" + rows.Length + " probeLimit=" + probe + " cpuUpdateList=" + listCount
    + " gearTime=" + Shader.GetGlobalFloat("_GearRotationTime").ToString("F2") + " time=" + Time.time.ToString("F2");
