---
name: unity-playmode-recorded-playtest
description: 'Unity Editor を PlayMode 起動し、uloop と Unity Recorder で end-to-end gameplay を録画付き検証する枠組み。Use When: 「Unity をコードで動かして録画したい」「フォーカス無しで PlayMode テスト」「Recorder を CLI 制御」「実プレイで動くか確認」「MonoBehaviour Update を回した状態で API 叩いて検証」「ロジック単体テストでは捕まらないシナリオを通しで確認」と言われた場合。フォーカス不要が必須要件のとき積極的に起動する。legacy Input・本番マスタ validation・AppDomain 持ち越し等の落とし穴は Body 参照。'
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

### 録画は Unity Recorder 一択
macOS の `screencapture -V` や `ffmpeg -f avfoundation` を使うアプローチも技術的には可能だが、**OS 越し画面キャプチャはフォーカス・解像度・他ウィンドウ重なりに脆弱**。Unity Recorder は GameView レンダリング結果を内部で直接受け取るので、Unity ウィンドウが他アプリの裏に隠れていても録画品質が変わらない。「フォーカス不要」が要件のとき OS 録画は選択肢から外す。

### 座標設計は別エージェントに事前検討させる
複数ブロックを連結するシナリオ (例: ベルト連鎖、回路) では **Plan サブエージェントに「絶対座標で input/output offset が一致するか」を表で出させてから実装**する。手書きで座標を決めると connector 不一致で「設置は成功するが繋がらない」状態になる。**connector の YAML データを実際に grep で読み**、各ブロックの `inputConnects[].offset` / `outputConnects[].offset` を「OriginalPos + offset = 絶対座標」で計算した表が信頼できる証拠。

### 検証は内部状態の dump ではなく UI を開いて視覚確認する
`WorldBlockDatastore.GetItem(slot)` で数値を返すだけだと「データレイヤは正しいが UI Prefab 配線が間違っている」バグを見落とす。**SubInventory state を強制起動して `screenshot --capture-mode rendering` で撮る**。プレイヤーが実際に見る画面に item アイコンが表示されることまで含めて確認する。内部 dump はサニティチェック用の補助。

### 「プレイヤー側に近い」優先順位
プロジェクトに UI controller 層 (例: `LocalPlayerInventoryController`) があるなら、ネットワークプロトコル層 (`VanillaApi.SendOnly` 等) より上の controller 層を優先して叩く。理由: controller 層は「local UI 状態の更新 + サーバ送信」を 1 コールでやるので、UI と data の整合性まで確認できる。サーバ送信だけだと UI 更新パスが回らずバグを見落とす。

## 全体フロー

```
1. (前提) Unity 起動、CLI Loop サーバ起動済み
2. uloop control-play-mode --action Play     # PlayMode 突入
3. shell sleep 15-25 + readiness polling      # ブート待機
4. uloop execute-dynamic-code で Recorder 起動 (AppDomain に保持)
5. シナリオ実行ループ (Step 5 a/b/c を組み合わせる)
   a. API 叩き                       # 設置・データ操作
   b. simulate-keyboard / mouse      # 本物のキー入力 (新 InputSystem 配下のみ)
   c. リフレクションで状態強制遷移   # legacy Input にしか届かない遷移を代替
6. 待機は shell sleep で              # Thread.Sleep は禁止 (PlayMode 凍る)
7. uloop screenshot --capture-mode rendering  # 視覚検証
8. uloop execute-dynamic-code で Recorder 停止
9. uloop control-play-mode --action Stop      # PlayMode 終了
10. 動画ファイル確認 (`/tmp/<name>.mp4`)
```

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

## Step 2: Unity Recorder の起動

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

## Step 3: シナリオ実行 - API/キー/リフレクションの使い分け

| プレイヤー操作 | 推奨手段 | 理由 |
|---|---|---|
| データ準備（インベントリ初期化、座標テレポート、世界配置）| **API 直叩き** (`execute-dynamic-code`) | プレイヤーが採掘・歩行する時間を省略。フォーカス不要 |
| 新 Input System に bind されたキー (E/数字/Tab 等が一般的) | `simulate-keyboard --action Press --key <KeyName>` | 本物のキー入力 → 入力ハンドラ → ゲームロジックを通る |
| マウスクリック (UI クリック・ワールドクリック) | `simulate-mouse-input --action Click --x N --y N` | 本物のレイキャスト経路を通る (cursor 座標は要事前検証) |
| **legacy `UnityEngine.Input.GetKeyDown(KeyCode.X)` でしか拾えないキー** | **リフレクションで状態強制遷移** | simulate-keyboard は new Input System 経由なので legacy には届かない |
| プロジェクト固有 UI window の強制起動 (例: SubInventory) | リフレクションで `UIStateControl.CurrentState` を直接書き換える | クリックでしか開けない UI を programmatic に開ける |

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

PlayMode 内で時間を進めるには `shell sleep` を使う。**execute-dynamic-code 内で `Thread.Sleep` を呼ぶと PlayMode の Update が止まる** (main thread を握ってしまうため)。

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

### simulate-keyboard と legacy Input
- **`simulate-keyboard --key B` は新 Input System に届くが `UnityEngine.Input.GetKeyDown(KeyCode.B)` には届かない**。プロジェクトのコードベースで `Input.GetKeyDown` を grep して、自分が叩こうとしているキーが legacy 側で読まれていないか確認すること。混在プロジェクト (Unity 6 で旧 Input System 残存) で頻発。
- `InputManager.UI.OpenInventory.GetKeyDown` のような新 Input System 配下のものは simulate-keyboard で OK。

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

### 座標とエンティティの対応
- ブロックの「論理座標」(Vector3Int) と View 層の `BlockGameObject.OriginalPos` は同じこともあれば違うこともある (回転や footprint origin 規約で変わる)。
- View 層の API (例: SubInventory 強制起動で BlockGameObject を渡すケース) では **必ず View 層の座標を使う**。`BlockGameObjectDataStore.BlockGameObjectDictionary` を一度ダンプして、論理座標との対応を確認してから使う。

### スクリーンショットの座標系
- `--capture-mode window`: Game ウィンドウ全体 (920×653 等の Editor サイズ)。`simulate-mouse-input` の座標系と一致する場合と一致しない場合がある (DPI スケーリング依存)。
- `--capture-mode rendering`: GameView 内部レンダー (1280×720 等のターゲット解像度)。視覚検証用。

### `UnityEngine.Object` vs `object`
- `using UnityEngine;` した snippet で `Object.FindFirstObjectByType<T>()` と書くと **CS0104 ambiguous reference (UnityEngine.Object と System.Object)** で失敗する。
- 必ず `UnityEngine.Object.FindFirstObjectByType<T>()` と完全修飾する。

### `BlockId` 等のプロジェクト固有 struct
- `IBlockComponent` インターフェース継承の有無で `ComponentManager.GetComponent<T>()` が CS0311 で失敗する。要求型は `IBlockComponent` を継承する具象または `IOpenableBlockInventoryComponent` のような派生インターフェースで取得し、必要なら `IOpenableInventory` 等の親インターフェースに代入する。

### Unity フォーカス
- Editor ウィンドウがフォーカスされていなくてもこのスキルは動く (uloop が Editor 内部の API を直接叩くため)。
- 例外: `simulate-keyboard --key X` だけは OS 経由でキー入力をシミュレートするので、**Editor ウィンドウがフォアグラウンドにある必要がある**。フォーカス無しで動かしたいシナリオでは simulate-keyboard を避け、API 直叩き or リフレクション遷移に置換する。

### 動画サイズ
- 1280×720 30fps H264 で **1 分あたり ~25MB**。3-5 分の playtest で 100MB、長尺で 300MB+。
- 必要部分だけ抽出するなら `ffmpeg -ss 0 -i in.mp4 -t 30 -c copy out.mp4` (再エンコードなし) で先頭 30s 切り出し。

### 検証スクショの撮り直しが効かない
- スクリーンショットは記録用。**撮り損ねた瞬間 (アイテム搬送中の belt 上を流れる物体等) は二度と撮れない** (PlayMode は時間が進む)。
- 「複数の中間状態を撮りたい」場合は録画動画を後で見直す方が確実。スクショは「最終状態の証拠」「前後比較の節目」用と割り切る。

## Step 7: 検証と完了判定

**成功条件 (3 つ全部満たすこと)**:
1. 動画ファイルが想定サイズで生成された (`ls -lh /tmp/<name>.mp4` + `ffprobe Duration` で確認、0 byte は失敗)
2. 期待する内部 state (出力 chest の item count 等) が達成された (`execute-dynamic-code` で読み取り)
3. 検証スクショに期待する UI 要素 (item アイコン等) が映っている

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
