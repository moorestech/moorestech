# 決定論的クライアント基盤 — 設計仕様

作成日: 2026-05-23
ブランチ: feature/ph4
ステータス: draft (brainstorming → spec フェーズ)

## 1. 目的とゴール

moorestech クライアントを「実用上の決定論」基盤の上に乗せ、以下の2用途を**同一基盤**で実現する。

1. **AI / テストプレイ駆動** — AI エージェント（Claude 等）が `N tick 進める` / `W キーを 30 tick 押しながら進める` / `スクリーン座標をクリックする` を細粒度に指示できる C# API を Editor PlayMode 内に提供する
2. **ユーザーバグ報告のほぼ完全再現** — エンドユーザーが「入力ログ + サーバセーブスナップショット + 環境 manifest」を提出すれば、開発者の Editor 上でほぼ同一最終状態を再現できるリプレイ機構

## 2. 保証範囲と非保証範囲

### 保証する
- 同一 OS / 同一 build / 同一 mod・master データ / 同一 Unity・InputSystem・PhysX バージョン下で、録画 → Editor リプレイの最終状態（プレイヤー座標 / インベントリ / ワールド / サーバ tick / 主要 datastore hash）が一致すること
- AI 駆動 API が決定論的に N frame 進行・キー入力・クリック・複合シーケンスを表現できること

### 保証しない（MVP では明示的に切る）
- 物理浮動小数の OS / CPU アーキ間完全一致
- Mac 録画 → Win リプレイ等のクロスプラットフォームリプレイ
- Audio 完全同期
- 外部 Web リクエスト / `UnityEngine.Application` 系の完了タイミング完全再現
- 旧 `UnityEngine.Input.*` 直叩き経路の再現（リプレイ対象外と宣言）

## 3. 設計の柱

### 3.1 1 client tick = 1 Unity frame
- AI 駆動・リプレイ時は `Time.captureDeltaTime = 1f / 60f` を立て、Unity の frame 進行を実時計から切り離す
- 通常プレイ時は captureDeltaTime を設定せず realtime 進行
- サーバ側 `Core.Update.GameUpdater`（20Hz 固定 tick）はそのまま維持。クライアント frame と直接対応はしない（サーバ tick はサーバ自身が進める）

### 3.2 PlayerLoop 内の固定実行順序
AI / リプレイモードでは 1 frame 内の処理順序を以下に固定する。

1. `InputRecorder` または `ReplayInputTimeline` から該当 frame の入力状態を `ManualInputSystemBridge` に inject
2. `InputSystem.Update()` を手動呼び出し（`InputSettings.updateMode = ProcessEventsManually`）
3. `Physics.Simulate(fixedDt)`（`Physics.autoSimulation = false`）
4. ゲームロジック Update（既存 MonoBehaviour Update + 既存 `Client.Update.ClientGameUpdater` 経由）
5. レンダリング

通常 realtime モードでは Unity 標準 PlayerLoop に任せ、上記順序は ClientFrameDriver が wrapping するだけにする。

### 3.3 ネットワーク非同期性は best-effort
- サーバ ⇄ クライアントのパケット記録は行わない
- リプレイ時は Editor PlayMode 内に「クライアント + ローカルサーバ」を同プロセスで起動し（既存 `EditModeInPlayingTest.LoadMainGame` パターンの延長）、サーバセーブ snapshot から復元 → クライアント入力を timeline 通りに inject
- パケット往復タイミングがズレた場合の差分は DeterminismProbe で検出するが、MVP では「許容範囲外なら警告」に留める

## 4. コンポーネント

### 4.1 `ClientGameClock`（static / API 中核）
- `int Frame` — 起動からの frame 連番
- `float DeltaTime` — captureDeltaTime / realtime dt を抽象化
- `double Time` — Frame * DeltaTime 累積
- `IEnumerator WaitFrames(int n)` — coroutine から N frame 進行を待つ
- `Mode { Realtime, AiStep, Replay }` の切替 API

### 4.2 `ClientFrameDriver`（MonoBehaviour、PlayerLoop に挿入）
- 単一インスタンスをシーンに常駐
- realtime: Unity 標準 PlayerLoop に乗る（既存挙動と同等）
- ai-step: 外部 C# から `StepFrames(n)` 呼び出しを受け、`Time.captureDeltaTime` を立てて n 回 `yield return null`
- replay: `ReplayInputTimeline` の終端まで自動進行
- 各 frame で「inject → InputSystem.Update → Physics.Simulate → game Update」の順序を保証

### 4.3 `ReplayInputTimeline`（データ）
正規化入力表現。frame をキーに以下を持つ。
- `KeyBitset` — 押下中キーの bitset
- `MouseScreenPos` (Vector2)
- `MouseDelta` (Vector2)
- `MouseButtonBitset`
- `Scroll` (float)
- `UIFocusTarget` — フォーカス中の UI 識別子 / path（解像度非依存にしたい場合用）
- `PointerRaycastTarget` — Raycast 先 GameObject path（参考、検証用途）

録画と AI DSL の両方が最終的にこの形式に正規化される。

### 4.4 `AiInputDsl`（API レイヤー）
frame タイムライン DSL。例:

```csharp
await ReplayDriver.Run(90, script =>
{
    script.Hold(Key.W, from: 0, frames: 30);
    script.Tap(Key.E, frame: 10);
    script.Click(screenX, screenY, downFrame: 40, upFrame: 42);
});
```

- 内部で `ReplayInputTimeline` を組み立て、`ClientFrameDriver.Replay` モードで再生
- async/await 外側 + frame 内部表現の2層構造

### 4.5 `ManualInputSystemBridge`
- AI/Replay モード中は `InputSettings.updateMode = ProcessEventsManually` を立てる
- 既存 `OsInputSpoof` の「inject 直後に InputSystem.Update を即時 flush」する挙動を、ClientFrameDriver の順序制御に統合（OsInputSpoof を廃止または無効化）
- 入力 inject は `InputState.Change` / `InputSystem.QueueStateEvent` 経由
- クリックは down frame と up frame を必ず分ける（DSL レベルでも強制）

### 4.6 `InputRecorder`（shipping build 対応）
- `ClientFrameDriver` realtime モード時、毎 frame 正規化入力をリングバッファに追記
- chunk 単位（例: 2 秒分）で binary + gzip / zstd 圧縮して flush
- リング保持戦略: 直近 5〜10 分の入力 + 直近 N 個の server snapshot
- バグ報告 UI: 「最新 manifest + snapshot + 入力ログ」を zip 化、PII（チャット文・ユーザー名・ローカルパス）を除外してエクスポート
- 想定サイズ: 60fps, 1 frame 64〜256B → 1 時間 14〜55MB

### 4.7 `ServerSnapshotAtTickBoundary`
- 既存サーバ `GameUpdater` の tick end フックに hook を追加
- N 秒（例: 30 秒）ごとに「tick 境界の一貫した世界状態」を immutable DTO 化、別スレッドで MessagePack / JSON + 圧縮で書き出し
- リング保持: 最新 N snapshot のみ
- 既存 `Game.SaveLoad` 機構の流用を検討

### 4.8 `ReplayManifest`
バグ報告 / リプレイバンドルに必ず添付する環境情報。
- moorestech build version / git commit hash
- mod / master データ hash
- Addressables catalog hash
- Unity version / InputSystem version / PhysX version
- OS / CPU arch
- 画面解像度 / DPI / quality settings
- 開始シーン名 / 開始サーバ tick / 開始 client frame
- RNG seed と初期 state
- 物理設定 snapshot（gravity, defaultMaxAngularSpeed, bounce threshold 等）

リプレイ実行時、開発者環境の manifest と提出 manifest を比較し、不一致は警告として表示する。

### 4.9 `ReplaySessionLoader`
1. manifest 検証（致命的不一致なら abort、軽微なら警告継続）
2. Editor PlayMode 内に client + local server を起動
3. server snapshot から状態復元
4. Addressables / Scene ロード完了バリア（全 async 完了を待つ）
5. `ClientFrameDriver.Replay` モードで `ReplayInputTimeline` 再生開始

### 4.10 `DeterminismProbe`
- 主要 datastore の hash を frame ごとに計算して出力
  - プレイヤー座標 / 速度
  - インベントリ内容
  - ワールド（ブロック配置）hash
  - サーバ tick number
  - 列車状態 hash（既存 `TrainUnitTickState` 流用）
- self-replay test で「録画時 hash 系列 == リプレイ時 hash 系列」を frame 単位で比較

### 4.11 `PhysicsStepController`（Level 1 物理）
- `Physics.autoSimulation = false` / `Physics2D.simulationMode = Script`
- `ClientFrameDriver` から毎 frame `Physics.Simulate(fixedDt)` を呼ぶ
- 物理設定（gravity, bounceThreshold, defaultSolverIterations 等）を `ReplayManifest` に記録
- 既存 `FixedUpdate` 依存箇所がないか調査（Phase 0）し、必要なら ClientFrameDriver 駆動に寄せる

## 5. 必須対応事項（監査推奨より）

1. クリックは down frame と up frame を必ず分ける（同一 frame down/up は API で禁止）
2. 旧 `UnityEngine.Input.GetKey*` 残存箇所は Phase 0 で棚卸しし、リプレイ対象外として明示するか `InputManager` 経由に寄せる
3. `OsInputSpoof` の二重 `InputSystem.Update` 問題を `ManualInputSystemBridge` 一本化で解消
4. RNG は seed 付き専用ラッパーを定義し、`UnityEngine.Random` 直叩きを禁止する区画を明示
5. self-replay test を CI 必須化（短い操作セッション録画 → 即リプレイ → DeterminismProbe hash 完全一致を判定）
6. `UniTask.Delay` / `Task.Delay` の realtime 系利用箇所を Phase 0 棚卸し、ゲームプレイ判定は `ClientGameClock.WaitFrames` に寄せる

## 6. Phase 構成

### Phase 0: 棚卸し調査
- 旧 `UnityEngine.Input.GetKey*` 利用箇所
- `Time.realtimeSinceStartup` / `DateTime.Now` / `Guid.NewGuid` 利用箇所
- `UniTask.Delay` realtime / `Task.Delay` 利用箇所
- `Addressables.LoadAssetAsync` / `SceneManager.LoadSceneAsync` 完了タイミング依存箇所
- `FixedUpdate` 利用箇所と Physics 依存箇所
- `UnityEngine.Random` 利用箇所
- 結果をスプレッドシートまたは ADR として記録

### Phase 1: AI 駆動の最小版
- `ClientGameClock` / `ClientFrameDriver` / `ManualInputSystemBridge` / `AiInputDsl`
- `PhysicsStepController`
- Editor PlayMode 内テストから AI DSL で短いシナリオを動かせる状態まで

### Phase 2: 録画 + スナップショット
- `InputRecorder`（shipping build 対応）
- `ServerSnapshotAtTickBoundary`
- `ReplayManifest`
- ユーザーバグ報告 UI（zip エクスポート + PII 除外）

### Phase 3: リプレイ + 自動検証
- `ReplaySessionLoader`
- `DeterminismProbe`
- self-replay test の CI 自動化
- 環境差警告 UI

### Phase 4: クリーンアップ・移行
- Phase 0 で見つかった旧 Input 経路の `InputManager` 移行
- realtime delay 系の `WaitFrames` 移行
- RNG 直叩き箇所の seed ラッパー移行

## 7. テスト戦略

1. **Input edge test** — down frame で `WasPressedThisFrame == true`、hold frame で `IsPressed == true`、up frame で `WasReleasedThisFrame == true` を検証
2. **Click split test** — down/up 同一 frame は DSL レイヤーで禁止されていることを検証
3. **Self-replay test** — 短い操作セッション（例: 10 秒分）を録画 → snapshot から即リプレイ → DeterminismProbe hash 系列が frame 単位で一致することを検証
4. **Manifest mismatch test** — Unity バージョン / Addressables hash / 解像度違いの manifest をロードしたとき適切に警告が出ることを検証
5. **Long replay test**（CI nightly）— 数分単位の長い録画を流して累積誤差を観測

## 8. 既存資産との関係

- `Core.Update.GameUpdater`（サーバ）：既存維持。`ServerSnapshotAtTickBoundary` のフック点として利用
- `Client.Input.InputManager`：既存維持。`MoorestechInputSettings` の InputAction を `ManualInputSystemBridge` 経由で駆動する形に内部実装を切り替え（呼び出し側 API は変えない）
- `Client.Tests/EditModeInPlayingTest/OsInput`：`OsInputSpoof` は廃止または `ManualInputSystemBridge` の薄いラッパーに改修
- `TrainUnitTickState`：既存決定論 sim をそのまま `DeterminismProbe` の hash 対象に組み込む
- `EditModeInPlayingTestUtil.LoadMainGame`：`ReplaySessionLoader` の実装ベースとして流用

## 9. オープン項目（spec 確定までに詰める）

- snapshot 頻度のデフォルト値（30 秒 / 60 秒 / tick 基準どれにするか）
- リング保持件数（直近 10 snapshot / 20 snapshot / 容量上限ベース）
- shipping build 録画のデフォルト on/off（プライバシー設定との関係）
- バグ報告 zip の最大サイズ上限と送信経路（GitHub Issue 添付 / 専用エンドポイント / 手動）
- AI DSL の async/await 外殻と coroutine 外殻の両対応の必要性
