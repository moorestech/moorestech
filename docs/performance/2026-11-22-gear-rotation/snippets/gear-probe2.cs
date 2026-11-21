// 変更後の歯車の Tick（GearRotationUpdateList.AdvanceAll）の時間を部分ごとに Stopwatch で測る（各50回の平均）
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Client.Game.InGame.Block.GearRotation;
using Client.Game.InGame.BlockSystem.StateProcessor;
using Game.Gear.Common;
var list = (List<GearStateChangeProcessor>)typeof(GearRotationUpdateList).GetField("Processors", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
var stateField = typeof(GearStateChangeProcessor).GetField("_currentGearState", BindingFlags.NonPublic | BindingFlags.Instance);
var states = list.Select(p => (GearStateDetail)stateField.GetValue(p)).ToList();
var moving = list.Where((p, i) => states[i] != null && states[i].CurrentRpm != 0).ToList();
var movingParts = moving.SelectMany(p => p.RotationInfos).Count(r => r != null && r.RotationTransform != null);
var sw = new System.Diagnostics.Stopwatch(); const int N = 50; var dt = 1f / 60f;
sw.Restart(); for (var i = 0; i < N; i++) GearRotationUpdateList.AdvanceAll(dt); var allMs = sw.Elapsed.TotalMilliseconds / N;
sw.Restart(); for (var i = 0; i < N; i++) foreach (var p in list) p.AdvanceRotation(0f); var zeroDtMs = sw.Elapsed.TotalMilliseconds / N;
sw.Restart(); for (var i = 0; i < N; i++) foreach (var p in moving) p.AdvanceRotation(dt); var movingMs = sw.Elapsed.TotalMilliseconds / N;
var parts = moving.SelectMany(p => p.RotationInfos).Where(r => r != null && r.RotationTransform != null).Select(r => r.RotationTransform).ToList();
sw.Restart(); for (var i = 0; i < N; i++) foreach (var t in parts) t.Rotate(Vector3.forward * 0.01f); var rotateOnlyMs = sw.Elapsed.TotalMilliseconds / N;
return "listed=" + list.Count + " moving=" + moving.Count + " movingParts=" + movingParts + " | AdvanceAll=" + allMs.ToString("F3") + "ms dt0(全員の早期return)=" + zeroDtMs.ToString("F3") + "ms movingOnly=" + movingMs.ToString("F3") + "ms Transform.Rotateだけ=" + rotateOnlyMs.ToString("F3") + "ms";
