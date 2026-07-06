# 実プレイ検証 安定化調査レポート（2026-07-06）

> **✅ 実装済み（2026-07-07）**: 本レポートの提案のうちPhase 1+2（プリフライト・PlaytestSetup・
> PlaytestRunner一発実行・結果JSON回収・録画内蔵）は実装完了。使い方は `docs/playtest-dsl.md` と
> `unity-playmode-recorded-playtest` スキルを参照。本書は調査記録として保存。残りはPhase 3
> （セマンティック入力層）とドメインリロード無効化調査。

過去のClaude Codeセッション187件（メインプロジェクト108＋worktree 79）の定量分析、
実プレイ検証で実行された動的コード316件の分類、既存ツール・デバッグ基盤のサーベイを統合した調査結果。

Quantitative analysis of 187 past Claude Code sessions, classification of 316 dynamic-code
invocations used in play verification, and a survey of existing tooling, integrated into one report.

## TL;DR

**問題は「能力の欠如」ではなく「形態の欠陥」。** uloopには再生制御・入力注入・録画・スクショ注釈まで
能力が既に揃っている一方、それを「1操作＝1CLI往復」のRPCとして使っているため、録画付き検証1回に
**Bash呼び出し約170〜190回（うちuloop 60〜110回、固定sleep 30〜43回）** を費やしている。
しかもexecute-dynamic-code（以下EDC）呼び出しの **84%は「今どういう状態か」を確認するだけの読み取り専用スニペット**。

maestro / Playwright型の「事前コンパイル済みプレイテストDSL＋単発シナリオ一発実行」への転換は現実的で、
ツール呼び出しを数回まで圧縮できる見込み。

## 1. 問題点ランキング（インパクト順・実測値）

| # | 問題 | 実測 |
|---|------|------|
| 1 | ドメインリロード・固定sleep待機 | sleep 369回＝**合計3.7時間**の純粋待機。「Unity is reloading」46セッション370回、最悪1セッション92回。sleep45→90→120の多段待機が常態化 |
| 2 | Editor占有→再起動の悪循環 | `uloop launch` 456回、うち再起動`-r`185回。主因は「テスト一括ランがEditorを占有して固まる」。regex分割ランが回避策として定着 |
| 3 | EDCのAPI推測ミス | CSエラー623回/57セッション。存在しないAPI推測（`Exists`16回、`RemainingTicks`12回…）、`CS0104: Object曖昧参照`11回。1スニペット2〜4回リトライが常態 |
| 4 | 状態確認だけのターン浪費 | EDC 316件中**264件(84%)が読み取り専用**（ServerContext確認61・マスタ確認57・UI状態33…）。中央値247文字の小スニペットを1回ずつ往復 |
| 5 | マスタ不整合・未ロード起動 | worktreeでmasterパス未設定→`MasterHolder`がnullのままWorld遷移不能が典型。毎回手動ローダーガードを記述 |
| 6 | 無限落下・足場の再発明 | 足場関連105回・スポーン57回。「板Primitive(50x3x50)を(0,30,0)に置いてWarp」を毎セッション座標決めから作り直し |
| 7 | 入力注入の汚染問題 | OS simulate系はEditor前面化→実OSマウスが毎フレーム注入を上書き汚染、**PlayMode再起動でしか回復しない**。`QueueStateEvent`一択が結論済みだが両方式が15セッションずつ併存 |
| 8 | 録画の段取りコスト | Recorder制御はEDCで起動/停止、状態は`AppDomain.SetData`持ち越し、専用skill読込…と段取りが多い |

その他: Riderデバッガのtracepoint式評価でスレッド固まり、NRE 16セッション。
モーダルは「ビジーに見える」問題として体感されるが直接のログ痕跡は少数
（発生時は検出手段がなく無言のタイムアウトになるのが実態）。

## 2. 既存資産の棚卸し

思っている以上に揃っている。

- **uloop 1.6.3**: `control-play-mode`（フォーカス不要のPlay/Stop/Pause）、`record-input`/`replay-input`（入力のJSON録画・再注入）、`screenshot --annotate-elements`（UI要素に名前＋クリック座標を注釈）、`find-game-objects`/`get-hierarchy`
- **入力**: `Client.Tests/EditModeInPlayingTest/OsInput/OsInputSpoof.cs`（QueueStateEvent注入の抽象化レイヤ）。ただし**legacy Input残存箇所（カメラズームF1/F2・設置高さQ/E・右クリックカメラ）は注入で駆動不可**
- **検証ヘルパー**: `EditModeInPlayingTestUtil.LoadMainGame()`に`GiveItem`/`PlaceBlock`等が既存 — セマンティックAPIの種はもうある
- **デバッグ基盤**: DebugSheet（テレポート・アイテム無限付与・即採掘/即クラフト）、kill floor（`PlayerObjectController`のy<-50で自動復帰）、NoSave Play（`SessionState "moorestech_SkipSaveLoadPlayMode"`）、`DebugParameters.SaveString("DebugServerDirectory", …)`でmasterパス切替
- **AutoUnityTestEnv**（~/AutoUnityTestEnv）: tart VMでのEditModeテスト隔離実行環境（CI用途、PlayMode検証には非対応）
- **無いもの**: god mode / noclip、明示的Respawn、PlayModeのheadless実行

## 3. 提案 — プレイテストDSL＋一発実行ランナー

### 設計の核心

「DSLは事前コンパイル済み資産、シナリオは単発コード」の2層に分ける。

```
Client.Playtest/ (asmdef, Editor+PlayModeで参照可、Test Runnerには載せない)
├── PlaytestRunner.cs      … シナリオ受付・PlayMode起動・完走・結果JSON書き出し
├── PlaytestDriver.cs (P)  … セマンティックAPI本体
├── SemanticInput.cs       … QueueStateEvent注入・WorldToScreenPoint解決
├── PlaytestRecorder.cs    … Recorder自動起動/停止
└── PlaytestSetup.cs       … 足場生成・アイテム付与・NoSave・masterパス
```

シナリオの記述イメージ（「1行でセマンティック操作」）:

```csharp
await PlaytestRunner.Run(async p => {
    await p.Setup(flatGround: true, give: ("iron_ingot", 64), noSave: true);
    await p.PlaceBlock("belt_conveyor", new Vector3Int(0, 1, 0), BlockDirection.North);
    await p.DragItem("iron_ingot", from: p.Hotbar(0), to: p.BlockInventory("chest"));
    await p.Until(() => p.Block("chest").Inventory.Contains("iron_ingot"), timeoutSec: 10);
    p.Assert(p.Screenshot("after-drag"), "チェストに鉄が入った状態");
});
```

### 各問題への対応

- **実行は1往復**: シナリオ全文をEDC 1回で`PlaytestRunner.Run`に渡す。DSL側は事前コンパイル済みなので**API推測ミス（CSエラー623回）が構造的に消える**
- **待機のセマンティクス化**: `p.Until(条件)`のフレーム待ちで固定sleep 3.7時間分の浪費を撲滅。完走後に結果JSON（assert結果・エラーログ・スクショ/mp4パス）を1ファイルに書き、CLI側は`until [ -f result.json ]`の1コマンドで回収 → 「状態確認ターン84%」も消える
- **操作の2レイヤ**: 各操作は`via: UI`（QueueStateEvent＋WorldToScreenPointの実クリック経路）と`via: Direct`（`VanillaApi.SendOnly`直呼び）を選択可能に。「UIバグの検証」と「状態を素早く作る」を分離
- **録画内蔵**: `Run(record: true)`でRecorder自動起動→終了時停止→mp4パスを結果JSONに含める。skill読込・AppDomain持ち越しの段取り不要に
- **足場・スポーンの標準化**: `p.Setup(flatGround: true)`が既定で板＋Warpを行い、無限落下の再発明105回を1メソッドに集約
- **プリフライト1コマンド**: 実行前に compile → masterバリデーション → CLI Loop疎通 → モーダル検出（`screenshot --window-name`でモーダルウィンドウを検知して報告）をシェル1発にまとめ、「コンパイルできない」「マスタ不整合で起動しない」「モーダルでビジーに見える」の3大あるあるを実行前に判定

### 「永続化しない単発コード」の扱い

シナリオ文字列はEDCで流すのでコンパイル資産として残らない。資産化したいものだけ
`Client.Playtest/Scenarios~/`（`~`付きフォルダはUnityがインポートしない）に.cs片として置く。
テストランナーには一切載らない。

### 段階的実装案

1. **Phase 1（即効・半日級）**: プリフライトスクリプト＋`PlaytestSetup`（足場/give/NoSave/masterパス）を先行導入。sleepの多段待機を`Until`ポーリングヘルパーに置換
2. **Phase 2**: `PlaytestRunner`＋結果JSON回収の一発実行化、録画内蔵
3. **Phase 3**: セマンティック入力層（`ClickBlock`/`DragItem`等）。前提としてlegacy Input残存3箇所の新InputSystem移行が必要（ここだけプロダクトコード修正を伴う）
4. **検討事項**: Enter Play Mode Options のドメインリロード無効化（Play開始が数秒になる大幅短縮だが、static初期化前提コードの洗い出しが必要なので別調査推奨）

## 4. uloopプレイ検証の確定ノウハウ（落とし穴集）

- 入力注入は`InputSystem.QueueStateEvent`(Mouse.current/Keyboard.current)一択。`simulate-*`系は前面化→OSカーソル汚染→PlayMode再起動まで回復不能
- EDC内で`Object`はCS0104曖昧参照。`UnityEngine.Object`と書く。書く前に実シグネチャをGrepで確認
- EDC内`Thread.Sleep`はPlayMode Updateを止めるので禁止。`InputSystem.Update()`は呼ばない
- スニペット間の状態持ち越しは`AppDomain.SetData/GetData`のみ（static/EditorPrefsは不可）
- クリック座標は`Camera.main.WorldToScreenPoint(collider.bounds.center)`。`screenshot --capture-mode rendering`の座標と一致
- テスト一括ランはEditorを占有し固まる → `--filter-type regex`でクラス単位分割
- worktreeではPlayMode起動前に`DebugParameters.SaveString("DebugServerDirectory", <masterパス>)`必須
