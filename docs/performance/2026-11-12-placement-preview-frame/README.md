# 設置プレビュー枠の GameObject 削減の前後計測（2026-11-12）

[ADR 0099](../../adr/0099-placement-bounding-box-single-collider-and-pooled-edges.md) の変更（設置プレビュー枠を、ブロックごとの当たり判定 `BlockPlacementHitFaces` 1個と、周囲20mにだけ貸す使い回しの辺に分ける）の前後で、ワールド読み込み時間・設置モードに入る瞬間・GameObject 数・定常時の fps を同じ日に続けて測った。

## 結論
- **Transform は 971,609 → 428,711（-542,898、-56%）。** アクティブな数（約31.6万）は変わらない。減ったのはすべて非表示のプレビュー部品。
- **ワールド読み込みは約7〜10秒短くなった。** 変更前に読み込み中にあった「枠の生成」の1フレーム（13,177ms）が無くなった。代わりにブロック生成のフレームが約2.0秒重くなった（当たり判定をブロック生成と同じフレームで作るため）。
- 定常時の fps は 24.0 → 26.5（+10%）、`PlayerLoop` は 34.7 → 31.5ms。なぜ速くなったかは測っていない（推測は後述）。
- 設置モードに入る瞬間の大きなフレームは、変更前も変更後も、遷移処理ではなく溜まった受信イベントの処理で決まっていた。遷移そのもの（`UIStateControl.Update`）は前後とも1回目が約16ms、2回目以降が1ms未満。
- 変更後に設置モードに入ると、ブロックの外に枠が177個作られ、ブロックの配下に `BlockPreviewBoundingBox` は1個も無い。`BlockPlacementHitFaces` はブロック数（15,082）と同じ数だけある。

| 指標 | 変更前 | 変更後 | 差 |
|---|---:|---:|---:|
| ワールド読み込み（開始→定常、秒） | 77（timeline 回）／85（subtree 回）<br>Renderer 21.75万に達した時刻 70.3 | 70（timeline 回）／75（subtree 回）<br>同 60.7 | -7／-10<br>-9.6 |
| 最も重いフレーム（ms）と中身 | 24,436：MainGame シーン読み込み（`Internal_SceneLoaded` 8,837）＋初期応答（`RouteResponse` 8,751） | 26,399：同（`Internal_SceneLoaded` 10,528＋`RouteResponse` 8,671） | +1,963 |
| 枠の生成にあたるフレーム（ms） | 13,177（`DelayedActionManager.LateUpdate` 10,369：`Instantiate` 15,082回 3,305ms、`BlockPreviewBoundingBoxSurface` 90,498個。同じフレームに VContainer の起動 1,231 も入る） | 該当なし。ブロック生成のフレーム（上の行）が 24,436→26,399（+1,963）。VContainer の起動は次の 1,536ms のフレームに移った | ブロック生成から続く1秒超の3フレームの合計で 39,988→28,944（-11,044） |
| 設置モードに入る瞬間の最大フレーム（ms）1回目 | 386.9 | 212.6 | -174.3 |
| 同 2回目 | 290.3 | 183.5 | -106.8 |
| 同 3回目 | 349.2 | 180.3 | -168.9 |
| 設置モードに入る瞬間の合計（ms）1回目・2回目・3回目 | 2,131.6（29フレーム）・1,877.6（31）・1,954.7（33） | 2,721.9（53）・2,339.0（46）・2,845.2（59） | 記録したフレーム数が違うので、1フレーム平均で比べる：73.5・60.6・59.2 → 51.4・50.8・48.2 |
| Transform 総数（うち activeInHierarchy） | 971,609（315,851） | 428,711（315,905） | -542,898（+54） |
| 定常時 fps・PlayerLoop ms | 24.01・34.7（3回平均） | 26.52・31.5（3回平均） | +10%・-9% |

表の注記
1. 設置モードに入る1回目は、変更後はプールが空なので、プレハブの同期ロードと周囲20mぶんの枠の生成が入り、2回目以降は貸し出しだけになる。1回目と2回目以降を分けて読む。今回、変更後の1回目は最大フレーム 212.6ms と2回目以降（約180ms）よりやや大きいが、その中身の上位は受信イベントの処理で、枠の生成は上位15件に出ていない（[after-buildmode.txt](after-buildmode.txt)）。
2. 「枠の生成にあたるフレーム」は変更後には対応するフレームが無い（当たり判定はブロック生成のフレーム内で作る）。変更後の欄は「該当なし」とし、代わりにブロック生成のフレームの前後差を書いた。変更前は、ブロック生成のフレーム（シーン読み込み＋初期応答）の**次の**フレームで、Addressables の完了コールバックから枠のプレハブを 15,082回 `Instantiate` していた。
3. 「設置モードに入る瞬間」の記録は、遷移要求のスニペットを実行してから約5秒（SSH 往復を含む）の Profiler の記録。窓の長さが毎回少しずつ違うので、合計 ms はそのままでは比べられない。最大フレームは毎回、記録の先頭のフレーム（スニペットの実行で Editor が止まっていた間に溜まった受信イベントを処理するフレームと推測）だった。

## 計測環境
| 項目 | 値 |
|---|---|
| マシン | windows-desktop（i7-12700／RTX 3060 Ti） |
| 実行形態 | Unity Editor 6000.3.8f1 の PlayMode、「ローカルでプレイ」。コード最適化は Debug |
| 変更前 | master b6b549239b（このブランチの分岐点） |
| 変更後 | `perf/placement-preview-frame` 9057b17296 |
| 外部リポジトリ | 前後とも moorestech_master 14e6a758c5、moorestech-client-private 1d6e12013f |
| ワールド | `2026-10-22-world_1`（ブロック 15,082個） |
| 計測時刻 | 変更前 12:46〜13:09、変更後 13:10〜13:20（同じ日に続けて、同じ Editor プロセスで） |

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
| Transform 内訳と、設置モードの出入り3回 | `../benchmark/run-buildmode-entry.sh <label>` | [before](before-buildmode.txt)・[after](after-buildmode.txt) |

`run-buildmode-entry.sh` は、定常後に `go-breakdown.cs`（Transform 内訳）と `placement-frame-count.cs`（枠・当たり判定の数をブロック配下とそれ以外に分けて数える）を実行し、その後、次を3回繰り返す。
1. `buildmode-enter.cs`：Profiler の記録を始めてから、キー入力を介さず `UIStateControl.RequestTransition(PlaceBlock)` で設置モードに入る（設置対象は `GetBlockAllIds()[0]`）
2. 3秒待って `profiler-stop.cs`、さらに2秒待って `buildmode-frames-collect.cs`：記録したフレームの最大・合計・一覧、最大フレームの self 上位15件、`UIStateControl.Update` の total が最大のフレーム（遷移を処理したフレーム）とその self 上位10件
3. `placement-frame-count.cs` → `buildmode-exit.cs`、3秒待つ

記録を止めたのと同じスニペットの中では記録が読めない（`firstFrameIndex` が -1 のまま）ことがあったので、止めるスニペットと集計するスニペットを分けた。

## 結果
### GameObject 数（`go-breakdown.cs`・`placement-frame-count.cs`、実測）
| | 変更前 | 変更後 |
|---|---:|---:|
| Transform 総数 | 971,609 | 428,711 |
| うち activeInHierarchy | 315,851 | 315,905 |
| ブロック配下の GameObject | 901,548 | 358,596 |
| `MeshRenderer`（非表示を含む） | 441,512 | 260,555 |
| `Collider`（コンポーネント数） | 191,676 | 191,676 |
| `MonoBehaviour` | 375,070 | 269,523 |
| `BlockPreviewBoundingBox`（定常） | 15,082（すべてブロック配下） | 0 |
| `BlockPreviewBoundingBoxSurface`（定常） | 90,492 | 型が無い |
| `BlockPlacementHitFaces`（定常） | 型が無い | 15,082（すべてブロック配下） |
| `PreviewOnlyObject`（定常） | 31,015 | 15,933 |
| 設置モード中の `BlockPreviewBoundingBox` | 15,082（すべて表示） | 177（ブロック配下は0、すべて表示） |
| 設置モード中に表示される `PreviewOnlyObject` | 25,375 | 10,293 |

コライダーの数は変わらない。変更前は面1枚ごとに GameObject が1個あり、変更後は1個の GameObject に `BoxCollider` を6枚付けるため。

### 変更後に出たエラー（`uloop get-logs --log-type Error`、`run-buildmode-entry.sh after-placement-frame` の直後）
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

### 設置モードに入る瞬間（`run-buildmode-entry.sh`、実測）
| 回 | 変更前：最大／合計（フレーム数）／遷移の `UIStateControl.Update` | 変更後：同 |
|---|---|---|
| 1回目 | 386.9／2,131.6（29）／16.2ms | 212.6／2,721.9（53）／15.8ms |
| 2回目 | 290.3／1,877.6（31）／0.5ms | 183.5／2,339.0（46）／0.6ms |
| 3回目 | 349.2／1,954.7（33）／0.7ms | 180.3／2,845.2（59）／0.6ms |

- 最大フレームは6回とも記録の先頭のフレームで、self の上位は `BlockState.Deserialize`（変更前 82〜86ms、変更後 45〜49ms）と各ブロック状態のプロセッサだった。
- 2回目以降の遷移のフレームは、前後とも約40ms（定常のフレームと同じくらい）。

## 推測（未確認）
- 定常時の fps が上がった（+10%）理由は分けて測っていない。非表示の GameObject が約54万個・非表示の `MeshRenderer` が約18万個減ったことで、Transform の階層や Renderer の管理にかかる毎フレームの処理が軽くなった可能性がある（コライダーの数は変わっていない）。前後で別々のワールド読み込みなので、読み込みごとのばらつきも混ざっている。
- 設置モードに入る瞬間の最大フレームが小さくなったのは、遷移の処理ではなく、記録の先頭で処理した受信イベントの量（`BlockState.Deserialize` が約半分）の違いによる。変更による差かどうかは分からない。1フレーム平均（約60〜74ms → 約48〜51ms）が下がったのは、定常時の fps が上がったのと同じ傾向。
- 変更後の1回目に入るはずのプレハブの同期ロードと枠177個の生成は、記録の中で目立つフレームとして見つからなかった（1回目の3フレーム目が 129ms で、2回目以降には無い大きさだが、中身は確かめていない）。
- ワールド読み込みの短縮は、timeline 回で7秒、subtree 回で10秒、Renderer が揃う時刻で9.6秒と、測り方で幅がある。「枠の生成の1フレーム（13.2秒）が無くなり、ブロック生成のフレームが2.0秒増えた」差し引き約11秒が上限の目安。

## 計測の仕組みの変更
- `benchmark/snippets/enter-world.cs`：画面が消えていても点くよう、開始時にマウスを1px往復させる。
- `2026-11-08-world-load-remeasure/load-measure.sh`：スニペットの送り元を、メインのチェックアウトの絶対パスから、スクリプトと同じチェックアウトからの相対パスに変えた（別 worktree から実行すると古いスニペットが送られていたため）。
- `benchmark/run-buildmode-entry.sh` と `buildmode-*.cs`・`profiler-stop.cs`・`placement-frame-count.cs` を足した。`go-breakdown.cs` の結果は SSH 越しの `Get-Content` だと日本語のブロック名が化けるので、ファイルのまま `scp` で取り込む。途中で失敗したら作業ワールドの差し替えを解除して PlayMode を止める。
