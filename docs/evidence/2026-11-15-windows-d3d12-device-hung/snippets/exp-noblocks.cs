using System.Linq;
using Client.Game.InGame.Context;
using UnityEngine;
var n = 0;
foreach (var b in ClientDIContext.BlockGameObjectDataStore.BlockGameObjectDictionary.Values)
foreach (var r in b.GetComponentsInChildren<Renderer>(true)) { r.enabled = false; n++; }
return "block renderers off=" + n;
