// フォーカスと、有効なスキンメッシュの数を返す
using UnityEngine;
var n = 0; foreach (var s in Object.FindObjectsByType<SkinnedMeshRenderer>(FindObjectsSortMode.None)) if (s.enabled && s.gameObject.activeInHierarchy) n++;
return "focused=" + Application.isFocused + " skinnedActive=" + n + " frame=" + Time.frameCount + " fps~" + (1f / Time.smoothDeltaTime).ToString("F1");
