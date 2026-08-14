# Generated World Editor Play Button Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: subagent-driven-development スキルを使い、このplanをタスクごとに実装すること。ステップはチェックボックス（`- [ ]`）記法で進捗管理する。

**Goal:** エディタツールバーの専用ボタン一つで、自動生成マップ（`--mapMode generated`）の永続ワールドを起動・継続プレイできるようにする。

**Architecture:** 既存の `NoSaveLoadPlayToolbarElement`（ツールバーボタン→SessionStateフラグ→`GameInitializer` シーンからPlayMode開始）＋ `SkipSaveLoadPlayModeSettings.ApplyIfNeeded`（`InitializeScenePipeline` 内でサーバー起動引数を書き換え）の完全同型ペアを1組追加する。書き換える引数は `WorldDirectory = <saves>/world_generated` と `MapMode = generated` の2項目のみで、以降のサーバー起動・マップ生成（`WorldProvisioner`）・セーブロードは通常プレイと完全同一経路。加えて確認ダイアログ付きの「Delete Generated World」メニュー項目で新seed作り直しを可能にする。

**Tech Stack:** Unity Editor拡張（MainToolbarElement / MenuItem / SessionState）、NUnit（Client.Tests）、uloop CLI

**設計ADR:** `docs/adr/0009-generated-world-editor-play-button.md`（必読・本planの上位裁定）

## Requirements

- R1: エディタツールバーに「Generated Play」ボタンを追加する。押すと `GameInitialaizer` シーンからPlayModeへ入り、`world_generated` ディレクトリ・`generated` マップモードでローカルサーバーが起動する（受け入れ基準: 初回押下でマップが自動生成されプレイ可能になる）
- R2: 起動引数の書き換えは `WorldDirectory` と `MapMode` の2項目のみ。`AutoSave` はデフォルト（有効）のまま（受け入れ基準: ユニットテストで2項目の書き換えとAutoSave=trueを検証）
- R3: ワールドは初回のみ生成され永続化される。2回目以降のボタン押下では同じワールドの続きをロードする（受け入れ基準: `WorldProvisioner` の「world.jsonがあればno-op」挙動をそのまま通す。追加コードを書かない）
- R4: 再生終了時にSessionStateフラグと `playModeStartScene` を復元し、通常の再生ボタン・NoSave Playボタンに影響を残さない（受け入れ基準: `EnteredEditMode` でフラグfalse・startScene=null）
- R5: 「moorestech/Delete Generated World」メニュー項目を追加する。確認ダイアログ付きで `world_generated` を削除し、次回起動時に新seedで再生成される。再生中は削除を拒否し、ワールドが無い場合は通知のみ（受け入れ基準: 再生中ガード・不在通知・確認後削除の3分岐）
- R6: フラグ無効時（通常再生・NoSave Play）は起動引数に一切影響しない（受け入れ基準: ユニットテストでフラグ無効時の無変更を検証）
- やらないこと: メインメニューUI／seed指定UI／サーバー・プロトコル側の変更／ビルドへの影響（全コードはエディタ専用: Editorフォルダ配下または `#if UNITY_EDITOR`）

## Global Constraints

- 作業ブランチ: `feature/generated-world-play-button`（メインworktree＝リポジトリ本体ディレクトリ上。エディタ実機検証に有料アセット・Libraryが必要なため別worktreeを作らない）
- .csファイルを変更したら必ず `uloop compile --project-path ./moorestech_client` を実行する
- .metaファイルは手動作成禁止。新規.csファイル作成後は `uloop compile` でUnityに.metaを生成させ、コミットに含める
- 1ファイル200行以下・partial禁止・`Func<>` 禁止・try-catch原則禁止
- コメントは日本語・英語の2行セット（各1行厳守）。自明なコメントは書かない
- エディタ専用コード: `Client.Starter/Editor/` 配下は `#if UNITY_EDITOR` でファイル全体を囲む（前例 `SkipSaveLoadPlayModeSettings.cs`）。`Scripts/Editor/` 配下はEditor専用コンパイルのため不要（前例 `NoSaveLoadPlayToolbarElement.cs`）
- テスト実行: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "<正規表現>"`。「Unity is reloading (Domain Reload in progress)」エラー時は45秒待ってリトライ
- テスト実行前にDebugParameters残置に注意（cache/のFreeBlockPlacement等が残っているとテストが無言死する）

## 配置と前例

| # | 項目 | 配置先 | 機構 | 前例（役割同型） |
|---|---|---|---|---|
| 1 | `GeneratedWorldPlayModeSettings`（新規） | `Client.Starter/Editor/`（Client.Starter asmdef・`#if UNITY_EDITOR`） | SessionStateフラグ→`CliConvert` で引数書き換え | `Client.Starter/Editor/SkipSaveLoadPlayModeSettings.cs`（同役割・同機構） |
| 2 | `InitializeScenePipeline` へ `ApplyIfNeeded` 呼び出し追加（変更） | 既存の `#if UNITY_EDITOR` ブロック内 | 明示呼び出し | 同ブロックの `SkipSaveLoadPlayModeSettings.ApplyIfNeeded` 呼び出し |
| 3 | `GeneratedWorldPlayToolbarElement`（新規） | `Scripts/Editor/Toolbar/`（Editor専用コンパイル） | `MainToolbarElement` ボタン | `Editor/Toolbar/NoSaveLoadPlayToolbarElement.cs`（同役割・同機構） |
| 4 | `GeneratedWorldDeleteMenu`（新規） | `Client.Starter/Editor/`（`#if UNITY_EDITOR`） | `MenuItem`＋`EditorUtility.DisplayDialog` | メニュールート `moorestech/` は既存多数（`moorestech/Bake Block Click Colliders` 等）。パス定数の所有者 `GeneratedWorldPlayModeSettings` と同居させ単一ソース化 |
| 5 | `GeneratedWorldPlayModeSettingsTest`（新規） | `Client.Tests/Starter/` | NUnit＋SessionState | `Client.Tests/StandaloneQa/StandaloneTerrainQaSettingsTest.cs`（引数書き換えの検証形式）・`Client.Tests/Playtest/PlaytestBootLifecycleTest.cs`（SessionState操作） |

- ドメイン層・共有層への追加はゼロ。サーバー側（`StartServerSettings` デフォルト・`WorldProvisioner`）は一切変更しない
- 世界ディレクトリパスの単一ソースは `GeneratedWorldPlayModeSettings.WorldDirectoryPath`（ボタン経由の起動と削除メニューが共有）
- 機能パリティ: 通常再生ボタン／NoSave Playボタン／プレイテストDSL／StandaloneQa はいずれも別SessionStateキー・別引数経路のため無影響（フラグは自ボタン押下時のみtrue、`EnteredEditMode` で復元）

---

### Task 1: GeneratedWorldPlayModeSettings（引数書き換え）とテスト

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Starter/Editor/GeneratedWorldPlayModeSettings.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/InitializeScenePipeline.cs`（`SkipSaveLoadPlayModeSettings.ApplyIfNeeded` 呼び出しの直後）
- Test: `moorestech_client/Assets/Scripts/Client.Tests/Starter/GeneratedWorldPlayModeSettingsTest.cs`
- Modify(必要時のみ): `moorestech_client/Assets/Scripts/Client.Tests/Client.Tests.asmdef`

**Interfaces:**
- Consumes: `SessionState`（UnityEditor）、`CliConvert.Parse/Serialize`（Server.Boot.Args）、`StartServerSettings`（Server.Boot）、`WorldProvisioner.GeneratedMapMode`（Game.MapGeneration.Provisioning）、`GameSystemPaths.GetSaveFilePath`（Game.Paths）、`InitializeProprieties`（Client.Starter）
- Produces: `GeneratedWorldPlayModeSettings.SessionStateKey`（const string）、`GeneratedWorldPlayModeSettings.WorldDirectoryPath`（static string プロパティ）、`GeneratedWorldPlayModeSettings.ApplyIfNeeded(InitializeProprieties proprieties)`（static void）— Task 2・Task 3 がこれらを参照する

- [ ] **Step 1: ブランチを作成する**

```bash
pwd  # リポジトリ本体ディレクトリ（メインworktree）であることを確認
git checkout -b feature/generated-world-play-button
```

- [ ] **Step 2: Client.Tests.asmdef の参照を確認する**

Run: `grep -n "Game.MapGeneration" moorestech_client/Assets/Scripts/Client.Tests/Client.Tests.asmdef`

参照が無ければ `references` 配列に `"Game.MapGeneration"` を1行追加する（asmdefはJSONなのでEditツールで編集可。Unityファイル直編集禁止の対象外）。テストが `WorldProvisioner.GeneratedMapMode` 定数を参照するために必要。

- [ ] **Step 3: 失敗するテストを書く**

`moorestech_client/Assets/Scripts/Client.Tests/Starter/GeneratedWorldPlayModeSettingsTest.cs` を新規作成:

```csharp
using Client.Starter;
using Client.Starter.Editor;
using Game.MapGeneration.Provisioning;
using NUnit.Framework;
using Server.Boot;
using Server.Boot.Args;
using UnityEditor;

namespace Client.Tests.Starter
{
    public class GeneratedWorldPlayModeSettingsTest
    {
        [TearDown]
        public void TearDown()
        {
            // フラグ残置は後続テストの起動引数を汚染するため必ず戻す
            // A leftover flag pollutes launch args of later tests, so always reset it
            SessionState.SetBool(GeneratedWorldPlayModeSettings.SessionStateKey, false);
        }

        [Test]
        public void フラグ有効時はworld_generatedとgeneratedモードへ書き換える()
        {
            SessionState.SetBool(GeneratedWorldPlayModeSettings.SessionStateKey, true);
            var proprieties = InitializeProprieties.CreateDefault();

            GeneratedWorldPlayModeSettings.ApplyIfNeeded(proprieties);

            var settings = CliConvert.Parse<StartServerSettings>(proprieties.CreateLocalServerArgs);
            Assert.That(settings.WorldDirectory, Is.EqualTo(GeneratedWorldPlayModeSettings.WorldDirectoryPath));
            Assert.That(settings.MapMode, Is.EqualTo(WorldProvisioner.GeneratedMapMode));
            Assert.That(settings.AutoSave, Is.True);
        }

        [Test]
        public void フラグ無効時は起動引数を変更しない()
        {
            SessionState.SetBool(GeneratedWorldPlayModeSettings.SessionStateKey, false);
            var proprieties = InitializeProprieties.CreateDefault();

            GeneratedWorldPlayModeSettings.ApplyIfNeeded(proprieties);

            var settings = CliConvert.Parse<StartServerSettings>(proprieties.CreateLocalServerArgs);
            Assert.That(settings.MapMode, Is.EqualTo(WorldProvisioner.TemplateMapMode));
            Assert.That(settings.WorldDirectory, Does.Not.Contain("world_generated"));
        }
    }
}
```

- [ ] **Step 4: コンパイルして失敗を確認する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: `GeneratedWorldPlayModeSettings` が存在しないためコンパイルエラー（CS0103/CS0246）

- [ ] **Step 5: GeneratedWorldPlayModeSettings を実装する**

`moorestech_client/Assets/Scripts/Client.Starter/Editor/GeneratedWorldPlayModeSettings.cs` を新規作成:

```csharp
#if UNITY_EDITOR
using Game.MapGeneration.Provisioning;
using Game.Paths;
using Server.Boot;
using Server.Boot.Args;
using UnityEditor;

namespace Client.Starter.Editor
{
    public static class GeneratedWorldPlayModeSettings
    {
        public const string SessionStateKey = "moorestech_GeneratedWorldPlayMode";
        private const string WorldDirectoryName = "world_generated";

        // 起動ボタンと削除メニューが共有する生成ワールドの保存先
        // Generated world save path shared by the play button and the delete menu
        public static string WorldDirectoryPath => GameSystemPaths.GetSaveFilePath(WorldDirectoryName);

        public static void ApplyIfNeeded(InitializeProprieties proprieties)
        {
            if (!SessionState.GetBool(SessionStateKey, false)) return;

            // 専用ワールドと自動生成モードだけを上書きする（セーブは通常どおり有効のまま）
            // Override only the dedicated world and generated map mode (saving stays enabled)
            var settings = CliConvert.Parse<StartServerSettings>(proprieties.CreateLocalServerArgs);
            settings.WorldDirectory = WorldDirectoryPath;
            settings.MapMode = WorldProvisioner.GeneratedMapMode;
            proprieties.CreateLocalServerArgs = CliConvert.Serialize(settings);
        }
    }
}
#endif
```

- [ ] **Step 6: InitializeScenePipeline へ呼び出しを追加する**

`moorestech_client/Assets/Scripts/Client.Starter/InitializeScenePipeline.cs` の既存 `#if UNITY_EDITOR` ブロック（`SkipSaveLoadPlayModeSettings.ApplyIfNeeded(_proprieties);` の行）の直後に1行追加:

```csharp
#if UNITY_EDITOR
            // ツールバーの専用再生ボタン経由なら、セーブデータをロード・保存しないよう起動引数を上書きする
            // When launched via the dedicated toolbar play button, override launch args to skip loading/saving save data
            Editor.SkipSaveLoadPlayModeSettings.ApplyIfNeeded(_proprieties);

            // 生成ワールド再生ボタン経由なら、専用ワールド・自動生成モードへ起動引数を上書きする
            // When launched via the generated-world play button, override launch args to the dedicated world and generated map mode
            Editor.GeneratedWorldPlayModeSettings.ApplyIfNeeded(_proprieties);
#endif
```

- [ ] **Step 7: コンパイルとテストを実行して通ることを確認する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0件

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "GeneratedWorldPlayModeSettingsTest"`
Expected: 2件PASS

- [ ] **Step 8: コミットする**

新規.csの.metaがUnityにより生成されていることを確認してから:

```bash
git add moorestech_client/Assets/Scripts/Client.Starter/Editor/GeneratedWorldPlayModeSettings.cs* \
        moorestech_client/Assets/Scripts/Client.Starter/InitializeScenePipeline.cs \
        moorestech_client/Assets/Scripts/Client.Tests/Starter/ \
        moorestech_client/Assets/Scripts/Client.Tests/Client.Tests.asmdef
git commit -m "feat: 生成ワールド起動用の引数書き換え設定を追加"
```

（`Client.Tests/Starter/` ディレクトリ自体が新規のため、ディレクトリの.metaも含める。asmdefは変更した場合のみadd）

---

### Task 2: Generated Play ツールバーボタン

**Files:**
- Create: `moorestech_client/Assets/Scripts/Editor/Toolbar/GeneratedWorldPlayToolbarElement.cs`

**Interfaces:**
- Consumes: `GeneratedWorldPlayModeSettings.SessionStateKey`（Task 1）、`ToolbarUtility.GetBuiltInIcon`（既存）、`MainToolbarElement` API（既存前例と同じ）
- Produces: なし（終端のエディタUI）

- [ ] **Step 1: ツールバー要素を実装する**

`moorestech_client/Assets/Scripts/Editor/Toolbar/GeneratedWorldPlayToolbarElement.cs` を新規作成（前例 `NoSaveLoadPlayToolbarElement.cs` の完全同型）:

```csharp
using Client.Starter.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Toolbars;
using UnityEngine;

namespace Client.Editor.Toolbar
{
    /// <summary>
    /// 自動生成ワールドでゲームを起動する専用の再生ボタンをツールバーに追加する
    /// Add a dedicated play button that launches the game with a generated world
    /// </summary>
    public static class GeneratedWorldPlayToolbarElement
    {
        private const string ElementPath = "moorestech/Generated Play";
        private const string GameInitializerScenePath = "Assets/Scenes/Game/GameInitialaizer.unity";

        [InitializeOnLoadMethod]
        private static void Initialize()
        {
            // 再生終了時の後始末を登録する
            // Register cleanup for when play mode ends
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        [MainToolbarElement(ElementPath, defaultDockPosition = MainToolbarDockPosition.Right, defaultDockIndex = 1)]
        public static MainToolbarElement CreateElement()
        {
            // 地形アイコン付きボタンを作成する
            // Create a button with a terrain icon
            var icon = ToolbarUtility.GetBuiltInIcon("d_Terrain Icon");
            var content = new MainToolbarContent(icon, "自動生成ワールドでゲームを起動する（初回は生成、以後は続きから）\nLaunch the game with a generated world (created once, then resumed)");
            return new MainToolbarButton(content, OnClicked);
        }

        private static void OnClicked()
        {
            // 既に再生中なら何もしない
            // Do nothing if already playing
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            // 生成ワールド起動フラグを立てる（ドメインリロードを越えて保持される）
            // Set the generated-world launch flag (persists across domain reload)
            SessionState.SetBool(GeneratedWorldPlayModeSettings.SessionStateKey, true);

            // ゲーム初期化シーンから再生を開始する
            // Start play mode from the game initializer scene
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(GameInitializerScenePath);
            EditorApplication.EnterPlaymode();
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            // 再生終了時にフラグと開始シーン設定を元へ戻す（通常の再生ボタンに影響させない）
            // Reset the flag and start-scene setting when play mode ends (so the normal play button is unaffected)
            if (state != PlayModeStateChange.EnteredEditMode) return;

            SessionState.SetBool(GeneratedWorldPlayModeSettings.SessionStateKey, false);
            EditorSceneManager.playModeStartScene = null;
        }
    }
}
```

- [ ] **Step 2: コンパイルして通ることを確認する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0件。エラーが出た場合の想定原因: アイコン名 `d_Terrain Icon` はコンパイルに影響しない（実行時null許容）が、`MainToolbarElement` API名の相違はエラーになるため前例ファイルと突き合わせる

- [ ] **Step 3: アイコンが取得できることを確認する**

Run: `uloop execute-dynamic-code --project-path ./moorestech_client --code "var icon = UnityEditor.EditorGUIUtility.IconContent(\"d_Terrain Icon\"); UnityEngine.Debug.Log(icon != null && icon.image != null ? \"icon OK\" : \"icon MISSING\");"`
Expected: `icon OK`。`icon MISSING` の場合は `d_TerrainInspector.TerrainToolSettings` へ差し替えて再確認

- [ ] **Step 4: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Editor/Toolbar/GeneratedWorldPlayToolbarElement.cs*
git commit -m "feat: 自動生成ワールド起動ボタンをツールバーへ追加"
```

---

### Task 3: Delete Generated World メニュー項目

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Starter/Editor/GeneratedWorldDeleteMenu.cs`

**Interfaces:**
- Consumes: `GeneratedWorldPlayModeSettings.WorldDirectoryPath`（Task 1）
- Produces: なし（終端のエディタUI）

- [ ] **Step 1: 削除メニューを実装する**

`moorestech_client/Assets/Scripts/Client.Starter/Editor/GeneratedWorldDeleteMenu.cs` を新規作成:

```csharp
#if UNITY_EDITOR
using System.IO;
using UnityEditor;

namespace Client.Starter.Editor
{
    /// <summary>
    /// 生成ワールドを削除して次回起動時に新しいseedで再生成させるメニュー項目
    /// Menu item that deletes the generated world so the next launch regenerates it with a new seed
    /// </summary>
    public static class GeneratedWorldDeleteMenu
    {
        private const string DialogTitle = "Delete Generated World";

        [MenuItem("moorestech/Delete Generated World")]
        private static void DeleteGeneratedWorld()
        {
            // 再生中はサーバーがワールドを使用中のため削除を拒否する
            // Refuse deletion during play mode because the server is using the world
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog(DialogTitle, "再生中は削除できません。再生を停止してください。", "OK");
                return;
            }

            var worldDirectory = GeneratedWorldPlayModeSettings.WorldDirectoryPath;
            if (!Directory.Exists(worldDirectory))
            {
                EditorUtility.DisplayDialog(DialogTitle, $"生成ワールドはありません。\n{worldDirectory}", "OK");
                return;
            }

            var confirmed = EditorUtility.DisplayDialog(DialogTitle,
                $"生成ワールドを削除します。次回起動時に新しいseedで再生成されます。\n{worldDirectory}", "削除", "キャンセル");
            if (!confirmed) return;

            Directory.Delete(worldDirectory, true);
        }
    }
}
#endif
```

- [ ] **Step 2: コンパイルして通ることを確認する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0件

- [ ] **Step 3: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Starter/Editor/GeneratedWorldDeleteMenu.cs*
git commit -m "feat: 生成ワールドの削除メニュー項目を追加"
```

---

### Task 4: エディタ実機での通し検証

ボタンUIのクリック自動化はできないため、ボタンが実行するのと同一の処理（SessionStateフラグ→startScene→EnterPlaymode）を `uloop execute-dynamic-code` で駆動して検証する。

**Files:** なし（検証のみ）

**Interfaces:**
- Consumes: `GeneratedWorldPlayModeSettings.SessionStateKey` / `WorldDirectoryPath`（Task 1）

- [ ] **Step 1: 事前状態をクリーンにする**

初回生成を検証するため、world_generated が残っていれば削除する（冪等）:

Run: `uloop execute-dynamic-code --project-path ./moorestech_client --code "var p = Client.Starter.Editor.GeneratedWorldPlayModeSettings.WorldDirectoryPath; var existed = System.IO.Directory.Exists(p); if (existed) System.IO.Directory.Delete(p, true); UnityEngine.Debug.Log(\"path: \" + p + \" existed: \" + existed);"`

Expected: `path: <saves>/world_generated existed: <True|False>`（ログに出たパスを以降のStepで使う）

- [ ] **Step 2: ボタン相当の処理でPlayModeへ入る**

Run: `uloop execute-dynamic-code --project-path ./moorestech_client --code "UnityEditor.SessionState.SetBool(Client.Starter.Editor.GeneratedWorldPlayModeSettings.SessionStateKey, true); UnityEditor.SceneManagement.EditorSceneManager.playModeStartScene = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEditor.SceneAsset>(\"Assets/Scenes/Game/GameInitialaizer.unity\"); UnityEditor.EditorApplication.EnterPlaymode();"`

注意: 他worktreeのUnityがPlayMode中だとサーバーポート・CEF TMPDIRが衝突する。同時実行しない。

- [ ] **Step 3: 起動完了とワールド生成を確認する**

60秒程度待ってから:

Run: `uloop get-logs --project-path ./moorestech_client --log-type Error`
Expected: 起動起因のエラーなし

Run: `uloop execute-dynamic-code --project-path ./moorestech_client --code "var p = Client.Starter.Editor.GeneratedWorldPlayModeSettings.WorldDirectoryPath; UnityEngine.Debug.Log(\"world.json: \" + System.IO.File.Exists(System.IO.Path.Combine(p, \"world.json\")));"`
Expected: `world.json: True`（world.jsonの中身の `mapMode` が `generated` であることも `cat` で確認）

- [ ] **Step 4: PlayModeを終了し後始末を確認する**

Run: `uloop control-play-mode --project-path ./moorestech_client --action stop`

停止後（ドメインリロード待ち45秒）:

Run: `uloop execute-dynamic-code --project-path ./moorestech_client --code "UnityEngine.Debug.Log(\"flag: \" + UnityEditor.SessionState.GetBool(Client.Starter.Editor.GeneratedWorldPlayModeSettings.SessionStateKey, false) + \" startScene: \" + (UnityEditor.SceneManagement.EditorSceneManager.playModeStartScene == null ? \"null\" : \"set\"));"`
Expected: `flag: False startScene: null`（R4の後始末確認）

- [ ] **Step 5: 2回目起動で継続ロードを確認する**

Step 2 を再実行し、60秒待って `uloop get-logs --log-type Error` がエラーなし・world_generated 内の `world.json` の作成日時が変わっていないこと（再生成されていないこと）を確認する。確認後 `uloop control-play-mode --action stop` で停止する。

Expected: 同一ワールドで続きから起動（R3）

- [ ] **Step 6: 検証結果を記録してコミットする**

検証で発見した問題があれば修正してからこのタスクを閉じる。修正が発生した場合はテスト→コンパイル→コミットのサイクルを守る。

```bash
git status  # 未コミットの変更が無いことを確認
```

---

### Task 5: 全ブランチレビュー（省略不可）

- [ ] **Step 1: moores-code-review スキルで全ブランチレビューを実行する**

必ず最後にコードレビュースキル（moores-code-review）で全ブランチレビューを実行すること（自動実行・ゴール文言による省略不可）。指摘があれば修正→再コンパイル→再テスト→コミットまで行う。

---

## 判断記録（ADR）

- 設計ADR: `docs/adr/0009-generated-world-editor-play-button.md`（スコープ=エディタ専用ボタン・永続化・削除メニュー・seed UIなし。出所: ユーザー裁定 2026-08-12、`.decisions/2026-08-12-generatedワールドプレイはエディタ専用ボタンで提供する.md`）
- 削除メニューの配置を `Client.Starter/Editor/`（パス定数の所有者と同居）とした。`Scripts/Editor/Toolbar/` は10ファイル上限に達するため回避を兼ねる。出所: agent前提（単一ソース原則＋1ディレクトリ10ファイル規約）
- 削除メニューのユニットテストは書かない。実体が `Directory.Exists` ガード＋`Directory.Delete` のみでダイアログ分岐はテスト不能なため、Task 4 の実機検証でカバーする。出所: agent前提（YAGNI）
- Task 4 の検証はツールバーUIクリックの代わりに同一コードパス（フラグ→startScene→EnterPlaymode）を `uloop execute-dynamic-code` で駆動する。ボタンの `OnClicked` 自体は前例と同型3行のため目視レビューで足りる。出所: agent前提（unity-playmode-recorded-playtestの知見: OS入力シミュレーション禁止）
- `defaultDockIndex = 1`（NoSave Playの隣）。出所: agent前提（既存ボタンがindex 0）
