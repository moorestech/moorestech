// 定常判定用の規模指標（Renderer数・GameObject数）をJSONで返す
using UnityEngine;
var r = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None).Length;
var g = Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None).Length;
return "{\"renderers\":" + r + ",\"gameObjects\":" + g + ",\"frameCount\":" + Time.frameCount + "}";
