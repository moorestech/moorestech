using Client.Game.InGame.Context;
using UnityEngine;
var n = 0;
foreach (var b in ClientDIContext.BlockGameObjectDataStore.BlockGameObjectDictionary.Values)
foreach (var s in b.GetComponentsInChildren<SkinnedMeshRenderer>(true)) { s.enabled = true; n++; }
return "skinned on=" + n;
