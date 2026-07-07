---
name: unity-playmode-recorded-playtest
description: 'Unity Editor を PlayMode 起動し、録画付きで end-to-end gameplay を検証する枠組み。第一選択はプレイテストDSL（Client.Playtest asmdef + tools/playtest/run-scenario.sh）による1コマンド一発実行で、preflight→PlayMode起動→シナリオ投入→result.json回収まで自動化される（実測ready~26秒）。DSLが無いブランチのみレガシー手動フロー（uloop往復）へフォールバック。Use When: 「Unity をコードで動かして録画したい」「フォーカス無しで PlayMode テスト」「Recorder を CLI 制御」「実プレイで動くか確認」「MonoBehaviour Update を回した状態で API 叩いて検証」「ロジック単体テストでは捕まらないシナリオを通しで確認」「プレイテストDSLでシナリオ実行」と言われた場合。フォーカス不要が必須要件のとき積極的に起動する。入力は必ず InputSystem QueueStateEvent で注入し OS simulate-keyboard/simulate-mouse-input は使わない（前面化して注入を汚染する・最重要）。masterデータはブランチ互換コミットへピン留めした worktree を使う（スキーマ不整合は MooresmasterLoaderException で初期化が無言死する）。クリック対象は collider.bounds.center を狙う、対象コードが legacy UnityEngine.Input を読むなら注入で駆動不可、InputSystem.Update()はスニペットで呼ばない、等の落とし穴は Body 参照。'
---

# unity-playmode-recorded-playtest

Unity PlayMode を CLI から自動操作し、シナリオを録画付きで end-to-end 検証する。フォーカス不要・本物の MonoBehaviour Update を回した状態で動作する。

## 何を解決するか

EditMode テストでは本番アセットも MonoBehaviour Update も UI Prefab も走らない。「実プレイで動くか」を機械的に確認する手段がない。このスキルは PlayMode + Unity Recorder + uloop で end-to-end を録画付き検証する。**フォーカス不要**なので別作業中の裏で回せる。

過去187セッションの分析で、旧来の「1操作=1CLI往復」方式は録画付き検証1回に **Bash呼び出し約170〜190回（固定sleep 30〜43回）** を要していた。これを解決するのが**プレイテストDSL**（下記）で、シェル1コマンドに圧縮される。

## 実行方式の選択（最初に判定する）

```bash
ls <repo-root>/moorestech_client/Assets/Scripts/Client.Playtest/ 2>/dev/null
```

- **ある → 「方式A: プレイテストDSL」を使う（第一選択・以下参照）**
- **無い → 「方式B: レガシー手動フロー」へフォールバック**（本ファイル後半）。ただし長期作業なら DSL のあるブランチ（`feature/playtest-stabilization`、コミット ad7535766 以降）の取り込みを先に検討する

どちらの方式でも「入力注入の鉄則」「masterデータのピン留め」「Gotchas」節は共通で適用される。

---

# 方式A: プレイテストDSL（第一選択）

`Client.Playtest` asmdef（Editor専用・Test Runner非依存の事前コンパイル済みDSL）と `tools/playtest/` のシェルランナーで、検証を1コマンドで完走させる。

## 一発実行

```bash
cd <repo-root>
./tools/playtest/run-scenario.sh \
    <path-to>/moorestech_client \
    tools/playtest/scenarios/<scenario>.cs \
    [master-server-dir]   # 省略時: /Users/katsumi/moorestech-worktrees/playtest-master/server_v8
```

内部の流れ（すべて自動・リトライ内蔵）:
1. **preflight**: CLI Loop疎通（タイムアウト=モーダル/ビジー検出兼務）→ コンパイル → master実在 → **マスタロードのドライラン**（EditモードでMasterHolder.Loadを試し、スキーマ不整合をPlayMode前に検出）
2. **boot**: `PlaytestBoot.PrepareAndEnterPlayMode(masterDir, noSave:true)` を EDC 1回で実行。NoSaveフラグ・`DebugServerDirectory`・`DebugObjectsBootstrap_Disabled` を設定して GameInitializer シーンから PlayMode 突入
3. **ready待ち**: ゲーム初期化完了イベント（`GameInitializedEvent`）で書かれる `ready.marker` をファイルポーリング（実測 ~26秒。EDCを連打しない）
4. **シナリオ投入**: シナリオ全文を EDC 1回で `PlaytestRunner.Run` に渡す（DSL側は事前コンパイル済みなのでAPI推測ミスが構造的に起きない）
5. **回収**: `result.json` の出現を待って表示。`Success` で exit 0/1

成果物は `moorestech_client/PlaytestResults/<セッション>/<ラン名>/` に result.json・スクショPNG・録画mp4（git管理外）。

## シナリオの書き方

execute-dynamic-code のスニペット形式（using可・文の羅列・return で値を返す）。`tools/playtest/scenarios/sample-chest.cs` が実証済みサンプル。

```csharp
using Client.Playtest;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using UnityEngine;

var options = new PlaytestRunOptions { Record = true };   // Record=true で mp4 録画内蔵
return PlaytestRunner.Run("my-scenario", options, async p =>
{
    await p.SetupFlatGround();                              // 足場(50x3x50 @ y30)生成+ワープ
    p.PlaceBlockDirect("木のチェスト", new Vector3Int(3, 32, 3), BlockDirection.North);
    await p.WaitBlockGameObject(new Vector3Int(3, 32, 3));  // クライアント側 View 出現待ち
    await p.GiveItem("鉄インゴット", 16);                    // 本番giveコマンド経路+サーバー在庫反映待ち
    p.GiveItemDirect("鉄板", 8);                            // サーバー直挿入（即時・状態を素早く作る用）
    await p.Until(() => p.CountItem("鉄インゴット") >= 16, 10f, "在庫反映");
    p.Assert(p.GetBlock(new Vector3Int(3, 32, 3)) != null, "チェスト存在");
    await p.Screenshot("final");                            // UI込みGameViewスクショ
});
```

### Driver API（PlaytestDriver）

| API | 用途 | 経路 |
|---|---|---|
| `SetupFlatGround()` | 足場生成+ワープ（無限落下対策の定型） | 板Primitive(50,4,50)@(0,30,0)、上面y=32ちょうど+`GroundGameObject`付与+Warp |
| `WarpPlayer(pos)` / `PlayerPosition` | テレポート/現在地 | `SetPlayerPosition`+サーバー同期 |
| `GiveItem(name, count)` | アイテム付与（反映待ち込み） | 本番 give コマンド経路 |
| `GiveItemDirect(name, count)` | アイテム付与（即時） | サーバーインベントリ直挿入 |
| `CountItem(name)` | サーバー在庫数 | メインインベントリ集計 |
| `PlaceBlockDirect(name, pos, dir)` | ブロック設置（即時・非消費） | `WorldBlockDatastore.TryAddBlock` |
| `RemoveBlock(pos)` / `GetBlock(pos)` | 削除/取得 | サーバーデータストア |
| `WaitBlockGameObject(pos)` | クライアント View 出現待ち | フレームポーリング(15s) |
| `Until(cond, timeout, label)` | 条件待機（固定sleepの代替） | 成否を result.json に記録 |
| `WaitSeconds(s)` / `Screenshot(name)` / `Assert(cond, label)` | 待機/撮影/検証 | |
| `SendCommand(cmd)` / `ServerService<T>()` | 低レベル脱出口 | VanillaApi / ServerContext DI |
| `PrepareBlockForUiPlacement(name, blockCount)` | **UI設置の前提を1行で**（アンロック＋建設コスト付与＋クライアント在庫反映待ち） | 下記2APIの複合 |
| `UnlockBlock(name)` / `GiveConstructionCost(name, blockCount)` | ブロック解放 / マスタ`RequiredItems`×個数の付与 | サーバー`UnlockBlock`→イベント同期 / give経路＋クライアント同期待ち |
| `DragPlaceViaUi(name, from, to)` | **UI経路**ドラッグ設置（ベルト等。向きは経路から自動解決） | B/Tab→ビルドメニュー→プレビュー→ドラッグ |
| `PlaceBlockViaUi(name, origin, dir)` | **UI経路**単クリック設置（向きはNorth固定） | 同上＋クリック。設置反映まで`Until`込み |
| `ExitToGameScreen()` / `WaitUiState(state, timeout)` / `CurrentUiState` | UIState遷移の操作/待機/確認 | B注入+`UIStateControl.CurrentState` |
| `PressKey(key)` / `SelectHotbar(slot)` / `AimAt(worldPos)` / `ClickPlace()` | 低レベルUI入力（slotは0始まり=キー1） | `SemanticInput`(QueueStateEvent) |

- **Direct系**=サーバーデータストア直叩き（状態を素早く作る・本番プロトコル非経由・インベントリ非消費）。**非Direct系**=本番経路。「UIバグの検証」と「状態構築」を使い分ける
- 例外・タイムアウト・Assert失敗はすべて result.json に落ちる（Runner内で捕捉し `Error` に記録）
- ブロック/アイテムは**マスタの日本語 Name** で指定（例: 「木のチェスト」「鉄インゴット」）。実在名は master の blocks.json / items.json で確認

### UI経路設置（Phase 3実装済み・等価性実証済み）

実プレイヤーと同じキーマウス経路での構築は `DragPlaceViaUi` / `PlaceBlockViaUi` を使う（`tools/playtest/scenarios/belt-line-via-ui.cs` がdirect版と同一assertで全通過した実証例）。前提と制約:

1. **事前に `PrepareBlockForUiPlacement(name, 個数)` が必須**: ビルドメニューは解放済みブロックのみ表示し、UI設置はインベントリの `RequiredItems` を消費する（direct設置と違う）。この1行がアンロック＋コスト付与＋クライアント在庫反映待ちまで面倒を見る
2. **メニュー再オープンはTab**: PlaceBlock中のBはGameScreenへ抜ける。`OpenBuildMenuAndSelectBlock` が状態を見て自動でB/Tabを使い分ける
3. **単クリック設置の向きはNorth固定**: place system内部の `_currentBlockDirection` は外部から読めないため回転キー注入は未対応。向きが要るラインはドラッグ（経路から自動解決）で組む
4. **注入が効くのは InputSystem/HybridInput 経由のコードのみ**: legacy `UnityEngine.Input` 直読み箇所（21ファイル中、設置プレビュー・UIState遷移キー・右クリックカメラは `Client.Input.HybridInput` へ移行済み）。新たに駆動しない入力を見つけたら HybridInput 化する
5. ビルドメニューのスロット選択は座標クリックでなく **EventSystem直叩き**（`ExecuteEvents.pointerDown/UpHandler`）で行っている（OSカーソル非依存・カメラ非干渉）

## masterデータのピン留め（worktree運用の要）

**moorestech_master リポジトリの HEAD は「いま最も進んだブランチ」用のスキーマに移行していることがある**。ブランチのスキーマ（VanillaSchema/*.yml）と master JSON が不整合だと `MooresmasterLoaderException` で PlayMode 初期化が**無言死**する（例: HEAD が plan4 用 `sortPriority` に移行済みなのに、旧ブランチは `priority` を期待 → data[0].priority で例外）。

対策（実証済み手順）:
1. **ブランチ互換コミットにピン留めした master の git worktree を作る**:
   ```bash
   git -C /Users/katsumi/moorestech_master worktree add \
       /Users/katsumi/moorestech-worktrees/playtest-master <互換コミット>
   ```
   互換コミットの特定: リポジトリ直下 `.moorestech-external-revisions.json` の `moorestech_master.commitHash` が**そのブランチがコミットした時点の期待値**（今回はこれで 584a14e と特定できた）
2. run-scenario.sh の第3引数（または preflight の第2引数）にそのパスを渡す
3. preflight [4/4] のマスタロードドライランが不整合を PlayMode 前に検出してくれる
4. **注意**: Unity Editor が `.moorestech-external-revisions.json` と `_CompileRequester.cs` を自動書き換えすることがある。スキーマを更新していないなら**コミットせず revert する**（`git checkout -- <両ファイル>`）

## DSL利用時の落とし穴（実際に踏んだもの）

1. **コンパイル直後・ドメインリロード中の EDC は失敗する**: run-scenario.sh / preflight.sh は `edc_retry`（5秒間隔・最大6〜8回）で吸収済み。手で叩くときも1回の失敗で諦めない
2. **worktree 初回起動のスキーマ再コンパイル**: 「Schemaフォルダに変更がありました→Core.Master再コンパイル」が**PlayMode中に発火するとドメインリロードで初期化が破壊される**（WebUiHost が起動→停止を繰り返すのが痕跡）。worktree 初回は PlayMode 前に `uloop compile` を一度通して落ち着かせる
3. **DebugObjectsBootstrap**: 無効化しないと IngameDebugConsole が毎フレーム NRE を吐く環境がある（エラーログ1.9万件で本当のエラーが埋もれた）。`PlaytestBoot` はテストと同じ `DebugObjectsBootstrap_Disabled` SessionState フラグを自動設定し、Play終了時に復元する
4. **worktree の Library**: メインの `Library`(28G) を `cp -Rc`（APFS clonefile）で複製すれば再インポート不要で数秒。**メイン側の Unity が閉じている時に行う**。`Assets/PersonalAssets`（非公開アセット）と `UserSettings` も同様にコピー
5. **moorestech_web の node バイナリ**: worktree には `moorestech_web/node/` が無く WebUiHost がエラーを出すが、**ゲーム初期化は継続する**（無視可。気になるなら `moorestech_web/setup.sh`）
6. **搬送ラインは投入前に接続数を assert する**: ベルト等の連結シナリオは、設置直後に末端ブロックの `GetComponent<Game.Block.Component.BlockConnectorComponent<IBlockInventory>>().ConnectedTargets.Count` を assert してから投入する。これを省くと接続失敗が「搬送 Until の90秒タイムアウト」として現れ、原因特定が1往復遅れる。接続0の原因は座標ミスのほか、**受け側ブロックのマスタ定義 `inventoryConnectors.inputConnects` が空（=そのブロックは搬入を受けない仕様）**のケースがあるので、終端ブロック選定時に blocks.json の当該定義を確認する

## トラブルシュート（DSL実行が進まないとき）

ready.marker が出ない / result.json が出ないときの診断EDC（1回で全部見る）:
```bash
uloop execute-dynamic-code --project-path <client> --code 'return "playing=" + UnityEditor.EditorApplication.isPlaying + " serverCtx=" + Game.Context.ServerContext.IsInitialized + " ready=" + Client.Playtest.Core.PlaytestGameReady.IsReady + " scene=" + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;'
```
- `serverCtx=False` のまま → 初期化前半で死んでいる。`uloop get-logs --log-type Error` と `--log-type Exception` を確認（**NREスパムで埋もれるので、メッセージをdedupして眺める**）。`MooresmasterLoaderException` ならピン留め master を確認
- `playing=False` → PlayMode に入っていない。boot の EDC レスポンスを確認
- 進行地点の特定には最新の Log 型ログ（`[WebUiHost] ready` 等）が有効

---

# Step 0: サブエージェントによる探索と実行計画作成（複雑シナリオでは必須）

PlayMode 起動より前に行う。単純なシナリオ（設置→give→assert 程度）なら DSL の Driver API 表だけで書けるので省略可。**複数ブロック連結・UI操作・列車などの複雑シナリオでは、必ず Plan または general-purpose サブエージェントに探索を委譲**し、以下を調査・出力させる:

1. **前提手順の連鎖** — シナリオ成立に先行して必要な操作（依存ブロック・必要アイテム・操作順序）。例: 「列車に乗る」には先にレール設置 → 列車設置が要る
2. **各操作の呼び出し経路** — DSL Driver / API 直叩き / `QueueStateEvent` 注入 / リフレクション遷移のどれか。**対象が触る入力読み取り箇所を grep し、legacy `UnityEngine.Input.*` を読んでいたら注入で駆動できない**ことを明示させる
3. **絶対座標と connector offset の表** — 複数ブロック連結なら YAML の `inputConnects[].offset` / `outputConnects[].offset` を「OriginalPos + offset = 絶対座標」で計算した表。手書き座標は「設置は成功するが繋がらない」を生む。**併せて受け側の `inputConnects` が空でないかを必ず確認**（空なら何をどこに置いても繋がらない。搬入可能な別ブロックを選ぶ）
4. **実コードで使うAPI/フィールド名の実在確認** — スニペットで叩く名前を**実ファイルを Read して確認**（存在しない名前を polling に書くと Result 空のまま無限ループ化する）
5. **Step 単位の実行計画** — 各 Step のコマンドと期待結果

---

# 方式B: レガシー手動フロー（DSLが無いブランチ用）

DSL 未導入ブランチでのフォールバック。全体フロー:

```
1. (前提) Unity 起動、CLI Loop サーバ起動済み（uloop list で確認）
2. NoSave フラグ: SessionState.SetBool("moorestech_SkipSaveLoadPlayMode", true)
   masterパス: DebugParameters.SaveString(ServerDirectory.DebugServerDirectorySettingKey, <ピン留めmaster>)
   デバッグ無効化: SessionState.SetBool("DebugObjectsBootstrap_Disabled", true)
3. uloop control-play-mode --action Play
4. readiness polling（下記ガード付き until。固定sleepの多段待機はしない）
5. シナリオ準備（世界配置・カメラframing・UI状態遷移）— まだ録画しない
6. de-risk probe: 単体入力を注入して期待状態を1つ確認（録画前の必須ゲート）
7. Recorder 起動（AppDomain に保持）← アクション直前で初めてON
8. シナリオ実行（API叩き / QueueStateEvent注入 / リフレクション遷移）
9. 各ビートで uloop screenshot --capture-mode rendering + 内部状態read
10. Recorder 停止（アクション直後に即OFF）→ control-play-mode --action Stop
11. 動画確認（0 byteは失敗）。必要なら ffmpeg -ss -t -c copy で切り出し
```

## readiness polling のガード（無限ループ事故防止・5点全部付ける）

ポーリングを `until <cmd> | grep -q <pattern>` "だけ" で書くと、スニペットのコンパイル失敗（存在しないAPI・shellエスケープで `&&` が化ける等）で `Result` が空になり**無限ループ化する**。

1. **事前単発検証**: ループ化前に1回叩いて `Success: true` + `Result` の形式を目視
2. **ループ内でエラーを毎回判定して即 abort（最重要）**: `error CS` / `CompilationErrors` 非空 / `"Success": false` / `Result` 空 → 待たずに停止。`Domain Reload` だけは一過性なので短く待って再試行。判定優先順位は**エラー検知 > 成功検知 > 待機**
3. **timeout 必須**（最後の保険。エラー検知の代わりではない）。抜けたら `uloop get-logs --log-type Error`
4. **grep は Result 行に限定**（`grep -q "Result.*ready=True"`）
5. **sleep は 5 秒以上**（短間隔はドメインリロードを煽る）

長い C# は `--code` 直書きせず**一時ファイルに書いて `$(cat file)` で渡す**とエスケープ事故を防げる。検証系スニペットは `$"..."` 補間より `"x=" + val` 連結が安定。

## Recorder の手動制御（DSLの `Record=true` が使えない場合）

snippet 間で RecorderController を持ち越すには `AppDomain.SetData/GetData` が唯一の手段（static/EditorPrefs 不可、Domain Reload で消える）:

```bash
uloop execute-dynamic-code --project-path <proj> --code '
using System;
using UnityEngine;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Encoder;
using UnityEditor.Recorder.Input;

var settings = ScriptableObject.CreateInstance<RecorderControllerSettings>();
settings.SetRecordModeToManual();    // 忘れると FrameInterval で勝手に停止する
settings.FrameRate = 30;
var movie = ScriptableObject.CreateInstance<MovieRecorderSettings>();
movie.name = "playtest";
movie.Enabled = true;
movie.EncoderSettings = new CoreEncoderSettings {
    EncodingQuality = CoreEncoderSettings.VideoEncodingQuality.High,
    Codec = CoreEncoderSettings.OutputCodec.MP4,
};
movie.ImageInputSettings = new GameViewInputSettings { OutputWidth = 1280, OutputHeight = 720 };
movie.OutputFile = "/tmp/playtest";   // .mp4 自動付与
settings.AddRecorderSettings(movie);
var ctrl = new RecorderController(settings);
ctrl.PrepareRecording();
ctrl.StartRecording();
AppDomain.CurrentDomain.SetData("playtest_recorder", ctrl);
return "recording started isRec=" + ctrl.IsRecording();'
```

停止は `GetData` で取得して `StopRecording()` → `sleep 2`（muxer完了待ち）→ `ffprobe` 確認。`scripts/start-recording.sh` / `scripts/stop-recording.sh` が参考実装。

**録画ウィンドウは最小に**: boot待ち・世界配置・framing・de-risk probe は録画OFFのまま行い、検証アクションの直前ON/直後OFF。尺の主因は入力ステップ間の必須 sleep 0.5 なので、録画窓を絞るのが効く（実測: 全部録ると3分/80MB、アクションだけだと1:23/34MB）。1280×720 30fps H264 で約25MB/分。

## 待機の取り方

- PlayMode の時間を進める待機は **shell sleep + 別スニペット**。EDC 内 `Thread.Sleep` は main thread を握って Update ごと止めるので禁止
- `InputSystem.Update()` をスニペット内で呼ぶと editor-update 文脈になり InputAction の `WasPressedThisFrame`/`IsPressed` が発火しない。**「queue → shell sleep（本物のフレームを進める）→ 別スニペットで read」が唯一機能する**

---

# 入力注入の鉄則（方式A/B共通・最重要）

世界は2つあり**両立しない。必ず (A) だけで通す**:

- **(A) InputSystem 直接注入 — `InputSystem.QueueStateEvent(Mouse.current / Keyboard.current, …)`**: フォーカス不要・決定論的
- **(B) OS入力 — `uloop simulate-keyboard` / `simulate-mouse-input`**: Editor を前面化させ、**実OSマウス状態が毎フレーム `Mouse.current` に forward されて (A) の注入を上書き無効化する。一度汚染すると PlayMode 再起動でしか戻らない**（ドラッグ中に ESC を simulate-keyboard で1回送っただけで以降全注入が死んだ実績あり）

```csharp
// マウス（押下/移動/解放）。HELD: true=押下中(ドラッグ), false=解放
using UnityEngine.InputSystem; using UnityEngine.InputSystem.LowLevel;
var st = new MouseState { position = new UnityEngine.Vector2(SX, SY) };
st = st.WithButton(MouseButton.Left, HELD);
InputSystem.QueueStateEvent(Mouse.current, st);   // Update()は呼ばない
```
```csharp
// キーボード（down と release を別スニペット=別フレームで）
var ks = new KeyboardState(); ks.Set(Key.Escape, true);
InputSystem.QueueStateEvent(Keyboard.current, ks);
// （shell sleep 0.5 を挟んでから）
InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
```

- **前提: 対象コードが InputSystem を読んでいること**。legacy `UnityEngine.Input.mousePosition` / `Input.GetKeyDown` を読む箇所は注入で駆動不可（moorestech の残存箇所: カメラズームF1/F2・設置高さQ/E・右クリックカメラ）。移行するか、リフレクションで状態遷移を代替
- **クリック座標は `Camera.main.WorldToScreenPoint(collider.bounds.center)`**（左下原点。`--capture-mode rendering` のスクショ座標と同一系）。論理原点や手書きoffsetを狙うと Terrain に当たって raycast MISS。詰まったら無マスク `Physics.Raycast` で当たる layer を可視化
- **ドラッグ中にキーを注入する各フレームでは、held マウス state（HELD=true・同座標）を同時に re-queue** しないと spurious press edge で `WasPressedThisFrame` が再発火し、選択リセット/誤確定が起きる
- 連続ドラッグは「1ステップ1スニペット（position+button 同時送出）+ 間に shell sleep 0.5」で組む。各ビート後に別スニペットで状態を read してから次へ

## de-risk probe（録画前の必須ゲート）

録画開始前に「単体の入力注入で期待状態が1つ成立するか」を必ず確認する（座標ズレ・入力汚染・API名乖離で全テイクが無駄になるのを防ぐ）。`collider.bounds.center` を1点 queue → sleep 0.5 → 別スニペットで内部状態 read（例: `selected=1`）。0 なら (a) `Mouse.current.position` が注入値か実OSカーソルか、(b) 無マスク raycast の当たり layer、(c) 狙い座標、(d) OS simulate-* 汚染、の順に切り分け。

## カメラ framing は「実プレイ視点」を壊すな（録画の価値そのもの）

- **不合格の典型**: `CinemachineBrain` を無効化して俯瞰カメラを直置き → アバター・地面・HUDが消えた「実プレイ感ゼロ」の絵。内部 state が正しくても不合格
- **正解**: 実プレイヤーカメラを生かしたまま、プレイヤー位置 × カメラ角度/距離をセットで反復。`SetControllable(false)` → `SetPlayerPosition`(Warp) で足場に立たせ、`StartTweenCamera`(pitch浅め・distance中庸) で寄せ、screenshot で**アバター+足場+対象が映る**まで詰める
- **推奨パターン「クリーン足場ステージ」**は DSL の `SetupFlatGround()` に実装済み（板Primitive(50,3,50)@(0,30,0)+Warp）。手動でも同じ構成を作る
- カメラ controller の `SetEnabled(false)` 系は `Camera.main` を null 化して raycast/WorldToScreenPoint が全滅する。**切り離さないのが正解**

## 検証と完了判定（成功条件4つ全部）

1. 動画が想定サイズで生成（`ffprobe` で Duration 確認、0 byte は失敗）
2. 期待する内部 state の達成（DSL なら result.json の Asserts、手動なら EDC read）
3. 検証スクショに期待 UI 要素が映っている（**内部 dump だけで済ませない** — 「データは正しいが UI Prefab 配線が違う」を見落とす。SubInventory 等を開いて撮る）
4. **絵が実プレイ視点**（アバター・地面・HUD が映る）

---

# Gotchas（方式A/B共通）

### Unity CLI Loop サーバ
- `uloop list` がエラーなら Editor 側で `Window > Unity CLI Loop > Server` を Start してもらう。`uloop compile`/`run-tests` は別経路なので、これらが通っても CLI Loop の保証にならない

### masterデータ・スキーマ不整合（worktree の最頻出死因）
- `MooresmasterLoaderException`（例: `PropertyPath: data[0].priority`）で初期化が無言死。**ブランチ互換コミットへピン留めした master worktree** + **preflight のマスタロードドライラン**で対処（方式Aの「masterデータのピン留め」節）
- 互換コミットは `.moorestech-external-revisions.json` の `commitHash` で特定できる
- 本番マスタは `IMasterValidator.Validate` が走るため、ロジックテストで出なかった master 不整合をこのスキルが最初に検出することが多い（これ自体が bug 検出機会）

### EDC（execute-dynamic-code）
- コンパイル直後・ドメインリロード中は失敗する → 5秒間隔でリトライ
- `using UnityEngine;` した snippet の `Object` は CS0104 → `UnityEngine.Object` と完全修飾
- 存在しないAPI推測が最頻出エラー → 書く前に実ファイルを Read/Grep でシグネチャ確認
- `Result` が空 =「未ready」ではなく「スニペットが壊れている」可能性が高い。待たずに abort 側へ倒す
- リポジトリ直下に複数 Unity プロジェクトがあると "Multiple Unity projects found" 警告が stdout を汚す → JSON抽出は `sed -n '/^{/,$p'` を通す（run-scenario.sh 実装済み）

### PlayMode 中のスキーマ再コンパイル
- 「Schemaフォルダに変更がありました」が PlayMode 中に発火するとドメインリロードで初期化破壊。worktree 初回は PlayMode 前にコンパイルを通す。Editor が自動書き換えする `.moorestech-external-revisions.json` / `_CompileRequester.cs` はスキーマ未更新なら revert

### moorestech: ブロックのサーバー設置 API
- `WorldBlockDatastore.TryAddBlock` は **5引数版**（`blockId, pos, direction, Array.Empty<BlockCreateParam>(), out var b`）が確実（拡張メソッド版は CS7036 になることがある）。DSL の `PlaceBlockDirect` はこれを内包
- 設置後はクライアント View 生成待ちが必要（DSL: `WaitBlockGameObject`。手動: sleep 2）

### 座標とエンティティの対応
- 論理座標(Vector3Int)と View 層の `BlockGameObject.OriginalPos` は回転・footprint origin 規約でズレることがある。View 層 API には View 層の座標を使う（`BlockGameObjectDataStore` をダンプして対応確認）

### スクリーンショット
- `--capture-mode rendering`: GameView レンダリング結果のみ（PlayMode必須・検証用は基本これ）。`window` はツールバーが映り込む
- DSL の `Screenshot()` は `ScreenCapture.CaptureScreenshot`（UIオーバーレイ込み・フォーカス不要・ファイル出現をフレームポーリング）
- 撮り損ねた瞬間は二度と撮れない。複数の中間状態は録画で押さえ、スクショは節目の証拠用

### control-play-mode
- `--action` は `Play` / `Stop` / `Pause` のみ（Status は無い）。再生確認は EDC で `EditorApplication.isPlaying` を読む

### エラーログの読み方
- IngameDebugConsole 等の毎フレーム NRE でエラーが数万件に膨れて本当のエラーが埋もれる。**メッセージを dedup して件数付きで眺める**（`DebugObjectsBootstrap_Disabled` で予防）
- Exception 型ログ（`--log-type Exception`）と Error 型は別枠。両方見る

### 環境構築（worktree）
- Library(28G) は `cp -Rc`（APFS clonefile）で複製 → 再インポート不要。**メイン側 Unity を閉じてから**
- `Assets/PersonalAssets`（非公開アセット）・`UserSettings` も同様にコピー
- 複数 worktree の Unity Editor は同時起動できる（プロジェクトパスが違えば独立）

## Available scripts

- `tools/playtest/run-scenario.sh`（リポジトリ側・方式A）— preflight→boot→シナリオ→result.json 回収の一発実行
- `tools/playtest/preflight.sh`（リポジトリ側・方式A）— 疎通/コンパイル/master実在/マスタロードドライラン
- `scripts/start-recording.sh` / `scripts/stop-recording.sh`（本スキル同梱・方式B用参考実装）— Recorder 手動制御のラッパー
