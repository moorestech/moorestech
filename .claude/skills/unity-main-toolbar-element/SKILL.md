---
name: unity-main-toolbar-element
description: Unity 6 Editor のメインツールバーに新しいボタン・テキスト・スライダー要素を追加するためのスキル。新規 [MainToolbarElement] が表示されない / 配置位置が思った場所にならない / Home/Reload 隣に置きたい等の問題を、Unity 内部 API のリフレクションと既存 ToolbarOverlayPositioner との統合で解決する。Use When — 「ツールバーに{ボタン/テキスト}を追加して」「MainToolbarElement を作って」「toolbar 要素が表示されない」「Home/Reload の横に何か置きたい」「ツールバー要素の位置がおかしい」と言われた場合。
---

# unity-main-toolbar-element

Unity 6 Editor の `MainToolbarElement` API を使って新しいツールバー要素を作成・自動表示・位置決めする全工程。

公開 API だけでは「新規要素が初期状態で非表示」「位置が思った場所にならない」問題が必ず発生する。本スキルは既存実装（`moorestech_client/Assets/Scripts/Editor/Toolbar/`）と整合させながら、内部 API をリフレクションで叩いて解決する手順を定義する。

## 前提

- Unity 6000.3+ を想定。Unity 6 以前は `MainToolbarElement` 自体が無い
- 配置先は `moorestech_client/Assets/Scripts/Editor/Toolbar/` 配下（Editor アセンブリ内なので `#if UNITY_EDITOR` 不要）
- ビルド検証は `uloop compile --project-path ./moorestech_client`
- 見た目検証は `uloop screenshot --project-path ./moorestech_client --window-name Toolbar --match-mode contains`
- ランタイム調査は `uloop execute-dynamic-code --project-path ./moorestech_client --code '...'`

## 手順

### Step 1. クラス雛形を作る

```csharp
using UnityEditor;
using UnityEditor.Toolbars;
using UnityEngine;

namespace Client.Editor.Toolbar
{
    public static class XxxToolbarElement
    {
        private const string ElementPath = "moorestech/Xxx";

        [MainToolbarElement(ElementPath, defaultDockPosition = MainToolbarDockPosition.Left)]
        public static MainToolbarElement CreateElement()
        {
            var content = new MainToolbarContent("テキスト or アイコン名", (Texture2D)null, "tooltip");
            return new MainToolbarButton(content, () => { /* clickハンドラ */ });
        }
    }
}
```

ポイント:
- `ElementPath` は必ず `moorestech/` プレフィックスを付ける。`ToolbarOverlayPositioner.AutoShowOverlaysIfNeeded` がこのプレフィックスで自動表示判定する
- `defaultDockIndex` を指定しても**実際の位置決めには効かない**（後述）。省略してよい
- `MainToolbarContent(string text, Texture2D icon, string tooltip)` 3 引数版が表示テキスト付き
- アイコンのみなら `MainToolbarContent(icon, tooltip)` 2 引数版
- `(Texture2D)null` を 2 引数版で渡すとボタンが描画されない罠あり。テキスト付きにするなら必ず 3 引数版

### Step 2. ToolbarOverlayPositioner にバージョン bump と配置ロジックを追加

新規要素は EditorPrefs に保存された toolbar 状態に無いと**初期状態で非表示**になる。`ToolbarUtility.cs` の `ToolbarOverlayPositioner` がこの問題を吸収しているが、新規追加時は必ずバージョン bump が必要。

`moorestech_client/Assets/Scripts/Editor/Toolbar/ToolbarUtility.cs` を編集:

1. `CurrentOverlayInitVersion` を +1 する。これにより全ユーザーの EditorPrefs キャッシュが invalidate され、`AutoShowOverlaysIfNeeded` が再走して `moorestech/` プレフィックスの全 overlay が再表示される
2. `TryPosition` 内に新規要素を加える。位置決めには 2 種類のパターンがある:

**パターン A: 既存要素の前後に挟む（同セクション内の並び替え）**

```csharp
Overlay xxx = null;
foreach (var o in overlayList)
{
    var id = o.GetType().GetProperty("id", ...)?.GetValue(o)?.ToString() ?? "";
    if (id.Contains("moorestech/Xxx")) xxx = o;
}
if (xxx != null) dockBefore?.Invoke(xxx, new object[] { home });  // home の前に挟む
```

**パターン B: セクション跨ぎ（BeforeSpacer → Middle に強制移動）**

`DockBefore` だけではセクションを跨げない。`DockAt(container, OverlayContainerSection.Middle, index, DockingHint.DockedBefore)` を使う:

```csharp
if (xxx != null) DockIntoMiddleSection(xxx, sceneReload);  // 既存ヘルパ
```

### Step 3. コンパイル → ドメインリロード → スクリーンショット検証

```bash
uloop compile --project-path ./moorestech_client
# Success: true, ErrorCount: 0 を確認

# ドメインリロード後の見た目検証
uloop screenshot --project-path ./moorestech_client --window-name Toolbar --match-mode contains
# 返ってきた ImagePath を Read で開いて視認
```

期待位置に出ていなければ Step 4 へ。

### Step 4. 配置がおかしい時のリフレクション探索

公開 API では Overlay の section / index がわからない。`uloop execute-dynamic-code` でランタイム調査:

```bash
# 既存 element (例: Scene Reload) の section と index を取得
uloop execute-dynamic-code --project-path ./moorestech_client --code '
var asm = typeof(UnityEditor.Overlays.Overlay).Assembly;
var sectionType = asm.GetType("UnityEditor.Overlays.OverlayContainerSection");
var ovType = typeof(UnityEditor.Overlays.Overlay);
var tMt = asm.GetType("UnityEditor.Toolbars.MainToolbar");
var tryGet = tMt.GetMethod("TryGetOverlay", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
var args = new object[] { "moorestech/Scene Reload", null };
tryGet.Invoke(null, args);
var pCont = ovType.GetProperty("container", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
var cont = pCont.GetValue(args[1]);
var getIdx = cont.GetType().GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
    .First(m => m.Name == "GetOverlayIndex" && m.GetParameters().Length == 3 && m.GetParameters()[1].ParameterType.GetElementType() == sectionType);
var getArgs = new object[] { args[1], null, 0 };
getIdx.Invoke(cont, getArgs);
UnityEngine.Debug.Log("section=" + getArgs[1] + " index=" + getArgs[2]);
'
sleep 2
uloop get-logs --project-path ./moorestech_client --log-type Log | grep "section="
```

Section の値は 3 種類のみ:
- `BeforeSpacer`: 左端付近のロゴ・Sign in・Asset Store 等
- `Middle`: プレイボタン直左の Home/Reload/TimeScale 群（再生ボタンに張り付く）
- `AfterSpacer`: 右端付近の Cloud/Layout/Help 等

新規 element は `defaultDockPosition=Left` で登録すると `BeforeSpacer` に入る。`Middle` に入れたい場合は明示的に `DockAt` で移動が必要。

### Step 5. テキスト着色が必要な場合は Overlay.rootVisualElement に限定する

ボタンの表示テキストに色付けする場合、`EditorWindow.rootVisualElement` 全体を再帰探索すると同名 TextElement を誤着色する。必ず該当 Overlay の `rootVisualElement` 配下に限定:

```csharp
var tryGetOverlay = typeof(MainToolbar).GetMethod("TryGetOverlay", BindingFlags.Static | BindingFlags.NonPublic);
var args = new object[] { ElementPath, null };
tryGetOverlay.Invoke(null, args);
if (args[1] is not Overlay overlay) return;
var rootProp = typeof(Overlay).GetProperty("rootVisualElement", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
var root = rootProp?.GetValue(overlay) as VisualElement;
// root 配下を ColorizeRecursive
```

## Gotchas

### `[MainToolbarElement]` だけでは表示されない

新規登録の要素は EditorPrefs の保存レイアウトに無いため、デフォルトで displayed=false。必ず `ToolbarOverlayPositioner.CurrentOverlayInitVersion` を bump して `AutoShowOverlaysIfNeeded` を再走させる。bump し忘れると「コンパイルは通るのに表示されない」状態になり、原因不明で長時間溶ける。

### `defaultDockIndex = int.MaxValue` は位置決めに効かない

既存の `HomeButtonToolbarElement` 等が `defaultDockIndex = int.MaxValue` を指定しているが、これは初回登録時のヒントに過ぎず、EditorPrefs に保存された後は無視される。実際の位置は `DockBefore`/`DockAfter`/`DockAt` で明示的に決める。MaxValue 指定は省略してよい（誤解を招くだけ）。

### `DockBefore` はセクションを跨げない

新規 element は `BeforeSpacer` (左端側) に入る。`Middle` セクションの Reload/Home の隣に置きたい場合、`DockBefore(branch, sceneReload)` を呼んでも視覚的に動かない。`DockAt(container, OverlayContainerSection.Middle, index, DockingHint)` で明示的にセクション指定が必要。**これに気付かないと「DockBefore が効かない」と数時間溶ける**。

### `OverlayContainerSection` / `DockingHint` enum は internal

`UnityEditor.Overlays` 名前空間内で internal なため `using UnityEditor.Overlays;` しても直接列挙できない。`Assembly.GetType("UnityEditor.Overlays.OverlayContainerSection")` + `System.Enum.Parse(t, "Middle")` でリフレクション経由。

### `MainToolbar.ShowAll(path)` / `TryGetOverlay` は非公開

`UnityEditor.Toolbars.MainToolbar` のメソッドは `Refresh(string)` のみ public。`ShowAll(string)` / `HideAll(string)` / `SetDisplayedAll(string, bool)` / `TryGetOverlay(string, out Overlay)` は `BindingFlags.Static | BindingFlags.NonPublic` で取得。

### `MainToolbarContent((Texture2D)null, "tooltip")` だとボタンが描画されない

2 引数版のコンストラクタに null を渡すとアイコン無しでボタン領域も無くなる。テキストを表示したい場合は必ず `new MainToolbarContent(text, (Texture2D)null, tooltip)` の 3 引数版を使う。

### `EditorApplication.update` の二重購読

`[InitializeOnLoadMethod]` で `EditorApplication.update += OnUpdate;` する前に必ず `-= OnUpdate;` する。ドメインリロード時に重複登録されて毎フレーム 2 回呼ばれる。

### ドメインリロード中の uloop コマンドはタイムアウトする

`uloop compile` 直後は Unity が Domain Reload 中で uloop コマンドが「Unity is reloading」エラーを返す。45 秒程度待ってから次のコマンド。`run_in_background: true` か別ターミナルで sleep するか、`Monitor` ツールで待つ。

### ToolbarOverlayPositioner の `_positioned` フラグはドメインリロード毎にリセットされる

`static bool _positioned` は static フィールドなのでドメインリロードで `false` に戻る。よって毎回起動時に `TryPosition` が走り直す。これに依存しているので問題ないが、「一度配置したら永続する」と誤解しないこと。

### 着色は `EditorWindow` 全体を走査しない

ブランチ名 / 任意テキストに色付けする場合、必ず該当 Overlay の `rootVisualElement` 配下に限定する。`Resources.FindObjectsOfTypeAll<EditorWindow>()` から `MainToolbarWindow` を見つけて全 visual tree を歩くと、ブランチ名と同じ文字列を持つ別の TextElement（例: ブランチ名が "main" だと別の UI 要素）を誤着色する。

### リフレクション結果は必ず null チェック

`AGENTS.md` で nullcheck は外部データ・非同期ロード結果のみ許容と書かれているが、**内部 API のリフレクション結果は「外部データ扱い」で nullcheck OK**。Unity アップデートで内部 API のシグネチャが変わると `GetMethod`/`GetType` が null を返す。

## 関連ファイル

- `moorestech_client/Assets/Scripts/Editor/Toolbar/ToolbarUtility.cs`: `ToolbarOverlayPositioner` (バージョン管理 + AutoShow + DockBefore/After + DockIntoMiddleSection ヘルパ)
- `moorestech_client/Assets/Scripts/Editor/Toolbar/HomeButtonToolbarElement.cs`: アイコンボタンの典型例
- `moorestech_client/Assets/Scripts/Editor/Toolbar/SceneReloadToolbarElement.cs`: PlayMode 連動ボタンの例
- `moorestech_client/Assets/Scripts/Editor/Toolbar/TimeScaleControlToolbarElement.cs`: スライダー注入の例
- `moorestech_client/Assets/Scripts/Editor/Toolbar/BranchNameToolbarElement.cs`: テキスト + 着色 + セクション跨ぎ配置の例（本スキルのリファレンス実装）

## 詳細リファレンス

[references/reflection-snippets.md](references/reflection-snippets.md) — 探索フェーズで頻用するリフレクションスニペット集（必要時に Read）
