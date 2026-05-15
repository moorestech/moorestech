# Reflection スニペット集

`uloop execute-dynamic-code` で頻用するリフレクションパターン。コピペして即使えるように個別断片化してある。

## 全 MainToolbarElement の section と index を一覧する

```csharp
var asm = typeof(UnityEditor.Overlays.Overlay).Assembly;
var sectionType = asm.GetType("UnityEditor.Overlays.OverlayContainerSection");
var ovType = typeof(UnityEditor.Overlays.Overlay);
var tMt = asm.GetType("UnityEditor.Toolbars.MainToolbar");
var getDefs = tMt.GetMethod("GetAllElementDefinitions", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
var defs = (System.Collections.IEnumerable)getDefs.Invoke(null, null);
var tryGet = tMt.GetMethod("TryGetOverlay", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
var pCont = ovType.GetProperty("container", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

foreach (var d in defs)
{
    var path = d.GetType().GetProperty("path")?.GetValue(d)?.ToString();
    if (path == null) continue;
    var args = new object[] { path, null };
    if (!(bool)tryGet.Invoke(null, args)) continue;
    var cont = pCont.GetValue(args[1]);
    if (cont == null) continue;
    var getIdx = cont.GetType().GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
        .First(m => m.Name == "GetOverlayIndex" && m.GetParameters().Length == 3 && m.GetParameters()[1].ParameterType.GetElementType() == sectionType);
    var ga = new object[] { args[1], null, 0 };
    getIdx.Invoke(cont, ga);
    UnityEngine.Debug.Log($"[{ga[1]}/{ga[2]}] {path}");
}
```

## 特定 element を任意 section / index にドッキング

```csharp
var asm = typeof(UnityEditor.Overlays.Overlay).Assembly;
var sectionType = asm.GetType("UnityEditor.Overlays.OverlayContainerSection");
var hintType = asm.GetType("UnityEditor.Overlays.DockingHint");
var ovType = typeof(UnityEditor.Overlays.Overlay);
var tMt = asm.GetType("UnityEditor.Toolbars.MainToolbar");
var tryGet = tMt.GetMethod("TryGetOverlay", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);

var target = new object[] { "moorestech/Xxx", null };
var anchor = new object[] { "moorestech/Scene Reload", null };
tryGet.Invoke(null, target);
tryGet.Invoke(null, anchor);

var pCont = ovType.GetProperty("container", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
var cont = pCont.GetValue(anchor[1]);
var dockAt = ovType.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
    .First(m => m.Name == "DockAt" && m.GetParameters().Length == 4);

var section = System.Enum.Parse(sectionType, "Middle");    // BeforeSpacer / Middle / AfterSpacer
var hint = System.Enum.Parse(hintType, "DockedBefore");    // None / DockedBefore / DockedAfter
var result = dockAt.Invoke(target[1], new[] { cont, section, (object)0, hint });
UnityEngine.Debug.Log("DockAt result=" + result);
```

## 強制再描画

```csharp
UnityEditor.Toolbars.MainToolbar.Refresh("moorestech/Xxx");
```

ボタンが再生成され、テキストやアイコンの変更が反映される。

## ShowAll / HideAll / SetDisplayedAll

```csharp
var m = typeof(UnityEditor.Toolbars.MainToolbar).GetMethod("ShowAll", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic, null, new[] { typeof(string) }, null);
m.Invoke(null, new object[] { "moorestech/Xxx" });
```

`HideAll` も同じシグネチャ。`SetDisplayedAll(string, bool)` は 2 引数。

## OverlayContainerSection の値

```
BeforeSpacer = 0  // 左端側
AfterSpacer  = 1  // 右端側
Middle       = 2  // プレイボタン直左
```

## DockingHint の値

```
None         = 0
DockedBefore = 1
DockedAfter  = 2
```
