# マップ自動生成 P4（ワールド選択・新規作成UI＋複数ワールド対応）Implementation Plan

> **⚠️ 凍結（2026-07-23 ユーザー裁定）: このプランは現段階では実装しない。**
> ワールド管理・複数セーブデータ管理はマップ自動生成の受け入れに不要（生成ワールドの起動はP1の `StartServerSettings` CLI引数/エディタ経由で足りる）。P5はP1のみに依存するため本凍結の影響を受けない。将来ワールド管理機能を作る際に本プランを解凍・整合確認のうえ再利用する。
>
> **判断記録（ADR）**: 「マップ自動生成において現段階でワールド管理は不要」とのユーザー指摘に対し、P4の中身（saves/列挙・MainMenu CEF起動・worldSelect feature・連番採番・最終プレイ記録）が生成・転送・実行時構築のいずれとも結合せず、各フェーズ受け入れ条件もUIを要求しないことを確認して凍結を決定。棄却案: 完全削除（プランは独立性が高く将来再利用可能なため凍結が上位互換）／最小限のseed入力UIだけ先行実装（検証は開発内部のCLIで足りるためYAGNI）。`StartLocal` は現行挙動のまま無改修。「新規プレイヤーの初回起動をgeneratedにするか」は生成品質確定後・ワールド管理実装時に判断する。

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** メインメニューでワールド一覧の表示・新規作成（seed入力・mapMode選択）・選択起動をWeb UIで行えるようにし、`saves/` 配下の複数ワールドディレクトリを切り替えてプレイできるようにする。

**Architecture:** MainMenuシーンにWebUiHost＋CEFビューを起動し（現状はGameInitializerシーンでのみ起動）、Web UI側に `worldSelect` featureを新設する。Unity側はワールド一覧をtopicスナップショットで配信し、`world.start` アクションが `InitializeProprieties.CreateLocalServerArgs` に `CliConvert.Serialize(StartServerSettings)` を積んで既存の `StartLocal` と同じシーン遷移を踏む。ワールドの実体作成（生成・プロビジョニング）はP1の `WorldProvisioner` がサーバー起動時に行うため、UIは「引数を組んで起動する」だけの薄い層に徹する（受動的統合・新しい生成経路は作らない）。

**Tech Stack:** React 18 + TypeScript + Mantine + zustand（webui）/ Unity C# / WebSocket（既存bridge）/ CEF Unity

**親スペック:** `docs/plans/map-autogen-world-design.md` §6 P4行・§1（ワールド=ディレクトリ）
**前提:** P1完了・masterマージ済み（P2/P3とは独立に実装可能。ただし検証はP1のtemplateモードで行う）。作業ブランチ: `feat/map-autogen-p4`

## Global Constraints

- 1ファイル200行以下（partial絶対禁止）・1ディレクトリ10ファイルまで
- try-catch 基本禁止（外部境界のみ・根拠コメント必須）。デフォルト引数禁止。単純getter/setter禁止
- コメントは日本語→英語2行セット（各1行）を3〜10行ごと
- イベントはUniRx。シーン編集は uloop execute-dynamic-code 経由のみ
- .cs変更後は `uloop compile --project-path ./moorestech_client` 必須
- **webui配下を書く前に webui-design スキル必読**（CSS/DOM/インラインSVG限定・画像アセット化禁止）
- Web UIの新パネルは `src/features/<name>/{index.ts, XxxPanel.tsx, style.module.css}` 構成・zustand topic購読＋dispatchActionの片方向フロー
- 各タスク完了ごとにコミット。巻き込み確認必須

---

## 配置と前例

### データフロー地図

```
[saves/world_*/world.json] → WorldListProvider【新設・読み手】 → topic(world_select) → webui worldSelectPanel → world.startアクション → WorldStartActionHandler【新設】 → InitializeProprieties.CreateLocalServerArgs → 既存StartLocalフロー（GameInitializerシーン→ServerStarter→ServerInstanceManager→WorldProvisioner）
```

UIは既存起動フローへの**引数の書き手が1人増えるだけ**。`ServerInstanceManager` は既に args→`StartServerSettings` 解決に対応済み（調査確認: `CliConvert.Parse` 経由で機能する）。

### 配置決定インベントリと前例

| # | 項目 | 配置先 | 前例（役割同型） | 判定 |
|---|---|---|---|---|
| 1 | `WorldListProvider`（saves/配下のworld列挙） | `Client.WebUiHost/Game/Worlds/` | 列挙APIの既存前例なし（Editor専用 `SaveDataManager` の `Directory.GetFiles` のみ）→ **新規パターン注目点①** | 注目点 |
| 2 | ワールド一覧のUI配信 | topicスナップショット（`WebSocketHub` 経由） | 既存topic配信（`topicStore.ts` のsnapshot/event機構） | ok |
| 3 | `world.list` / `world.create` / `world.start` アクション | `Client.WebUiHost/Game/Actions/WorldActions.cs` | `BuildMenuSelectActionHandler`（JObject payload検証パターン）・`PauseMenuBackToMainMenuActionHandler`（シーン遷移トリガ） | ok |
| 4 | 起動引数の組み立て | `CliConvert.Serialize(new StartServerSettings{...})` | `CliConvert.Serialize<T>` は既存API（`Server.Boot.Args`）。エディタの skip-save 再生も同経路 | ok |
| 5 | MainMenuへのWebUiHost＋CEFビュー起動 | `MainMenu.unity` に `MainMenuWebUi.prefab` 新設配置 | `MainGameUI.prefab` 内の `CefUnityBrowserSample`＋`WebUiCefNavigator` ペア（調査確認済み）→ ただしMainMenuでの起動は **新規パターン注目点②** | 注目点 |
| 6 | webui `worldSelect` feature | `moorestech_web/webui/src/features/worldSelect/` | `pauseMenu` featureの構成（index.ts/Panel.tsx/style.module.css） | ok |
| 7 | WorldMetaJson の読み取り | `WorldListProvider` から `Game.MapGeneration` の `WorldMetaJson` をデシリアライズ | P1 Task 7 産の型を読むだけ（書き手はWorldProvisionerのみのまま） | ok |

### 新規パターン（ユーザーレビュー注目点）

1. **ワールド列挙APIの新設**: ランタイム初の saves/ 列挙。`GameSystemPaths.SaveFileDirectory` 直下の「`world.json` を含むサブディレクトリ」のみをワールドとして認識（旧 `save_1.json` 等のフラットファイルは無視）。列挙はUnity側（ローカルファイルはWeb UIから直接読めないため）
2. **MainMenuシーンでのWebUiHost起動**: 現状はInitializeScenePipelineでのみ起動。MainMenu→GameInitializerと遷移してもホストを再起動しない冪等化（`WebUiHost.StartAsync()` を起動済みならno-opに）が必要。CEFビューはMainMenu用の軽量prefabを新設し、URLに `#/world-select` ルートを付与してゲーム内UIと画面を分岐
3. **新規作成の命名**: `world_1, world_2, ...` の連番自動採番（既存最大+1）。ユーザー入力名は採らない（YAGNI・P4スコープ外として記録）

### 機能パリティ（死活表）

| 現在使える操作 | P4後 | 根拠 |
|---|---|---|
| StartLocalボタン（uGUI・即起動） | **置換** | ワールド選択UIを経由する起動に変わる。旧ボタンは「最後に遊んだワールドで起動」として残す（挙動退化なし） |
| ConnectServer（リモート接続） | 生存 | 無改修（リモートはワールド選択不要） |
| エディタ「セーブをロード・保存しない再生」 | 生存 | P1 Task 9で一時ワールドディレクトリ化済み・UIを経由しない |
| Web UIのゲーム内パネル群 | 生存 | WebUiHost冪等化はStartAsyncの入口ガードのみで既存フロー無変更 |

---

### Task 1: WorldListProvider とワールド列挙

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Worlds/WorldListProvider.cs`
- Create: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Worlds/WorldSummary.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/UnitTest/WorldListProviderTest.cs`

**Interfaces:**
- Produces:

```csharp
// saves/直下の world.json を持つディレクトリだけをワールドとして列挙する
// Enumerates only directories under saves/ that contain world.json
public static class WorldListProvider
{
    public static List<WorldSummary> ListWorlds(string saveFileDirectory);
    public static string NextWorldDirectoryName(string saveFileDirectory);  // "world_<max+1>"
}
public class WorldSummary
{
    public readonly string DirectoryName;   // world_1
    public readonly string MapMode;         // world.json由来
    public readonly int Seed;
    public readonly string CreatedAt;       // ISO8601
    public WorldSummary(string directoryName, string mapMode, int seed, string createdAt) { ... }
}
```

- [ ] **Step 1: テストを書く**（一時ディレクトリに world_1（world.jsonあり）/ world_2（world.jsonなし=無視）/ save_1.json（ファイル=無視）を作り、ListWorldsが1件・NextWorldDirectoryNameが "world_2"…ではなく既存名回避で "world_3" を返すことをAssert）→ FAIL確認
- [ ] **Step 2: 実装 → PASS確認**（world.jsonのデシリアライズはP1の `WorldMetaJson` を使用。壊れたworld.jsonはそのワールドをスキップせず例外＝破損の無言隠蔽をしない）
- [ ] **Step 3: コンパイル・コミット**

---

### Task 2: WebUiHost の冪等化と MainMenu CEFビュー

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Boot/WebUiHost.cs`（StartAsync入口に起動済みガード）
- Create: `moorestech_client/Assets/Asset/UI/Prefab/MainMenuWebUi.prefab`（execute-dynamic-code: CefUnityBrowserSample＋WebUiCefNavigator構成をMainGameUI.prefabから複製・軽量化）
- Modify: `moorestech_client/Assets/Scenes/Game/MainMenu.unity`（execute-dynamic-code: prefab配置＋`WebUiHost.StartAsync()` を呼ぶ起動MonoBehaviour `MainMenuWebUiBootstrap` 追加）
- Create: `moorestech_client/Assets/Scripts/Client.MainMenu/MainMenuWebUiBootstrap.cs`

- [ ] **Step 1: StartAsync 冪等化**（起動済みなら既存URLを返すだけ。停止系（GameShutdownEvent）は無改修）
- [ ] **Step 2: MainMenuWebUiBootstrap 実装**（`WebUiHost.StartAsync()` → CEFビューへ `#/world-select` 付きURLをLoadUrl。`WebUiCefNavigator` のリトライパターンを踏襲）
- [ ] **Step 3: execute-dynamic-code でprefab作成・シーン配置 → プレイモードでMainMenuにWeb UIが表示されることを確認**
- [ ] **Step 4: コンパイル・コミット**

---

### Task 3: world.* アクションハンドラとtopic配信

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Actions/WorldActions.cs`（`WorldStartActionHandler` / `WorldCreateActionHandler`）
- Create: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Worlds/WorldSelectTopic.cs`（一覧スナップショット配信）
- Modify: アクション/topic登録箇所（`WebSocketMessageDispatcher` の既存登録テーブル）

**Interfaces:**
- Consumes: `WorldListProvider`（Task 1）、`CliConvert.Serialize`、`InitializeProprieties`
- Produces:
  - topic `world_select`: `{ worlds: [{directoryName, mapMode, seed, createdAt}] }`
  - action `world.start` payload `{ directoryName: string }` — 既存ワールドで起動
  - action `world.create` payload `{ mapMode: "generated"|"template", seed: number }` — `NextWorldDirectoryName` で新ディレクトリ名を決め即起動（実体作成はWorldProvisionerに委ねる）

両ハンドラの終端は共通:

```csharp
// 引数を組んで既存のローカル起動フローへ渡す（生成はサーバー側WorldProvisionerの責務）
// Builds args and enters the existing local-start flow; provisioning belongs to WorldProvisioner
var settings = new StartServerSettings { WorldDirectory = worldPath, MapMode = mapMode, Seed = seed };
var proprieties = new InitializeProprieties(null, ServerConst.LocalServerIp, ServerConst.LocalServerPort, playerId)
    { CreateLocalServerArgs = CliConvert.Serialize(settings) };
InitializeScenePipeline.SetProperty(proprieties);
SceneManager.LoadScene(SceneConstant.GameInitializerSceneName);
```

（`StartLocal.cs` の遷移手順を踏襲。payload検証は `BuildMenuSelectActionHandler` のJObjectパターン）

- [ ] **Step 1: 実装＋登録**
- [ ] **Step 2: コンパイル → EditModeInPlayingTestで `world.create`(template) → 起動 → `saves/world_N/` にworld.json/save.jsonが生えることをAssert**
- [ ] **Step 3: コミット**

---

### Task 4: webui worldSelect feature

**Files:**
- Create: `moorestech_web/webui/src/features/worldSelect/index.ts`
- Create: `moorestech_web/webui/src/features/worldSelect/WorldSelectPanel.tsx`
- Create: `moorestech_web/webui/src/features/worldSelect/style.module.css`
- Modify: `moorestech_web/webui/src/bridge/transport/protocol.ts`（Topics/ActionPayloadsに world_select / world.start / world.create 追加）
- Modify: ルーティング（`App.tsx` に `#/world-select` 分岐）

**Interfaces:**
- Consumes: topic `world_select`・action `world.start` / `world.create`（Task 3）

- [ ] **Step 1: webui-design スキルを読む（必須）**
- [ ] **Step 2: WorldSelectPanel 実装**（ワールドカード一覧（mapMode/seed/作成日時表示・クリックで `world.start`）＋新規作成フォーム（seed数値入力・空ならランダム、mapModeトグル、作成ボタンで `world.create`）。`useTopic(Topics.worldSelect)`＋`dispatchAction` の片方向フロー）
- [ ] **Step 3: `npm run build` → dist差し替え → アプリ再起動（mooreseditorプラグインキャッシュと同様、CEF側も再起動必須）→ 表示・作成・起動のE2E確認**
- [ ] **Step 4: コミット**

---

### Task 5: StartLocal の「最後に遊んだワールド」化

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.MainMenu/StartLocal.cs`（PlayerPrefsに最後のworldディレクトリ名を保存し、それで起動。未記録ならworld_1）
- Modify: `WorldStartActionHandler`（起動時にPlayerPrefsへ記録）

- [ ] **Step 1: 実装 → コンパイル → 手動確認 → コミット**

---

### Task 6: 統合検証と最終レビュー

- [ ] **Step 1: unity-playmode-recorded-playtest で「新規作成(generated, seed指定)→起動→終了→一覧に表示→再選択起動で同一ワールド」を録画検証**
- [ ] **Step 2: 必ず moores-code-review スキルで全ブランチレビューを実行すること（自動実行・ゴール文言による省略不可）**
- [ ] **Step 3: 指摘反映 → pr-create**
