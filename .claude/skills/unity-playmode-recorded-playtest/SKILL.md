---
name: unity-playmode-recorded-playtest
description: 'Unity Editor を PlayMode 起動し、uloop と Unity Recorder で end-to-end gameplay を録画付き検証する枠組み。Use When: 「Unity をコードで動かして録画したい」「フォーカス無しで PlayMode テスト」「Recorder を CLI 制御」「実プレイで動くか確認」「MonoBehaviour Update を回した状態で API 叩いて検証」「ロジック単体テストでは捕まらないシナリオを通しで確認」と言われた場合。フォーカス不要が必須要件のとき積極的に起動する。入力は必ず InputSystem QueueStateEvent で注入し OS simulate-keyboard/simulate-mouse-input は使わない（前面化して注入を汚染する・最重要）。クリック対象は collider.bounds.center を狙う、対象コードが legacy UnityEngine.Input を読むなら注入で駆動不可、InputSystem.Update()はスニペットで呼ばない、本番マスタ validation・AppDomain 持ち越し等の落とし穴は Body 参照。'
---

# unity-playmode-recorded-playtest

Unity PlayMode を CLI から自動操作し、シナリオを録画付きで end-to-end 検証する。フォーカス不要・本物の MonoBehaviour Update を回した状態で動作する。

## 何を解決するか

EditMode テストでは本番アセットも MonoBehaviour Update も UI Prefab も走らない。「実プレイで動くか」を機械的に確認する手段がない。このスキルは PlayMode + Unity Recorder + uloop で end-to-end を録画付き検証する。**フォーカス不要**なので別作業中の裏で回せる。

## 前提条件

1. **Unity CLI Loop サーバが起動済み**: Unity Editor のメニューから `Window > Unity CLI Loop > Server` を開いて Start。**起動していない場合 `uloop list` 等が "Unity CLI Loop server is not" エラーで失敗する**。`uloop compile` / `uloop run-tests` だけは別経路なので動くため、これらが動いても CLI Loop が動いている保証にはならない。`uloop list` でツール一覧が返ることを確認してから始める。

2. **Unity Recorder パッケージがインストール済み**: `Packages/manifest.json` に `com.unity.recorder` が含まれること。バージョンは 5.x 以降推奨。

3. **PlayMode で起動するシーンが分かっていること**: 通常はビルド設定の最初のシーン (例: `GameInitializer` 等のブートストラップシーン)。シーンを直接指定せずに `control-play-mode --action Play` を呼ぶとビルド設定の最初のシーンから走る。

4. **プロジェクトのブート完了判定方法を把握していること**: 「DI コンテナがロードされた」「主要 static クラスに値が入った」等の判定条件。例: moorestech では `ClientContext.VanillaApi != null && ServerContext.WorldBlockDatastore != null` が ready の指標。

5. **uloop CLI のパスが通っていること**: `which uloop` で見つかること。

## 重要な設計判断 (ユーザー指示を反映)

### 探索と実行計画作成は必ずサブエージェントに先行委譲する
PlayMode 起動・録画・シナリオ実行に着手する**前に、必ずサブエージェント（Plan または general-purpose）へコード/データの探索と実行計画作成を委譲する**。メインエージェントがいきなり PlayMode を起動してはならない。理由: playtest シナリオは「設置プロトコルの呼び方」「ブロックの前提依存（例: 鉄道は先にレール設置が必要）」「絶対座標と connector offset」「readiness 判定条件」「legacy Input 箇所」など、コードとマスタデータを実際に読まないと確定できない情報に依存する。これを探索しないまま実行すると、設置失敗・接続不一致・無限ループ polling などで録画が無駄になる。サブエージェントには次を必ず調査・出力させる:
- 検証対象シナリオを成立させる**前提手順の連鎖**（依存ブロック・必要アイテム・操作順序）
- 各操作の**呼び出し経路**（API / controller 層 / `QueueStateEvent` 注入 / リフレクション遷移のどれを使うか。OS simulate-* は使わない — 「入力注入の鉄則」節）
- **絶対座標とconnector offset の表**（複数ブロック連結時。後述「座標設計」参照）
- プロジェクトの**readiness 判定条件**と NoSave フラグ等の起動前提
- Step 単位に分解した**実行計画**（各 Step の uloop コマンドと期待結果）

メインはこの実行計画を受け取ってから Step 1 以降を実行する。

### 録画は Unity Recorder 一択
macOS の `screencapture -V` や `ffmpeg -f avfoundation` を使うアプローチも技術的には可能だが、**OS 越し画面キャプチャはフォーカス・解像度・他ウィンドウ重なりに脆弱**。Unity Recorder は GameView レンダリング結果を内部で直接受け取るので、Unity ウィンドウが他アプリの裏に隠れていても録画品質が変わらない。「フォーカス不要」が要件のとき OS 録画は選択肢から外す。

### 座標設計は別エージェントに事前検討させる
複数ブロックを連結するシナリオ (例: ベルト連鎖、回路) では **Plan サブエージェントに「絶対座標で input/output offset が一致するか」を表で出させてから実装**する。手書きで座標を決めると connector 不一致で「設置は成功するが繋がらない」状態になる。**connector の YAML データを実際に grep で読み**、各ブロックの `inputConnects[].offset` / `outputConnects[].offset` を「OriginalPos + offset = 絶対座標」で計算した表が信頼できる証拠。

### 検証は内部状態の dump ではなく UI を開いて視覚確認する
`WorldBlockDatastore.GetItem(slot)` で数値を返すだけだと「データレイヤは正しいが UI Prefab 配線が間違っている」バグを見落とす。**SubInventory state を強制起動して `screenshot --capture-mode rendering` で撮る**。プレイヤーが実際に見る画面に item アイコンが表示されることまで含めて確認する。内部 dump はサニティチェック用の補助。

### セーブをロード・保存しないモードで起動する (moorestech、必要に応じて)
playtest は再現性が命なので、既存セーブをロードせず・オートセーブもしないクリーンな状態で起動するのが望ましい。既存セーブで世界が汚れる / playtest の操作が本番セーブを破壊するのを防げる。「まっさらな世界から検証する」シナリオでは原則これを使う（既存セーブの状態を引き継いで検証したい場合のみ無効のまま）。

PlayMode 突入前 (EditMode 中) に SessionState フラグを立ててから `control-play-mode --action Play` する:

```bash
uloop execute-dynamic-code --project-path ./moorestech_client --code 'UnityEditor.SessionState.SetBool("moorestech_SkipSaveLoadPlayMode", true); return "set";'
```

`InitializeScenePipeline` がこれを読み、サーバを `AutoSave=false` + 一時 saveFilePath で起動する。フラグは PlayMode 終了時に自動クリアされるため Play のたびに立て直す。moorestech 固有の仕組み (Unity ツールバーの「NoSave Play」再生ボタンと同一)。

### 「プレイヤー側に近い」優先順位
プロジェクトに UI controller 層 (例: `LocalPlayerInventoryController`) があるなら、ネットワークプロトコル層 (`VanillaApi.SendOnly` 等) より上の controller 層を優先して叩く。理由: controller 層は「local UI 状態の更新 + サーバ送信」を 1 コールでやるので、UI と data の整合性まで確認できる。サーバ送信だけだと UI 更新パスが回らずバグを見落とす。

### 入力注入の鉄則 — OS入力(simulate-*)と InputSystem注入(QueueStateEvent)を混ぜない（最重要）

このスキルで入力を扱うとき、世界は2つしかなく**両立しない。フォーカス不要のこのスキルでは必ず前者(A)だけで通す**:

- **(A) InputSystem 直接注入 — `InputSystem.QueueStateEvent(Mouse.current / Keyboard.current, …)`**: フォーカス不要・決定論的。マウスもキーボードもこれで送る。**今回これだけで通したら一発で撮れた。**
- **(B) OS入力 — `uloop simulate-keyboard` / `uloop simulate-mouse-input`**: OS経由でキー/マウスを送るため **Unity Editor をフォアグラウンドに引き出す**。すると Game View が OSポインタフォーカスを取得し、**実OSマウス状態が毎 dynamic frame `Mouse.current` に forward され、(A) の `QueueStateEvent` 注入を上書きして無効化する**。

**致命的な相互作用（今回1時間溶かした事故）**: (A)でマウスを注入してドラッグ中に、ESCを (B)`simulate-keyboard` で1回送った瞬間に Editor が前面化 → 以降マウス注入が毎フレーム実OSカーソル(例: 画面外(1049,2086)・ボタン非押下)に戻され、選択が一切積まれなくなった。**一度この汚染が起きると PlayMode 再起動でしか戻らなかった**。

したがって:
- **DON'T**: PlayMode 中に `uloop simulate-keyboard` / `uloop simulate-mouse-input`（フォーカスを伴う系）を使う。キー入力も含めて。
- **DO**: マウスもキーも `InputSystem.QueueStateEvent` で注入する。`InputSystem.Update()` はスニペット内で呼ばない（後述）。

```csharp
// マウス（押下/移動/解放）。HELD: true=押下中(ドラッグ), false=解放
using UnityEngine.InputSystem; using UnityEngine.InputSystem.LowLevel;
var st = new MouseState { position = new UnityEngine.Vector2(SX, SY) };
st = st.WithButton(MouseButton.Left, HELD);
InputSystem.QueueStateEvent(Mouse.current, st);   // ← Update()は呼ばない。次のPlayModeフレームに処理させる
```
```csharp
// キーボード（down と release を別スニペット＝別フレームで）
var ks = new KeyboardState(); ks.Set(Key.Escape, true);
InputSystem.QueueStateEvent(Keyboard.current, ks);   // down
// （shell sleep 0.5 を挟む）
InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());   // release
```

#### 前提: 検証対象コードが「InputSystem を読んでいる」こと
`QueueStateEvent` で注入できるのは InputSystem 側(`Mouse.current` / `Keyboard.current` / InputAction)だけ。**検証対象コードが legacy `UnityEngine.Input.mousePosition` / `Input.GetKeyDown` を読んでいると、いくら注入しても駆動できない**（別系統）。Step 0 探索で対象シナリオが触る入力読み取り箇所を grep し、legacy を読んでいたら「`Mouse.current` 等へ移行しないと録画で駆動不能」とユーザーに報告する（今回は `BlockClickDetectUtil` の raycast を `Input.mousePosition` → `Mouse.current.position` へ移行して初めて録画が成立した）。

#### `InputSystem.Update()` をスニペット内で呼ぶな
queue 後に自前で `InputSystem.Update()` を呼ぶと、それは editor-update 文脈であり、InputAction の `WasPressedThisFrame()`/`IsPressed()` が発火しない（低レベル `Mouse.leftButton.isPressed` は変わるのに高レベル Action は False のまま）。**「queue → shell sleep（=本物の PlayMode フレームを進める）→ 別スニペットで読む」が唯一機能する。**

#### 座標系（混同すると上下反転）
- `Mouse.current.position` と `Camera.WorldToScreenPoint` は **左下原点ピクセル**（同一系。注入はこちらで計算する）。
- `uloop simulate-mouse-input --x/--y` は **左上原点**。混ぜると反転する（そもそも simulate-* は使わない方針だが念のため）。

#### ワールドオブジェクトを狙うときは collider.bounds.center
クリック対象の screen 座標は **`Camera.main.WorldToScreenPoint(collider.bounds.center)`** で出す。論理原点(Vector3Int)や `OriginalPos`+手書きoffset を狙うと、footprint origin 規約のズレで地面(Terrain)に当たり、Block 専用レイヤマスク付き raycast が MISS して `selected=0` になる（今回 collider 中心は論理原点から約 (+1.0, +0.9, +1.5) ズレていた）。詰まったら **無マスクで `Physics.Raycast` を撃ち、当たった collider の `gameObject.layer` を出して**、狙うべき層と実際に当たる層の差を可視化する。

## 全体フロー

```
0. サブエージェントにコード/データ探索 + 実行計画作成を委譲 (必須・後述 Step 0)
1. (前提) Unity 起動、CLI Loop サーバ起動済み
2. uloop control-play-mode --action Play     # PlayMode 突入
3. shell sleep 15-25 + readiness polling      # ブート待機
4. シナリオ準備 (世界配置・カメラframing・UI状態遷移) — まだ録画しない
5. de-risk probe: 単体入力を注入して期待状態を1つ確認  # 録画前の必須ゲート
6. uloop execute-dynamic-code で Recorder 起動 (AppDomain に保持) ← ここで初めてON
7. シナリオ実行ループ (a/b/c を組み合わせる) — Step 0 の実行計画に従う
   a. API 叩き                              # 設置・データ操作・準備
   b. InputSystem QueueStateEvent で入力注入  # マウス/キー。OS simulate-* は使わない
   c. リフレクションで状態強制遷移            # クリックでしか開けない UI 等の代替
8. 待機は shell sleep で              # Thread.Sleep / InputSystem.Update() は禁止
9. uloop screenshot --capture-mode rendering  # 視覚検証 (各ビート)
10. uloop execute-dynamic-code で Recorder 停止 ← アクション直後に即OFF
11. uloop control-play-mode --action Stop      # PlayMode 終了
12. 動画ファイル確認 (`/tmp/<name>.mp4`) + 必要なら ffmpeg -ss -t -c copy で切り出し
```

## 確立された録画ワークフロー（実プレイ視点・決定版 / 検証済み）

UIインタラクション機能を実プレイ視点で録る決定版手順。各 Step の詳細・スニペットは後続節を参照。**この順序を守れば、文脈ゼロの担当でも一発で撮れることを別subagentで実証済み。**

1. **Step 0 探索**（実コードを Read して確定）: 対象が読む入力経路（`Mouse.current`/InputAction か legacy `Input` か）・readiness 条件・設置API・状態遷移の仕方・**実際に叩く API/フィールド名の実在**。
2. **NoSave→Play→ready待ち**: SessionState フラグ→`control-play-mode Play`→単発検証してから timeout 付き until で ready をポーリング。
3. **クリーン足場ステージを作る**（「カメラframing」節の推奨パターン）: `CreatePrimitive(Cube)` の広い板を空中配置 → `SetControllable(false)`+`SetPlayerPosition`(Warp) でプレイヤーを足場に着地 → 対象を足場上・プレイヤー前方に設置（`sleep 2` で View 生成待ち）。
4. **実プレイヤーカメラのまま framing**: Cinemachine を切り離さず `StartTweenCamera`（pitch 浅め・distance 中庸）で寄せ、`screenshot --capture-mode rendering` で**アバター＋足場＋対象が映る**まで反復。
5. **de-risk probe**（録画前の必須ゲート）: `collider.bounds.center` を1点 queue→sleep→内部状態 read で「単体選択=1」を確認。通らなければ座標/入力汚染/layer を切り分け。
6. **Recorder ON**（アクション直前。boot/準備/de-risk は録らない）。
7. **シナリオ実行**: 入力は全て `QueueStateEvent`（マウス/キー）。ドラッグは held 維持、**キー注入時は held マウスstateも再アサート**、`InputSystem.Update()` は呼ばない。各ビートで `screenshot` ＋内部状態 read。
8. **Recorder OFF**（アクション直後）→ `control-play-mode Stop`。
9. **受け入れ4条件を全部確認**（Step 7 参照）: 動画非0 / 内部 state / UI要素 / **実プレイ視点（アバター・地面・HUD が映る）**。
10. **詰まったら**: 入力が一切効かない/汚染は **PlayMode 再起動で回復**。OS `simulate-*` は最後まで使わない。

## Step 0: サブエージェントによる探索と実行計画作成 (必須)

PlayMode 起動より前に行う。**メインは Plan または general-purpose サブエージェントを起動**し、検証対象シナリオについて以下を調査・出力させる:

1. **前提手順の連鎖** — シナリオを成立させるために先行して必要な操作（依存ブロック・必要アイテム・操作順序）。例: 「列車に乗る」には先にレール設置 → 列車設置が要る。
2. **各操作の呼び出し経路** — API 直叩き / controller 層 / `QueueStateEvent` 注入 / リフレクション遷移 のどれを使うか。**対象シナリオが触る入力読み取り箇所を grep し、`UnityEngine.Input.mousePosition` / `Input.GetKeyDown` といった legacy Input を読んでいたら注入で駆動できない**（`Mouse.current`/`Keyboard.current` への移行が要る）ことを明示させる。OS `simulate-*` は使わない前提で計画する。
3. **絶対座標と connector offset の表** — 複数ブロックを連結するなら YAML を読んで `inputConnects[].offset` / `outputConnects[].offset` を絶対座標で計算した表（「座標設計」節参照）。クリック対象は `collider.bounds.center` を狙う前提で screen 座標式も出す。
4. **readiness 判定条件と起動前提** — ブート完了判定の static 条件、NoSave フラグ等。
5. **実コードで使うAPI/フィールド名の実在確認** — スニペットで叩く予定のメソッド/プロパティ/private フィールド名を**実ファイルを Read して確認**する（タスク記述や記憶のAPI名を無検証で使わない）。存在しない名前を polling/probe に書くと `Result` が空になり until ループが無限化する。今回 `SelectedCount()` が実コードに無く（内部 `Dictionary` だった）reflection で `.Count` を読む必要があった。
6. **Step 単位の実行計画** — 各 Step の uloop コマンドと期待結果を列挙したもの。

メインはこの実行計画を受領・確認してから Step 1 以降に進む。探索を省略してメインが直接 PlayMode を起動することは禁止。

## Step 1: PlayMode 起動とブート待機

```bash
uloop control-play-mode --action Play
sleep 18   # 必要時間はプロジェクト依存。アセットロードが重い場合 30-60s 必要
```

**ブート完了 polling** をプロジェクト固有の readiness 条件で行う。

### Step 1a: ポーリング snippet を「先に 1 回単発実行」する (必須)

ループ化する前に、必ずスニペットを単発で叩いて `Success: true` かつ `Result` が期待形式で返ることを確認する。スニペット内の C# にコンパイルエラーがあると `Result` が空のまま返り、後段の `grep` が永遠に成功しないので **until ループに入れた瞬間に無限ループ化する** (実例: `MasterHolder.BlockMaster?.GetBlockMasters().Count` のように存在しないメソッドを書いてしまうケース)。

```bash
# 単発検証: Success=true と Result= の中身を目視確認する
uloop execute-dynamic-code --project-path ./<unity-project> --code '
using UnityEngine.SceneManagement;
// ↓ プロジェクト固有: 自分の DI / static state の null チェックに置き換える
// using YourProject.Context;
// return $"scene={SceneManager.GetActiveScene().name} ready={YourContext.IsInitialized}";
return $"scene={SceneManager.GetActiveScene().name}";
' 2>&1 | head -10
```

### Step 1b: 単発で動いた snippet を timeout 付き until に乗せる

`timeout` で外周ガードを掛け、Domain Reload を煽らないよう sleep は 5 秒以上にする。`grep` は `Result.*ready=True` のような**Result 行の中**にマッチさせる (CompilationErrors 等の他出力で偽陽性しないように)。

```bash
# 60 秒上限・5 秒間隔で polling
timeout 60 bash -c '
until uloop execute-dynamic-code --project-path ./<unity-project> --code "
using UnityEngine.SceneManagement;
return \$\"scene={SceneManager.GetActiveScene().name} ready={YourContext.IsInitialized}\";
" 2>&1 | grep -q "Result.*ready=True"; do
  sleep 5
done
' || { echo "boot timeout"; uloop get-logs --log-type Error --max-count 20; exit 1; }
```

`timeout` 60 秒で抜けた場合は `uloop get-logs --log-type Error` を確認 (本番マスタの validation 等でブートが止まっていることがある)。

## Step 1.5: de-risk probe（録画前の必須ゲート）

世界配置・カメラframing・状態遷移を終えたら、**録画を始める前に「単体の入力注入で期待状態が1つ成立するか」を確認する**。ここを通さずに本番シナリオを録ると、座標ズレ・入力汚染・API名乖離で `selected=0` のまま全テイクが無駄になる（今回これで撮り直しになった）。

例（破壊モードで1台ホバー選択できるか）:
```bash
# 1) collider中心の screen 座標を1点 queue（HELD=true）= 「入力注入の鉄則」節のマウス QueueStateEvent スニペットを SX/SY=collider中心・HELD=true で1回実行
uloop execute-dynamic-code --project-path ./<proj> --code '
var st = new UnityEngine.InputSystem.LowLevel.MouseState { position = new UnityEngine.Vector2(409f, 458f) };
st = st.WithButton(UnityEngine.InputSystem.LowLevel.MouseButton.Left, true);
UnityEngine.InputSystem.InputSystem.QueueStateEvent(UnityEngine.InputSystem.Mouse.current, st);
return "queued";'
sleep 0.5                               # 本物のフレームを進める
# 2) 別スニペットで内部状態を read（API名は実コードで確認したものを使う）
uloop execute-dynamic-code --project-path ./<proj> --code '
var sel = /* DeleteObjectState._selection を reflection 取得 */;
var dict = (System.Collections.IDictionary)sel.GetType()
  .GetField("_selectedTargets", System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)
  .GetValue(sel);
return $"selected={dict.Count}";'   # ← 1 を期待
```
- `selected>=1` なら注入が本物の raycast/選択を駆動できている → 録画へ。
- `selected=0` なら原因切り分け: (a) `Mouse.current.position` が注入値か実OSカーソルか直読み、(b) 無マスク `Physics.Raycast` で当たる collider の layer を出す（Terrain に当たっていないか）、(c) `collider.bounds.center` を狙っているか、(d) OS simulate-* で汚染していないか。直してから再 probe。

## Step 2: Unity Recorder の起動

**録画ウィンドウは最小に**: boot待ち・世界配置・カメラframing・状態遷移・de-risk probe は**録画OFFのまま**やり、検証アクション（撮りたい操作）の直前に StartRecording、直後に StopRecording する。これを怠ると尺が膨らむ（実測: 全部録ると 3分/80MB、アクションだけだと 1:23/34MB、さらに `ffmpeg -ss <start> -t <dur> -c copy out.mp4` で無再エンコ切り出しすると数秒/数MB）。尺の主因は入力ステップ間の必須 `sleep 0.5` なので、sleep は削れない＝録画窓を絞るのが効く。

snippet 間で RecorderController を持ち越すため `AppDomain.SetData` を使う (これが唯一信頼できる cross-snippet state):

```bash
uloop execute-dynamic-code --project-path ./<unity-project> --code '
using System;
using UnityEngine;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Encoder;
using UnityEditor.Recorder.Input;

var settings = ScriptableObject.CreateInstance<RecorderControllerSettings>();
settings.SetRecordModeToManual();
settings.FrameRate = 30;

var movie = ScriptableObject.CreateInstance<MovieRecorderSettings>();
movie.name = "playtest";
movie.Enabled = true;
movie.EncoderSettings = new CoreEncoderSettings {
    EncodingQuality = CoreEncoderSettings.VideoEncodingQuality.High,
    Codec = CoreEncoderSettings.OutputCodec.MP4,
};
movie.ImageInputSettings = new GameViewInputSettings {
    OutputWidth = 1280,
    OutputHeight = 720,
};
movie.OutputFile = "/tmp/playtest";   // .mp4 自動付与

settings.AddRecorderSettings(movie);
var ctrl = new RecorderController(settings);
ctrl.PrepareRecording();
ctrl.StartRecording();
AppDomain.CurrentDomain.SetData("playtest_recorder", ctrl);
return $"recording started: /tmp/playtest.mp4 isRec={ctrl.IsRecording()}";
'
```

## Step 3: シナリオ実行 - API/入力注入/リフレクションの使い分け

**入力は原則すべて `InputSystem.QueueStateEvent` で注入する**（「入力注入の鉄則」節参照）。OS `simulate-keyboard`/`simulate-mouse-input` は注入を汚染するため使わない。

| プレイヤー操作 | 手段 | 備考 |
|---|---|---|
| データ準備（インベントリ初期化、座標テレポート、世界配置）| **API 直叩き** (`execute-dynamic-code`) | プレイヤーの採掘・歩行を省略。フォーカス不要 |
| キー入力 (ESC/E/数字/Tab 等、InputSystem 配下) | **`QueueStateEvent(Keyboard.current, …)`** | down と release を別スニペット=別フレームで。OS simulate-keyboard は禁止 |
| マウス（クリック/ドラッグ/ホバー） | **`QueueStateEvent(Mouse.current, …)`** | position=`WorldToScreenPoint(collider.bounds.center)`。ドラッグは HELD=true で position を複数フレームに渡って更新、最後に HELD=false |
| legacy `UnityEngine.Input.GetKeyDown`/`Input.mousePosition` でしか拾えない箇所 | **コードを InputSystem へ移行**、または **リフレクションで状態強制遷移** | 注入では届かない。移行できないなら下記リフレクションで代替 |
| クリックでしか開けない UI window の強制起動 (例: SubInventory/DeleteBar) | リフレクションで `UIStateControl` の state を `OnExit→OnEnter→CurrentState set` | programmatic に開ける |

### 連続ドラッグの組み立て方（押下したまま移動）
`uloop` のマウスアクションは「押下したまま経路移動」を持たないので、`QueueStateEvent` を**1ステップ1スニペットで** position+button 同時送出し、間に `shell sleep 0.5`（最低0.4）を挟む。`InputSystem.Update()` はスニペット内で呼ばない（本物のフレームに処理させる）。

```
[down]  queue pos=p0, HELD=true   → sleep0.5 →  (この間に GetKeyDown 発火 = ドラッグ開始)
[move]  queue pos=p1, HELD=true   → sleep0.5 →  (IsPressed 継続、p1 上の対象を選択)
[move]  queue pos=p2, HELD=true   → sleep0.5
[up]    queue pos=p2, HELD=false  → sleep0.5 →  (GetKeyUp 発火 = 確定/削除)
```
各ビート後に状態を**別スニペットで read**して期待値（選択数・state 等）を確認してから次へ進む。

#### ⚠️ ドラッグ中に別デバイス(キー)を注入するときは held マウスstateを毎フレーム re-assert する（実測30分溶かした罠）
マウス左ボタンを HELD=true のまま「キーボードの `QueueStateEvent`(例: ESC down/up)」を注入すると、**キーイベントを跨いだ瞬間に held 中の左ボタンに spurious な press edge が乗り、`WasPressedThisFrame`(GetKeyDown)が再発火する**。ドラッグ系stateだと `BeginDrag()` 等が再走し、選択がリセット→カーソル下を再選択→意図せぬ確定(誤削除)になる。
対策: **ドラッグ中にキー入力を注入する各フレームで、同じスニペット内 or 直前直後に「左ボタン HELD=true・同座標」のマウスstateも一緒に re-queue し、押下を連続的に維持する**。これで press edge が再発火せず、ESC等のキーがクリーンに一発で効く。
```csharp
// 例: ESC を押す瞬間も左ドラッグを維持する（マウスstateを先に re-assert してからキーを送る）
var ms = new MouseState { position = new UnityEngine.Vector2(SX, SY) };
ms = ms.WithButton(MouseButton.Left, true);   // ← held を再アサート
InputSystem.QueueStateEvent(Mouse.current, ms);
var ks = new KeyboardState(); ks.Set(Key.Escape, true);
InputSystem.QueueStateEvent(Keyboard.current, ks);
```

### legacy Input 遷移をリフレクションで代替する典型コード

UI ステートマシン型のプロジェクトで `Input.GetKeyDown` が遷移トリガになっているとき:

```csharp
using System.Reflection;

var ctrl = UnityEngine.Object.FindFirstObjectByType<UIStateControl>();
var dictField = typeof(UIStateControl).GetField("_uiStateDictionary", BindingFlags.NonPublic | BindingFlags.Instance);
var dict = dictField.GetValue(ctrl);
var getStateMethod = dict.GetType().GetMethod("GetState");

// 1. 現在 state に OnExit
var cur = getStateMethod.Invoke(dict, new object[] { ctrl.CurrentState });
cur.GetType().GetMethod("OnExit").Invoke(cur, null);

// 2. 次 state に OnEnter (必要なら context を渡す)
var ctxContainer = new UITransitContextContainer();
ctxContainer.Set<ISubInventorySource>(/* プロジェクト固有のソース */);
var ctx = System.Activator.CreateInstance(typeof(UITransitContext), TargetStateEnum, ctxContainer);
var next = getStateMethod.Invoke(dict, new object[] { TargetStateEnum });
next.GetType().GetMethod("OnEnter").Invoke(next, new object[] { ctx });

// 3. CurrentState を更新 (private setter を SetValue で書く)
typeof(UIStateControl).GetProperty("CurrentState").SetValue(ctrl, TargetStateEnum);
```

`UIStateControl` 等のクラス名はプロジェクト固有。State machine pattern を採用していない場合は別アプローチ (ScreenSpaceOverlay の親 Canvas を SetActive 等)。

## Step 4: 待機の取り方

PlayMode 内で時間を進めるには `shell sleep` を使う。**execute-dynamic-code 内で `Thread.Sleep` を呼ぶと PlayMode の Update が止まる** (main thread を握ってしまうため)。同じく **`InputSystem.Update()` をスニペット内で呼んでも入力は前進しない**（InputAction が発火しない。「入力注入の鉄則」節）。入力注入も「queue → shell sleep → 別スニペットで read」で本物のフレームに処理させる。

```bash
# PlayMode が動き続ける正しい待機:
sleep 6
uloop execute-dynamic-code --code '...verify state...'

# ❌ NG (Update が 6 秒凍る):
uloop execute-dynamic-code --code 'System.Threading.Thread.Sleep(6000); return ...'
```


## Step 5: 視覚検証 (screenshot + UI 強制起動)

```bash
# 撮影前に UI を強制起動 (Step 3 のリフレクションパターン)
uloop execute-dynamic-code --code '... force UIStateControl to TargetState ...'
sleep 1   # UI 描画を待つ (TextMeshPro 等の async layout 対策)

uloop screenshot --window-name Game --capture-mode rendering
ls -t <unity-project>/.uloop/outputs/Screenshots/Rendering_*.png | head -1
```

`--capture-mode rendering` は GameView のレンダリング結果のみ取得 (PlayMode 必須)。`--capture-mode window` は EditorWindow 全体を取るが GameView ツールバーが映り込む。検証用は基本 `rendering`。

スクリーンショットは Editor の `.uloop/outputs/Screenshots/` 配下に保存される (Project ルート相対)。Read tool で画像表示できる。

## Step 6: 録画停止と動画取得

```bash
uloop execute-dynamic-code --code '
using System;
using UnityEditor.Recorder;
var ctrl = AppDomain.CurrentDomain.GetData("playtest_recorder") as RecorderController;
if (ctrl == null) return "ERROR: recorder not in AppDomain";
ctrl.StopRecording();
return "stopped";
'
sleep 2   # MP4 muxer 完了待ち
ls -lh /tmp/playtest.mp4
ffprobe /tmp/playtest.mp4 2>&1 | grep -E "Duration|Stream.*Video"

uloop control-play-mode --action Stop
```

## Gotchas

### Unity CLI Loop サーバ
- **`uloop list` がエラーでもユーザーに「サーバを起動して」と言うだけで OK**。ユーザー側で `Window > Unity CLI Loop > Server` から Start。Editor を再起動するとサーバも再起動が必要。
- `uloop compile` と `uloop run-tests` は別経路で動くため、これらが通っても `uloop list` / `simulate-*` / `screenshot` の保証にはならない。**この機能群を使う前に必ず `uloop list` で確認する**。

### 入力は QueueStateEvent 一択（OS simulate-* 禁止）— 詳細は「入力注入の鉄則」節
- **PlayMode 中に `uloop simulate-keyboard` / `simulate-mouse-input`（OS入力）を使うと、Editor が前面化し実OSマウスが `Mouse.current` を毎フレーム上書きして `QueueStateEvent` 注入を無効化する。一度汚染すると再起動でしか戻らない。** キーもマウスも `QueueStateEvent` で送る。
- `QueueStateEvent` で届くのは InputSystem 側だけ。対象コードが legacy `UnityEngine.Input.*` を読んでいたら注入で駆動できない → コードを `Mouse.current`/`Keyboard.current` へ移行するか、リフレクションで状態遷移を代替する。
- スニペット内で `InputSystem.Update()` を呼ぶと InputAction の `WasPressedThisFrame`/`IsPressed` が発火しない。queue→shell sleep→別スニペットで read。

### AppDomain 越しの RecorderController 保持
- `execute-dynamic-code` は snippet ごとに新しい compilation context。**static field や local 変数は持ち越されない**。
- `AppDomain.CurrentDomain.SetData(key, obj)` / `GetData(key)` は唯一信頼できる cross-snippet 手段。Domain Reload (PlayMode 終了等) で消える。
- `EditorPrefs` は string-only なので RecorderController のような Object 参照には使えない。
- 同じキーで上書きすると前の RecorderController が garbage collect されないことがある。新しい録画を始める前に明示的に Stop + null clear すべき。

### Thread.Sleep 禁止
- `execute-dynamic-code` の snippet は main thread で動く。`Thread.Sleep(N)` で N ミリ秒スレッドを止めると PlayMode の Update も止まる。
- 待機は **shell sleep + 別 snippet 呼び出し** で行う。snippet 間は PlayMode が動き続ける。

### `until` / `while` ループの無限ループ事故
ポーリングを `until <cmd> | grep -q <pattern>; do sleep N; done` で書くと、`<cmd>` 側のスニペットがコンパイルエラーで失敗した瞬間に `Result` が空になり、grep が永遠に成功しないため**無限ループに突入する** (実例: `MasterHolder.BlockMaster?.GetBlockMasters().Count` のような存在しないメソッドを polling snippet に書いてしまった結果、3 秒間隔で Domain Reload を煽り続けてユーザーが手動停止する羽目になった)。

これを避けるため、ポーリング系コマンドには以下 4 つのガードを必ず付ける:

1. **事前単発検証**: ループ化する前に snippet を単発で 1 回叩き、`Success: true` かつ `Result` 行が期待形式で返ることを目視確認する (Step 1a 参照)。
2. **timeout 必須**: `timeout 60 bash -c '...'` のように外周で時間制限する。タイムアウトしたら必ず `uloop get-logs --log-type Error` を吐かせて原因を可視化する。
3. **grep は Result 行に限定**: `grep -q "Result.*ready=True"` のように Result 行内のパターンに当てる。`grep -q "True"` のように緩いと CompilationErrors の中身などで偽陽性を起こす。
4. **sleep は 5 秒以上**: 短すぎる間隔 (例: `sleep 1`) で再試行すると Domain Reload と uloop の compilation を煽って Unity 側まで巻き込んで遅くなる。

`grep` の前で `Success.*false` や `CompilationErrors` をマッチさせて即 `break` する形でも良いが、`timeout` で外周ガードする方が確実。

### Recorder の落とし穴
- `RecordMode.Manual` を忘れるとデフォルトの `FrameInterval` で勝手に停止し、想定より早く録画が切れる。手動 Stop 前提のスクリプトでは必ず Manual に設定する。

### ブート失敗の切り分け
- PlayMode 起動後 30 秒経っても ready が True にならないとき、まず `uloop get-logs --log-type Error` を見る。
- 本番アセットの `IMasterValidator.Validate` で失敗すると ブートが止まり scene 遷移しない。**この skill が初めてプロジェクトに「本番アセットを通す」契機になる**ため、ロジックテストでは出ていなかった master data の不整合 (例: 存在しないアイテム key を参照するレシピ) を検出することが多い。
- これは bug 検出機会なので、「Recorder を使って end-to-end 確認すること自体が静的バリデーションだけでは捕まらないクラスのバグを発見する手段」と考える。

### カメラframing は「実プレイ視点」を壊すな（最重要 — 録画の価値そのもの）
このスキルで実ゲームを録る目的は **「プレイヤーが実際に見る画面」を撮ること**。ここを外すと、実システム(実ブロック/raycast/サーバー)を通していても、絵としては合成キューブを孤立空間に並べて撮るのと**本質的に変わらなくなる**。
- **不合格になる典型**: framing が面倒だからと `CinemachineBrain` を無効化して `Camera.main` を任意の俯瞰に直接配置する。→ アバター・地面・HUD が消え、空/雲を背景に対象が浮く「実プレイ感ゼロ」の絵になる（実際にこれで不合格動画が生まれ、担当が「面倒で打ち切った手抜き」と認めた。技術的障害ではなく、プレイヤー位置×tween角度の反復を惜しんだだけ）。
- **正しいやり方**: **実プレイヤーカメラ(Cinemachine 等)を生かしたまま** framing する。
  1. プレイヤーを対象が視界に入る位置・向きに立たせる（`PlayerObjectController.transform` 等）。
  2. `StartTweenCamera` 等の角度/距離を**プレイヤー基準で**詰める。FramingTransposer は follow 追従するので、カメラ単体でなく「プレイヤー位置 × カメラ角度/距離」をセットで反復する。
  3. `screenshot --capture-mode rendering` で **アバターと地面(必要なら HUD)が映り、かつ対象もフレームに入る**ことを確認するまで反復。
  4. その確定フレームで対象 collider 中心の `WorldToScreenPoint` を取り、de-risk probe → 録画へ。
- 罠（切り離しを試みた場合の二次被害）: カメラcontrollerの `SetEnabled(false)` 系は Camera コンポーネントごと殺し `Camera.main` を null 化する（raycast/WorldToScreenPoint 全滅）。**そもそも切り離さないのが正解。**
- framing の試行回数を惜しまない。2〜3回失敗しても俯瞰へ逃げず、プレイヤー位置を変えて追い込む。

#### 推奨パターン: 実ワールド内に「クリーン足場ステージ」を作る（検証済み・一番楽に実プレイ視点を保てる）
雑多な地形と follow カメラで構図が決まらないとき、**カメラを切り離す代わりに、実ワールド内に平らな足場を作ってそこにプレイヤーを立たせる**。実プレイヤーカメラ(Cinemachine)・アバター・HUD はそのままなので実プレイ視点を100%保てる上、背景がクリーンで被写体が見やすい。今回これで一発成功した。
1. **足場を置く**: `GameObject.CreatePrimitive(PrimitiveType.Cube)` で広い板(例 localScale=(50,3,50))を空中(例 position=(0,30,0))に。CreatePrimitive の BoxCollider をそのまま使う。無地マテリアル、layer は Default で可（Block レイヤの raycast と干渉しない）。
2. **プレイヤーを足場に乗せる**: 生 `transform.position` 代入は ThirdPersonController の重力で弾かれる。**`SetControllable(false)` で入力を止めてから `SetPlayerPosition(...)`(内部 `CharacterController.Warp`)で足場直上へワープ** → 数フレームで天面に着地・安定する（screenshot で立脚を確認）。ThirdPersonController の GroundLayers が Default を含むので接地判定も通る。
3. **対象を足場の上に設置**し、プレイヤーの前方に並べる。足場という安定した立脚点があると follow カメラの構図が決まりやすく、`StartTweenCamera(pitch 浅め, distance 中庸)` を寄せるだけでアバター・足場・対象が綺麗にフレームインする。
4. screenshot で **アバターと足場が映る**ことを確認 → de-risk probe → 録画。

### uloop の cwd（複数 Unity プロジェクト同居時）
- リポジトリ root 直下に複数の Unity プロジェクト(例: `moorestech_client` と `moorestech_server`)があると、`--project-path` を付けても "Multiple Unity projects found" 警告が stdout に混ざり、`grep`/JSON パースを汚す。
- 対象プロジェクト dir に `cd` してから `uloop` を呼ぶと出力が綺麗になる（until ループの偽陽性も減る）。

### 座標とエンティティの対応
- ブロックの「論理座標」(Vector3Int) と View 層の `BlockGameObject.OriginalPos` は同じこともあれば違うこともある (回転や footprint origin 規約で変わる)。
- View 層の API (例: SubInventory 強制起動で BlockGameObject を渡すケース) では **必ず View 層の座標を使う**。`BlockGameObjectDataStore.BlockGameObjectDictionary` を一度ダンプして、論理座標との対応を確認してから使う。

### スクリーンショットの座標系
- `--capture-mode rendering`: GameView 内部レンダー (1280×720 等のターゲット解像度)。視覚検証用。基本これ。
- 注入マウス座標(`Mouse.current.position` = 左下原点)と `Camera.WorldToScreenPoint` は同一系なので、rendering 解像度を基準に screen 座標を計算すれば一致する。
- `--capture-mode window`: Game ウィンドウ全体 (920×653 等の Editor サイズ)。GameView ツールバーが映り込むので検証用には非推奨。

### `UnityEngine.Object` vs `object`
- `using UnityEngine;` した snippet で `Object.FindFirstObjectByType<T>()` と書くと **CS0104 ambiguous reference (UnityEngine.Object と System.Object)** で失敗する。
- 必ず `UnityEngine.Object.FindFirstObjectByType<T>()` と完全修飾する。

### `BlockId` 等のプロジェクト固有 struct
- `IBlockComponent` インターフェース継承の有無で `ComponentManager.GetComponent<T>()` が CS0311 で失敗する。要求型は `IBlockComponent` を継承する具象または `IOpenableBlockInventoryComponent` のような派生インターフェースで取得し、必要なら `IOpenableInventory` 等の親インターフェースに代入する。

### Unity フォーカス
- Editor ウィンドウがフォーカスされていなくてもこのスキルは動く (uloop が Editor 内部の API を直接叩くため)。`execute-dynamic-code`・`QueueStateEvent` 注入・`screenshot --capture-mode rendering`・Recorder は全てフォーカス不要。
- **フォーカスを要求するのは OS入力系 (`simulate-keyboard`/`simulate-mouse-input`) だけで、それらは前面化して `QueueStateEvent` 注入を汚染するため、このスキルでは使わない**（「入力注入の鉄則」節）。結果としてセッション全体をフォーカス不要・無汚染で通せる。

### 動画サイズ
- 1280×720 30fps H264 で **1 分あたり ~25MB**。3-5 分の playtest で 100MB、長尺で 300MB+。
- 必要部分だけ抽出するなら `ffmpeg -ss 0 -i in.mp4 -t 30 -c copy out.mp4` (再エンコードなし) で先頭 30s 切り出し。

### 検証スクショの撮り直しが効かない
- スクリーンショットは記録用。**撮り損ねた瞬間 (アイテム搬送中の belt 上を流れる物体等) は二度と撮れない** (PlayMode は時間が進む)。
- 「複数の中間状態を撮りたい」場合は録画動画を後で見直す方が確実。スクショは「最終状態の証拠」「前後比較の節目」用と割り切る。

## Step 7: 検証と完了判定

**成功条件 (4 つ全部満たすこと)**:
1. 動画ファイルが想定サイズで生成された (`ls -lh /tmp/<name>.mp4` + `ffprobe Duration` で確認、0 byte は失敗)
2. 期待する内部 state (出力 chest の item count 等) が達成された (`execute-dynamic-code` で読み取り)
3. 検証スクショに期待する UI 要素 (item アイコン等) が映っている
4. **絵が実プレイ視点であること** — 動画/スクショに **プレイヤーアバター・地面・通常の HUD** が映り、実際にプレイヤーが見る画面になっている（カメラを孤立俯瞰に切り離した「対象だけが空に浮く」絵は、内部 state が正しくても**不合格**。「カメラframing は実プレイ視点を壊すな」節参照）。スクショを Read して目視確認し、前任/既存の正式動画があれば構図を照合する。

**失敗パターン → 切り分け**:
| 症状 | 原因候補 | 確認方法 |
|---|---|---|
| ブートが完了しない (60 秒経っても scene が target に切り替わらない) | 本番マスタ validation 失敗、Asset ロード失敗 | `uloop get-logs --log-type Error` |
| 自動搬送が始まらない | connector 不整合 (offset 計算ミス)、座標が footprint 内で重なっている | YAML の `inputConnects[].offset` / `outputConnects[].offset` を絶対座標で再計算 |
| 録画が空 / 0 byte | AppDomain key 不一致、Stop 前に PlayMode が落ちた、Recorder package 未インストール | `Packages/manifest.json` で `com.unity.recorder` 確認、Stop 時の `was recording` 戻り値が True か |

## Available scripts

- `scripts/start-recording.sh` — RecorderController を起動するための execute-dynamic-code を 1 コマンドにラップ。引数: `--project-path <unity-project> --output <path-without-ext>`。
- `scripts/stop-recording.sh` — AppDomain から RecorderController を取得して停止。引数: `--project-path <unity-project>`。

両方とも参考実装。プロジェクト固有の readiness 判定や width/height は呼び出し側で snippet を編集して使うのが正しい。
