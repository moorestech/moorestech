// 動いている歯車の部品の角度を1秒あけて2回読み、PlayerLoop の Tick で実際に回っているかと、RPM から見込む角度との比を返す
using System.Linq;
using System.Reflection;
using Client.Game.InGame.BlockSystem.StateProcessor;
using Game.Gear.Common;
var stateField = typeof(GearStateChangeProcessor).GetField("_currentGearState", BindingFlags.NonPublic | BindingFlags.Instance);
var samples = Object.FindObjectsByType<GearStateChangeProcessor>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
    .Where(p => p.isActiveAndEnabled && stateField.GetValue(p) is GearStateDetail s && s.CurrentRpm > 0)
    .SelectMany(p => p.RotationInfos.OfType<TransformRotationInfo>().Where(r => r.RotationTransform != null).Select(r => (p, r)))
    .Take(30).ToList();
var before = samples.Select(x => x.r.RotationTransform.localRotation).ToList();
var t0 = Time.realtimeSinceStartup; var f0 = Time.frameCount;
await System.Threading.Tasks.Task.Delay(1000);
var seconds = Time.realtimeSinceStartup - t0; var frames = Time.frameCount - f0;
var moved = 0;
for (var i = 0; i < samples.Count; i++) if (Quaternion.Angle(before[i], samples[i].r.RotationTransform.localRotation) > 0.01f) moved++;
var rpms = samples.Select(x => ((GearStateDetail)stateField.GetValue(x.p)).CurrentRpm).ToList();
return "sampled=" + samples.Count + " moved=" + moved + " frames=" + frames + " seconds=" + seconds.ToString("F2") + " rpm(min/max)=" + rpms.DefaultIfEmpty().Min() + "/" + rpms.DefaultIfEmpty().Max();
