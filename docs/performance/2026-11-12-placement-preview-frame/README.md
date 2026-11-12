# 設置プレビュー枠の GameObject 削減の前後計測（2026-11-12）

[ADR 0099](../../adr/0099-placement-bounding-box-single-collider-and-pooled-edges.md) の変更（設置プレビュー枠を、ブロックごとの当たり判定 `BlockPlacementHitFaces` 1個と、周囲20mにだけ貸す使い回しの辺に分ける）の前後で、ワールド読み込み時間・設置モードに入る瞬間・GameObject 数・定常時の fps を同じ日に続けて測った。設置モードに入る瞬間は、最初の計測（12:46〜13:20）では遷移を捉えられていなかったので、13:36〜13:58 に測り方を変えて測り直した（[設置モードに入る瞬間](#設置モードに入る瞬間測り直しrun-buildmode-entrysh実測)）。

## 結論
- **Transform は 971,661 → 428,711（-542,950、-55.9%）。** アクティブな数（315,903 → 315,905）はほぼ変わらない。減ったのはすべて非表示のプレビュー部品。
- **設置モードに入る瞬間（遷移の処理そのもの）は約1.1秒 → 約0.57秒になった（2回目以降、-49%）。** 1回目（変更後はプレハブの読み込みと枠177個の生成を含む）は 1,122 → 654ms（-42%）。設置モードから出る瞬間は 296 → 174ms。各条件3回のワールド読み込み、入る遷移は1回目3回・2回目以降12回の平均。中身はどちらもほぼ `GameObject.ActivateAwakeRecursively`（GameObject の有効化）で、変更前 約1,000ms → 変更後 約520ms。
- 設置モード中に描画される枠の数は、変更前も変更後も177個で同じ（周囲20m）。変更前は描画しない枠も含めて `BlockPreviewBoundingBox` 15,082個（と面 90,492個）を有効にしていた。変更後に残る約0.5秒は、全ブロックの当たり判定 `BlockPlacementHitFaces` 15,082個などの有効化と考えている（推測。分けて測っていない）。
- **ワールド読み込みは約7〜10秒短くなった（各条件1回の読み込みでの値）。** 変更前に読み込み中にあった「枠の生成」の1フレーム（13,177ms）が無くなった。代わりにブロック生成のフレームが約2.0秒重くなった（当たり判定をブロック生成と同じフレームで作るため）。
- 定常時の fps は 24.0 → 26.5（+10%）、`PlayerLoop` は 34.7 → 31.5ms（各条件1回の読み込みの中で3回測った平均。読み込みごとのばらつきは含まない）。なぜ速くなったかは測っていない（推測は後述）。
- 変更後に設置モードに入ると、ブロックの外に枠が177個作られ、ブロックの配下に `BlockPreviewBoundingBox` は1個も無い。`BlockPlacementHitFaces` はブロック数（15,082）と同じ数だけある。

| 指標 | 変更前 | 変更後 | 差 |
|---|---:|---:|---:|
| ワールド読み込み：Renderer 21.75万に達した時刻（開始ボタンからの秒、timeline 回） | 70.3 | 60.7 | -9.6 |
| ワールド読み込み：開始→定常（秒、注4） | 77（timeline 回）／85（subtree 回） | 70（timeline 回）／75（subtree 回） | -7／-10 |
| 最も重いフレーム（ms）と中身 | 24,436：MainGame シーン読み込み（`Internal_SceneLoaded` 8,837）＋初期応答（`RouteResponse` 8,751） | 26,399：同（`Internal_SceneLoaded` 10,528＋`RouteResponse` 8,671） | +1,963 |
| 枠の生成にあたるフレーム（ms） | 13,177（`DelayedActionManager.LateUpdate` 10,369：`Instantiate` 15,082回 3,305ms、`BlockPreviewBoundingBoxSurface` 90,498個。同じフレームに VContainer の起動 1,231 も入る） | 該当なし。ブロック生成のフレーム（上の行）が 24,436→26,399（+1,963）。VContainer の起動は次の 1,536ms のフレームに移った | ブロック生成から続く1秒超の3フレームの合計で 39,988→28,944（-11,044） |
| 設置モードに入る遷移の処理時間（ms）1回目（n=3） | 1,122<br><sub>1,102〜1,142</sub> | 654<br><sub>640〜662</sub> | -468（-42%） |
| 同 2回目以降（n=12） | 1,104<br><sub>1,048〜1,298</sub> | 566<br><sub>544〜593</sub> | -538（-49%） |
| 設置モードに入ったフレームから6フレームの超過時間（ms）1回目（n=3） | 1,396<br><sub>1,393〜1,401</sub> | 875<br><sub>858〜897</sub> | -521 |
| 同 2回目以降（n=12） | 1,380<br><sub>1,266〜1,728</sub> | 747<br><sub>702〜793</sub> | -633 |
| 設置モードから出る遷移の処理時間（ms、n=12） | 296<br><sub>281〜322</sub> | 174<br><sub>160〜195</sub> | -122（-41%） |
| 設置モード中に描画される枠（個） | 177（有効なのは 15,082） | 177（有効なのも 177） | 0 |
| Transform 総数（うち activeInHierarchy） | 971,661（315,903） | 428,711（315,905） | -542,950（+2） |
| 定常時 fps・PlayerLoop ms | 24.01・34.7（1回の読み込みで3回の平均） | 26.52・31.5（同） | +10%・-9% |

表の注記
1. 設置モードに入る1回目は、変更後はプールが空なので、プレハブの同期ロードと周囲20mぶんの枠177個の生成が入り、2回目以降は貸し出しだけになる。1回目と2回目以降を分けて読む。変更後の1回目は2回目以降より約90ms長く、その中に `Instantiate.Produce` 約20ms・`Instantiate.Copy` 約4ms・`Instantiate.Awake` 約3ms が出ている。変更前は1回目と2回目以降の差が小さい（1,122 と 1,104）。「処理時間」は遷移を起こす呼び出し（`RequestTransition` → `UIStateControl.Update`）を囲んだ Stopwatch の値、「超過時間」は遷移のフレームから6フレームぶんの、直前30フレームのフレーム時間の中央値を超えた分の合計（Editor の描画などを含む）。測り方は[設置モードに入る瞬間](#設置モードに入る瞬間測り直しrun-buildmode-entrysh実測)。
2. 「枠の生成にあたるフレーム」は変更後には対応するフレームが無い（当たり判定はブロック生成のフレーム内で作る）。変更後の欄は「該当なし」とし、代わりにブロック生成のフレームの前後差を書いた。変更前は、ブロック生成のフレーム（シーン読み込み＋初期応答）の**次の**フレームで、Addressables の完了コールバックから枠のプレハブを 15,082回 `Instantiate` していた。
3. 最初の計測（12:46〜13:20）の「設置モードに入る瞬間の最大フレーム」（変更前 386.9／290.3／349.2ms、変更後 212.6／183.5／180.3ms）は、遷移を捉えていなかったので表から外した（[旧計測](#旧計測遷移を捉えていなかった参考比較不可)）。
4. 「開始→定常」は、`load-measure.sh` が開始ボタンを押せた時点から、5秒ごとに数える Renderer 数の変化が0.1%以内になった時点までの、Mac 側の壁時計の秒（スクリプトの標準出力の `stable=1 elapsed=…s`）。5秒おき＋SSH 往復の粒度があり、この回は標準出力を保存していなかったので生データと突合できない。生データで確かめられるのは timeline の Renderer 数の時系列（[before](before-timeline/timeline-raw.txt) の末尾 78.4秒・[after](after-timeline/timeline-raw.txt) の末尾 70.9秒が、定常を判定した後に集計した時点）と、上の行の「21.75万に達した時刻」。以後は `load-measure.sh` が標準出力を出力先の `load-measure-stdout.txt` にも残す。ワールド読み込みの行は、前後とも timeline・subtree 各1回の読み込みの値で、読み込みごとのばらつきは見積もっていない。

## 計測環境
| 項目 | 値 |
|---|---|
| マシン | windows-desktop（i7-12700／RTX 3060 Ti） |
| 実行形態 | Unity Editor 6000.3.8f1 の PlayMode、「ローカルでプレイ」。コード最適化は Debug |
| 変更前 | master b6b549239b（このブランチの分岐点） |
| 変更後 | `perf/placement-preview-frame` 9057b17296（設置モードに入る瞬間の測り直しは b0a2d45cca。9057b17296 にこの記録のコミットを足しただけで、コードは同じ） |
| 外部リポジトリ | 前後とも moorestech_master 14e6a758c5、moorestech-client-private 1d6e12013f |
| ワールド | `2026-10-22-world_1`（ブロック 15,082個） |
| 計測時刻 | 変更前 12:46〜13:09、変更後 13:10〜13:20。設置モードに入る瞬間の測り直しは 変更前 13:36〜13:47、変更後 13:48〜13:58（すべて同じ日に続けて、同じ Editor プロセスで） |

### 画面が消えていると約6fpsに間引かれる
Editor を起動し直した直後の1回目の timeline は、Game ビューも Editor も前面にあった（`EditorWindow.focusedWindow` が GameView、前面ウィンドウが Editor）のに、タイトル画面から全区間が約5.8fpsに張り付き、定常まで180秒かかった。マウスを1px動かしたところ、同じタイトル画面が 5.8fps → 59.6fps に戻った。画面がすでに消えていたため、`enter-world.cs` の画面スリープ抑止（`SetThreadExecutionState`）だけでは画面が点かなかったと考えている（推測）。このため `enter-world.cs` で、開始時にマウスを1px往復させるようにした。1回目の結果は捨て、以下はすべてこの修正の後の計測。

`compare.py` は前後6回とも「表示待ち（`WaitForLastPresentation`）が記録されていない」と警告した。前後で同じ条件なので、前後比較には使えると判断した。

### Editor が起動しなかった
最初の起動では、Editor が「Recovering Scene Backups」の画面で止まり、CPU をほとんど使わないまま15分進まなかった。`Temp\__Backupscenes\0.backup`（2026-11-08 23:37、前回の計測の PlayMode 中のもの）を消して起動し直すと進んだ。

## 計測方法
| 手順 | スクリプト | 生データ |
|---|---|---|
| ワールド読み込みの時系列（Profiler なし） | `../2026-11-08-world-load-remeasure/load-measure.sh timeline` | [before](before-timeline/timeline-raw.txt)・[after](after-timeline/timeline-raw.txt) |
| 1秒超のフレームの主要な塊の内訳（Profiler あり） | `load-measure.sh subtree` | [before](before-subtree/heavy-frames-raw.txt)・[after](after-subtree/heavy-frames-raw.txt) |
| 定常時ベンチマーク（1回の読み込みで3回） | `../benchmark/run-benchmark.sh <label>` | `../benchmark/results/2026-11-12T1252_before-placement-frame_*`・`2026-11-12T1315_after-placement-frame_*` |
| Transform 内訳と GameObject 数（最初の計測） | 旧版の `run-buildmode-entry.sh`（b0a2d45cca） | [before](before-buildmode.txt)・[after](after-buildmode.txt) |
| 設置モードに入る瞬間（測り直し、各条件3回の読み込み） | `../benchmark/run-buildmode-entry.sh <label>` | `../benchmark/results/2026-11-12T13{36,40,44}_before-probe_buildmode.txt`・`2026-11-12T13{48,51,55}_after-probe_buildmode.txt` |

設置モードに入る瞬間の測り直し（`run-buildmode-entry.sh`）は、1回のワールド読み込みで、定常後に `go-breakdown.cs` と `placement-frame-count.cs` を実行し、`buildmode-probe-start.cs` で次の予定を PlayerLoop の Update フェーズの先頭に差し込む。
1. 差し込んでから200フレーム（約8秒）待つ。スニペットの実行中は Editor が止まり、その間に溜まった受信イベントが直後のフレームで処理されるので、それが落ち着くのを待つ（最初の計測の最大フレームはこれだった）。
2. 75フレームごとに、入る → 出る を4回繰り返し、最後にもう1回入る（入る遷移は1回目1回と2回目以降4回、出る遷移は4回）。遷移はキー入力を介さず、本番と同じ `UIStateControl.RequestTransition` を呼び、そのフレームのうちに `UIStateControl.Update` を反射で呼んで処理させる。この2つの呼び出しを Stopwatch で囲んだのが「処理時間」。設置対象は `GetBlockAllIds()[0]`。
3. 毎フレームの開始時刻を記録し、遷移のフレームから6フレームぶんのフレーム時間から、直前30フレームの中央値を引いた分の合計を「超過時間」とする（遷移の後のフレームに遅れて出る処理や描画も含めるため）。
4. 各遷移の3フレーム前〜10フレーム後だけ Profiler を記録し（ディーププロファイルなし）、遷移の呼び出しを `Probe.EnterTransition`／`Probe.ExitTransition` のサンプルで囲む。`buildmode-probe-collect.cs` が、そのサンプルの中の self 上位と、続く3フレームの self 上位を返す。
5. 終わったら `buildmode-probe-collect.cs` で集計して PlayerLoop から外し、設置モードのまま `placement-frame-count.cs`（有効な数と、配下の Renderer が1つでも描画中の数）を数えてから出る。

Profiler の記録中に遷移を処理しているので、処理時間には Profiler のサンプリングの負荷が少し入る（Stopwatch の値と `Probe.*` サンプルの total はほぼ一致した。例：変更後1回目 662.25ms と 662.2ms）。前後とも同じ条件。

## 結果
### GameObject 数（`go-breakdown.cs`・`placement-frame-count.cs`、実測）
| | 変更前 | 変更後 |
|---|---:|---:|
| Transform 総数 | 971,661 | 428,711 |
| うち activeInHierarchy | 315,903 | 315,905 |
| ブロック配下の GameObject | 901,548 | 358,596 |
| `MeshRenderer`（非表示を含む） | 441,538 | 260,555 |
| `Collider`（コンポーネント数） | 191,676 | 191,676 |
| `MonoBehaviour` | 375,096 | 269,523 |
| `BlockPreviewBoundingBox`（定常） | 15,082（すべてブロック配下） | 0 |
| `BlockPreviewBoundingBoxSurface`（定常） | 90,492 | 型が無い |
| `BlockPlacementHitFaces`（定常） | 型が無い | 15,082（すべてブロック配下） |
| `PreviewOnlyObject`（定常） | 31,015 | 15,933 |
| 設置モード中の `BlockPreviewBoundingBox` | 15,082（すべて表示） | 177（ブロック配下は0、すべて表示） |
| 設置モード中に表示される `PreviewOnlyObject` | 25,375 | 10,293 |

コライダーの数は変わらない。変更前は面1枚ごとに GameObject が1個あり、変更後は1個の GameObject に `BoxCollider` を6枚付けるため。

### 変更後に出たエラー（`uloop get-logs --log-type Error`、最初の計測の `run-buildmode-entry.sh after-placement-frame` の直後）
- `NullReferenceException` は0件。枠・プール・当たり判定（`BlockPreviewEdgeFramePool`・`BlockPreviewEdgeFrameLease`・`BlockPlacementHitFaces`）に関わるエラーも0件。ベースラインワールドのブロックはハンドシェイク時に作られるので、前回のレビューで直した「ハンドシェイク時に作られるブロックで借り口のプールが null」は、この環境では起きなかった。設置モード中に枠が177個貸し出されていることも上の表で確かめた。
- 出ていたのは次の5件。
  - `MissingReferenceException: ElectricWireLineView has been destroyed`（4件）：`ConnectionLineViewBase.UpdateConnectionLines` → `ElectricWireStateChangeProcessor.OnChangeState` から。電線の表示で、このブランチは触っていない。変更前でも出るかは確かめていない。
  - `[Steamworks.NET] SteamAPI_Init() failed`（1件）：Editor では Steam が動いていないため。

### ワールド読み込み（timeline・subtree、実測）
| 時刻（開始ボタンからの経過秒） | 変更前 | 変更後 |
|---|---|---|
| ブロックアイコンの撮影開始 | 15.3 | 14.8 |
| シーン読み込み＋初期応答の1フレーム | 26.7 → 49.9 | 26.4 → 52.2 |
| 枠の生成の1フレーム | 49.9 → 61.5 | なし（52.2 → 54.6 の間隔は VContainer の起動の 1,536ms のフレームなど） |
| Renderer 21.75万に達する | 70.3 | 60.7 |

1秒超のフレーム（subtree）

| フレーム | 変更前 | 変更後 |
|---|---:|---:|
| サーバー起動（`ServerStarter.Start`） | 13,240 | 13,365 |
| 中身の分からないフレーム（主要な塊がどれも 500ms 未満） | 4,225 | 4,655 |
| シーン読み込み＋初期応答（ブロック生成） | 24,436 | 26,399 |
| 枠の生成（`DelayedActionManager.LateUpdate` 10,369）＋ VContainer の起動（1,231） | 13,177 | — |
| VContainer の起動（836） | — | 1,536 |
| その次 | 2,375 | 1,009 |

ブロック生成のフレームで増えた分（変更後、`Internal_SceneLoaded` 配下）
- `GameObject.AddComponent` 110,469回（821ms）→ 246,207回（1,613ms）。増えた 135,738回はブロック 15,082個 × 9 にあたる（当たり判定の GameObject 1個に `BlockPlacementHitFaces` と `BoxCollider` 6枚を付けるので7回は説明がつく。残り2回の内訳は確かめていない）。
- `Instantiate` の回数は 20,133回で変わらない。

### 定常時（`run-benchmark.sh`、実測）
`compare.py` の出力（抜粋）。

| 指標 | before-placement-frame（3回）<br>b6b549239b | after-placement-frame（3回）<br>9057b17296 |
|---|---:|---:|
| fps（Profiler なし） | 24.01<br><sub>23.63〜24.44</sub> | 26.52<br><sub>26.33〜26.65</sub> (+10%✅) |
| フレーム ms 平均（Profiler なし） | 41.6<br><sub>40.9〜42.3</sub> | 37.7<br><sub>37.5〜38.0</sub> (-9%✅) |
| フレーム ms p95（Profiler） | 60.5<br><sub>56.3〜65.9</sub> | 53.4<br><sub>51.5〜57.1</sub> (-12%) |
| 受信処理 ms/フレーム（応答を除く） | 3.6<br><sub>3.3〜4.1</sub> | 3.5<br><sub>3.2〜4.2</sub> (-2%) |
| ブロック状態 件/秒 | 4,173<br><sub>3,809〜4,501</sub> | 4,092<br><sub>4,040〜4,148</sub> (-2%) |
| `PlayerLoop` ms | 34.7<br><sub>34.0〜35.6</sub> | 31.5<br><sub>30.8〜32.6</sub> (-9%✅) |
| `RenderPlayModeViewCameras` ms | 10.4<br><sub>10.3〜10.6</sub> | 10.2<br><sub>10.1〜10.3</sub> (-2%✅) |
| `EditorLoop` ms | 10.0<br><sub>9.0〜11.7</sub> | 9.4<br><sub>9.1〜9.9</sub> (-6%) |
| `GC.Alloc` 回/フレーム | 5,366<br><sub>5,210〜5,532</sub> | 4,933<br><sub>4,845〜5,073</sub> (-8%✅) |

### 設置モードに入る瞬間（測り直し、`run-buildmode-entry.sh`、実測）
遷移の処理時間（ms、Stopwatch）。読み込みごとに、入る（1回目）・出る・入る・出る・入る・出る・入る・出る・入る の順。

| 読み込み | 変更前：入る | 変更前：出る | 変更後：入る | 変更後：出る |
|---|---|---|---|---|
| 1 | **1,123**・1,048・1,050・1,061・1,104 | 318・288・292・293 | **662**・570・557・547・564 | 167・178・166・167 |
| 2 | **1,142**・1,056・1,051・1,298・1,131 | 281・305・293・322 | **659**・579・564・590・544 | 170・160・192・195 |
| 3 | **1,102**・1,204・1,087・1,092・1,066 | 288・296・294・286 | **640**・593・564・560・561 | 176・177・173・170 |

太字が各読み込みの1回目。超過時間（遷移のフレームから6フレーム）は、入る1回目 変更前 1,393〜1,401 → 変更後 858〜897ms、入る2回目以降 1,266〜1,728 → 702〜793ms、出る 377〜480 → 219〜387ms。

遷移の中身（Profiler、`Probe.*Transition` の中の self）
| | 変更前 | 変更後 |
|---|---:|---:|
| 入る：`GameObject.ActivateAwakeRecursively`（15回の範囲・平均） | 962〜1,046・999 | 486〜561・516 |
| 入る：`SortingGroup.UpdateRenderer` | 約12ms | 約3.6ms |
| 入る1回目だけ：`Instantiate.Produce`／`Instantiate.Copy`／`Instantiate.Awake` | — | 約20／約4.5／約3ms |
| 出る：`GameObject.ActivateAwakeRecursively`（12回の範囲・平均） | 223〜263・237 | 120〜153・130 |

- 遷移の直後の1フレームは、変更前 63〜146ms、変更後 41〜85ms と直前（約35〜41ms）より重く、2フレーム目以降はおおむね直前と同じくらいに戻る。直後のフレームの上位は通常の `UpdateFunction.Invoke`・`EditorLoop`・PhysX で、遷移に固有の大きな処理は見つからなかった。
- 設置モード中の数（`placement-frame-count.cs`、各条件3回とも同じ値）

| | 変更前 | 変更後 |
|---|---:|---:|
| `BlockPreviewBoundingBox` 有効 | 15,082 | 177 |
| `BlockPreviewBoundingBox` 描画中（配下の Renderer が1つでも enabled） | 177 | 177 |
| `BlockPreviewBoundingBoxSurface` 有効 | 90,492 | 型が無い |
| `BlockPlacementHitFaces` 有効 | 型が無い | 15,082 |
| `PreviewOnlyObject` 有効／描画中 | 25,371／349 | 10,289／172 |

  レビューで「変更前は設置モードで枠 15,082 個が有効になる」と指摘された点は、実測でもその通りだった。ただし描画されるのはプレイヤーの周囲20mの177個だけで（[ADR 0083](../../adr/0083-build-mode-preview-only-objects-player-radius.md) の半径。半径の外は有効のまま Renderer だけ切る）、描画される数は前後で同じ。変更で減ったのは「描画しないのに有効にする」枠と面の数。

### 旧計測（遷移を捉えていなかった。参考・比較不可）
最初の計測（12:46〜13:20、[before](before-buildmode.txt)・[after](after-buildmode.txt)）は、Profiler を始めるスニペットの中で遷移を要求し、約3秒後に止めて最大フレームなどを見た。

| 回 | 変更前：最大／合計（フレーム数）／`UIStateControl.Update` 最大 | 変更後：同 | 差 |
|---|---|---|---|
| 1回目 | 386.9／2,131.6（29）／16.2ms | 212.6／2,721.9（53）／15.8ms | — |
| 2回目 | 290.3／1,877.6（31）／0.5ms | 183.5／2,339.0（46）／0.6ms | — |
| 3回目 | 349.2／1,954.7（33）／0.7ms | 180.3／2,845.2（59）／0.6ms | — |

- 測り直しで遷移の処理だけで変更前 約1,100ms・変更後 約570ms かかると分かったので、記録の中に遷移の処理は入っていなかった。どのフレームで処理されたかは確かめていない（推測：遷移を処理したフレームが Profiler の記録の始まる前か、記録の先頭の欠けたフレームに入った）。`UIStateControl.Update` の最大は遷移ではない通常のフレームから選ばれたものと考えている。
- 最大フレームは6回とも記録の先頭のフレームで、self の上位は `BlockState.Deserialize` と各ブロック状態のプロセッサ（スニペットの実行で Editor が止まっていた間に溜まった受信イベントの処理と推測）。変更とは関係の無い量で決まるので、前後の差は取らない。
- 記録したフレーム数が前後で違う（29〜33 と 46〜59）のは、窓の壁時計の長さ（sleep 3秒＋SSH 往復）はほぼ同じでも、記録に入ったフレーム時間の合計が 1.9〜2.1秒と 2.3〜2.8秒で違うため。残りの時間は記録に入っていない。変更前の方が記録から漏れた時間が長いのは、遷移の処理（変更前の方が長い）が漏れた側にあったためと推測している。

## 推測（未確認）
- 定常時の fps が上がった（+10%）理由は分けて測っていない。非表示の GameObject が約54万個・非表示の `MeshRenderer` が約18万個減ったことで、Transform の階層や Renderer の管理にかかる毎フレームの処理が軽くなった可能性がある（コライダーの数は変わっていない）。前後で別々のワールド読み込みなので、読み込みごとのばらつきも混ざっている。
- 設置モードに入る遷移で変更後に残る約520msの `ActivateAwakeRecursively` は、全ブロックの `BlockPlacementHitFaces` 15,082個（各 `BoxCollider` 6枚）と、枠以外の `PreviewOnlyObject` 約1万個の有効化と考えている。どちらがどれだけかは分けて測っていない。変更前は、これに加えて枠 15,082個と面 90,492個を有効にしていた。
- 変更前の入る遷移が1回目と2回目以降でほぼ同じ（1,122 と 1,104）なのは、変更前は読み込み中に枠を全部作り終えていて、1回目に作る物が無いため。変更後の1回目の約90ms増は、プレハブの読み込みと枠177個の `Instantiate`（約28ms）と、それ以外（`Mono.JIT` 約7ms など、1回目だけのもの）と考えている。
- ワールド読み込みの短縮は、timeline 回で7秒、subtree 回で10秒、Renderer が揃う時刻で9.6秒と、測り方で幅がある。「枠の生成の1フレーム（13.2秒）が無くなり、ブロック生成のフレームが2.0秒増えた」差し引き約11秒が上限の目安。

## 計測の仕組みの変更
- `benchmark/snippets/enter-world.cs`：画面が消えていても点くよう、開始時にマウスを1px往復させる。
- `2026-11-08-world-load-remeasure/load-measure.sh`：スニペットの送り元を、メインのチェックアウトの絶対パスから、スクリプトと同じチェックアウトからの相対パスに変えた（別 worktree から実行すると古いスニペットが送られていたため）。
- `benchmark/run-buildmode-entry.sh` と `placement-frame-count.cs` を足した。`go-breakdown.cs` の結果は SSH 越しの `Get-Content` だと日本語のブロック名が化けるので、ファイルのまま `scp` で取り込む（取れなければ結果ファイルに ERROR を残す）。途中で失敗したら（定常にならない・開始できない場合も）作業ワールドの差し替えを解除して PlayMode を止める。
- 設置モードに入る瞬間は、最初は `buildmode-enter.cs`・`buildmode-frames-collect.cs`・`profiler-stop.cs` で測ったが、遷移を捉えていなかったので、`buildmode-probe-start.cs`・`buildmode-probe-collect.cs`（PlayerLoop の中で遷移を起こして Stopwatch で測る）に置き換え、旧スニペットは消した（旧版は b0a2d45cca にある）。`placement-frame-count.cs` は描画中の数（`rendered`）も返す。
- `2026-11-08-world-load-remeasure/load-measure.sh`：「開始→定常」の秒を生データと突合できるよう、標準出力を出力先の `load-measure-stdout.txt` にも残す。
