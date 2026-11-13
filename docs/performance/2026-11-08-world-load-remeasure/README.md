# ワールド読み込み時間の再計測（2026-11-08）

GameObject 削減に取りかかる前に、[2026-10-22 の調査](../2026-10-22-world-load-time/README.md)の後で入った変更（パイプの差分同期 ADR 0088・ブロック状態の束ね ADR 0091）による読み込み時間の変化を測り直した。

## 結論
- ベースラインワールド（`2026-10-22-world_1`）を「ローカルでプレイ」で開いてから定常状態になるまで、**約76秒**かかった（前回は約110秒）。
- 前回の約44秒（溜まった受信イベントの後処理）は、ほぼ消えた。読み込み中に受信イベントの処理が目立つフレームは、合わせて約2.4秒だけだった。
- 今、止まっているのは次の大きな塊。
  | 塊 | 秒 | 中身 |
  |---|---:|---|
  | サーバー起動（セーブ読み込み） | 約11〜12.5 | `ServerStarter.Start` の1フレーム。self が11.2秒で、マーカーが無いので中身は分からない。GC.Alloc が約1,549万回 |
  | MainGame シーン読み込み＋初期応答の処理（1フレーム） | 約24〜25 | シーン読み込み後の初期化 9.2秒、初期応答の処理（`RouteResponse`）8.6秒、CEF の起動 3.7秒 |
  | ブロックの GameObject 生成（1フレーム） | 約13〜14 | `Instantiate` 15,082回で 3.3秒。残りの self 7.2秒は生成後の初期化で、マーカーが無い |
  | ブロックアイコンの撮影 | 約5.5 | 87個＋列車3個 |
- **GameObject 削減で直接縮むのは、ブロック生成の塊（約13秒）と、シーン読み込み中の `Instantiate`（20,133回で 3.1秒）。** ブロック生成の1フレームで `BlockPreviewBoundingBoxSurface` を 90,498個、`PreviewOnlyObject` を 15,083個作っていた。
- 削減の前後比較に使う定常時のベンチマークは、[benchmark の結果一覧](../benchmark/README.md#結果一覧)の `before-gameobject-reduction` に記録した。

## 計測環境
| 項目 | 値 |
|---|---|
| マシン | windows-desktop（i7-12700／RTX 3060 Ti） |
| 実行形態 | Unity Editor 6000.3.8f1 の PlayMode、「ローカルでプレイ」。コード最適化は Debug |
| コミット | master b6b549239b（moorestech_master 14e6a758c5、moorestech-client-private 1d6e12013f） |
| ワールド | `2026-10-22-world_1`（ロード時に版6→版11へマイグレーションされる。所要約1.4秒） |

### Game ビューが隠れていると約6fpsに間引かれる
1回目の計測では、タイトル画面・初期化シーンを含む全区間が約5.7fpsに張り付き、定常まで約178秒かかった。Game ビューが Scene ビューの裏のタブにあり、Editor ウィンドウも前面ではなかった。Game ビューを前に出し、Editor を前面にしたところ、タイトル画面が 5.8fps から 59.6fps に戻った（同じコード・同じ画面で実測）。このため `benchmark/snippets/enter-world.cs` で、開始時に Game ビューと Editor ウィンドウを前に出すようにした。1回目の結果は捨て、以下はすべて前に出した状態の計測。

## 計測方法
`load-measure.sh <mode> <出力先>` で、1回のワールド読み込みにつき1つの mode を測った（計3回読み込んだ）。

| mode | 内容 | 生データ |
|---|---|---|
| `timeline` | 開始ボタンの直前から、全ログの時刻と2秒ごとの fps・Renderer 数を記録（`../2026-10-22-world-load-time/tl-start.cs`・`tl-collect.cs`）。Profiler なし | [timeline-raw.txt](timeline-raw.txt) |
| `heavy` | Profiler を開始ボタンの直前から定常まで記録し、1秒超のフレームの上位階層を書き出す（`../2026-10-22-world-load-time/heavy-collect.cs`） | [heavy-frames-raw.txt](heavy-frames-raw.txt) |
| `subtree` | 同じく Profiler で、主要な塊（サーバー起動・シーン読み込み・初期応答・ブロック生成・VContainer）の配下を名前ごとの self 時間で合算する（`subtree-collect.cs`） | [heavy-subtree-raw.txt](heavy-subtree-raw.txt) |

3回は別々の読み込みなので、秒数は1〜2秒ずれる。

## タイムライン（開始ボタンからの経過秒、`timeline`）
| 時刻 | 何が起きているか |
|---|---|
| 0〜3.8秒 | タイトル → 初期化シーン、WebUI ホストの起動 |
| 3.8〜15.0秒 | 内蔵サーバーがセーブを読む（マイグレーション約1.4秒を含む） |
| 15.7〜21.2秒 | ブロックアイコンの撮影（87個 5.1秒＋列車 0.3秒） |
| 21.2〜30.5秒 | 地形の取得など。fps は 40〜60 で動いている |
| **30.5〜54.4秒** | **1フレーム約24秒**（下の「シーン読み込み＋初期応答」） |
| **54.4〜67.9秒** | **1フレーム約13.5秒**（下の「ブロック生成」） |
| 68.0〜71.1秒 | ブループリントのサムネイル撮影。「工場」の `elapsed:24045ms` は、上の止まったフレームをまたいだ待ち時間を含む |
| 72〜76秒 | Renderer 約21.75万で落ち着く。直後の fps は 15〜20 |

## 重いフレームの内訳（Profiler 実測、`heavy`・`subtree`）
### サーバー起動：12.5〜13.3秒
- `ServerStarter.Start` total 12,494ms のうち self が 11,204ms。マーカーが無いので、セーブ読み込みのどこが重いかは分からない。
- 配下で名前が付いているのは GC.Alloc 1,549万回（531ms）、Mono.JIT（385ms）、ログ出力 749回（約300ms。マイグレーションの警告など）だけ。

### シーン読み込み＋初期応答：25.0〜25.6秒
| 処理 | 時間 | 内容 |
|---|---:|---|
| MainGame シーン読み込み後の初期化（`SceneManager.Internal_SceneLoaded`） | 9.2秒 | self 4.2秒、`Instantiate` 20,133回で 3.1秒、`AddComponent` 110,469回で 0.85秒、`GetComponentNullErrorMessage` 22.4万回で 0.47秒 |
| シーン統合（`Application.LoadLevelAsync Integrate`） | 1.6秒 | |
| 初期応答の処理（`PacketExchange.RouteResponse`） | 8.6秒 | self 4.9秒（マーカー無し）。`BlockState.Deserialize` 392回で 0.96秒、ブロック状態の各プロセッサ合計約1.4秒（電線 7.2万回・機械 7.5万回・歯車 13万回・歯車ベルト 6.5万回）、コライダーの焼き込み 918回で 0.58秒、`Instantiate` 3,026回で 0.4秒 |
| 受信イベントの処理（`PacketExchange.RouteEvent`） | 1.2秒 | 1,520件。前回は同じ位置で 78.4万件・30.9秒だった |
| CEF の起動（`CefUnityBrowserSample.Start`） | 3.7秒 | うち FMOD 初期化 1.0秒 |

### ブロック生成：13.1〜14.3秒
| 処理 | 時間 | 内容 |
|---|---:|---|
| Addressables の完了コールバック（`DelayedActionManager.LateUpdate`） | 10.7〜11.1秒 | `Instantiate` 15,082回で 3.3秒（Produce 2.0・Awake 0.7・Copy 0.4）。**self 7.2秒は生成後の初期化で、マーカーが無い**。`BlockPreviewBoundingBoxSurface` 90,498個・`BlockPreviewBoundingBox` 15,083個・`PreviewOnlyObject` 15,083個をこのフレームで作っている |
| VContainer の起動 | 1.2〜1.9秒 | `AddComponent` 30,188回（`ObservableDestroyTrigger`・`EffectsQualitySwitch` が各15,094個） |

### その後
- 次のフレーム（2.3秒）では、受信イベント 884件で 1.2秒、地形の高さフィールド生成で 0.5秒。
- 6.1秒の `EditorLoop` だけのフレームが1つある。Editor の処理なので、ビルド版には無い。

## 定常時（`before-gameobject-reduction`）について
fps は 19.0（18.4〜19.4）で、2026-10-30 の `tick-bundle`（25.4）より低かった。ただし、ゲームのコードと関係の無い `EditorLoop`（9.6→13.2ms）やカメラ描画（10.7→13.4ms）まで同じ割合で遅くなっている。そのため、コードの悪化ではなく計測環境の差と見ている（推測）。計測時、Windows 側でリモートデスクトップ（`remoting_desktop`）が CPU を多く使っていた。また今回は Editor を前面に出している（10/30 は `editorWindowForeground: False`）。GameObject 削減の前後比較は、同じ日・同じ表示状態で `before-gameobject-reduction` を取り直してから行う。

## 次に見るべきこと
- ブロック生成の self 7.2秒、初期応答の self 4.9秒、サーバー起動の self 11.2秒は、マーカーが無いので中身を分けられない。GameObject 削減の効果を読み込み時間で確かめるときは、`BlockGameObject.Initialize` まわりに ProfilerMarker を入れる。
