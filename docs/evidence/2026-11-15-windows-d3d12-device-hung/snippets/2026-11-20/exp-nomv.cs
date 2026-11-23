// 全スキンメッシュのスキンのモーションベクターを切る（プレハブは保存しない）
using UnityEngine;
var n = 0; var changed = 0;
foreach (var s in Object.FindObjectsByType<SkinnedMeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
{ n++; if (s.skinnedMotionVectors) { s.skinnedMotionVectors = false; changed++; } }
return "skinned=" + n + " motionVectorsTurnedOff=" + changed;
