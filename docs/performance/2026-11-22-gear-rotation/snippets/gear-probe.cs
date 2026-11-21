// 歯車の回転の内訳と、1フレーム分の処理を部分ごとに Stopwatch で測る（各200回の平均）
using System.Linq;
using System.Reflection;
using Client.Game.InGame.BlockSystem.StateProcessor;
using Game.Gear.Common;
var all = Object.FindObjectsByType<GearStateChangeProcessor>(FindObjectsInactive.Include, FindObjectsSortMode.None);
var active = all.Where(p => p.isActiveAndEnabled).ToList();
var stateField = typeof(GearStateChangeProcessor).GetField("_currentGearState", BindingFlags.NonPublic | BindingFlags.Instance);
var withState = active.Where(p => stateField.GetValue(p) != null).ToList();
var infos = withState.SelectMany(p => p.RotationInfos).Where(r => r != null).ToList();
var tInfos = infos.OfType<TransformRotationInfo>().ToList();
var aInfos = infos.OfType<AnimatorRotationInfo>().ToList();
var zeroRpm = withState.Count(p => ((GearStateDetail)stateField.GetValue(p)).CurrentRpm == 0);
var transforms = tInfos.Select(r => r.RotationTransform).Where(t => t != null).ToList();
var roots = transforms.Select(t => t.root).Distinct().Count();
var sw = new System.Diagnostics.Stopwatch();
const int N = 200;
// 1) 今の Rotate 全体（deltaTime=0 なので見た目は変わらない）
sw.Restart();
for (var i = 0; i < N; i++) foreach (var p in withState) p.Rotate((GearStateDetail)stateField.GetValue(p), 0f);
var fullMs = sw.Elapsed.TotalMilliseconds / N;
// 2) Transform.Rotate だけ（角度0）
sw.Restart();
for (var i = 0; i < N; i++) foreach (var t in transforms) t.Rotate(Vector3.zero);
var rotateMs = sw.Elapsed.TotalMilliseconds / N;
// 3) transform.rotation の取得だけ（ワールド符号の計算に使っている）
sw.Restart();
var acc = 0f;
for (var i = 0; i < N; i++) foreach (var t in transforms) acc += t.rotation.w;
var getRotMs = sw.Elapsed.TotalMilliseconds / N;
// 4) localRotation を書くだけ
sw.Restart();
for (var i = 0; i < N; i++) foreach (var t in transforms) t.localRotation = t.localRotation;
var setLocalMs = sw.Elapsed.TotalMilliseconds / N;
return "processors all=" + all.Length + " active=" + active.Count + " withState=" + withState.Count + " zeroRpm=" + zeroRpm
    + " | infos=" + infos.Count + " transform=" + tInfos.Count + " animator=" + aInfos.Count + " distinctRoots=" + roots
    + " | perFrame full=" + fullMs.ToString("F3") + "ms transform.Rotate=" + rotateMs.ToString("F3") + "ms getRotation=" + getRotMs.ToString("F3") + "ms setLocalRotation=" + setLocalMs.ToString("F3") + "ms";
