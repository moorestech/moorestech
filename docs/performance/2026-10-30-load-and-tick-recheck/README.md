# 読み込み時間の測り直しと、名前の無い 6ms の正体（2026-10-30）

対象コード: ブロック状態の tick 束ね送信ブランチ（`e20e74e495`）。Tick の内訳だけは、そこにマーカーを足した `perf/train-simulator-tick-markers`（`23a1706af9`）で測った。ベースラインワールド `2026-10-22-world_1`、windows-desktop の Editor（Debug 最適化）。

## 結論
- **ワールド読み込みは約110秒 → 約68秒**（Profiler なし、開始ボタンから Renderer 数が落ち着くまで）。前回いちばん大きかった「溜まった受信イベントの後処理」（約44秒）が大きく減った。
- **定常時の名前の無い約6ms は、VContainer の `ITickable.Tick()`**（`VContainerUpdate` の段）。そのほぼ全部（約4.7ms）が `TrainUnitClientSimulator.Tick()`。このメソッドは列車だけでなく、ベルト上のアイテム表示とパイプの水位も毎フレーム更新している。
- Tick の内訳は、**パイプの水位 2.74ms**、ベルト上のアイテム 1.14ms、列車 tick の進行 0.92ms、列車の描画 0.47ms、合流マスの覆い 0.13ms。

## 1. 読み込み時間（Profiler なし）
生データ: [timeline-noprofiler-raw.txt](timeline-noprofiler-raw.txt)

| 区間 | 2026-10-22 | 2026-10-30 | 中身 |
|---|---:|---:|---|
| サーバー起動（セーブ読み込み） | 3.2〜13.5秒 | 3.0〜12.8秒 | ほぼ同じ約10秒 |
| アイコン撮影 | 13.8〜18.5秒 | 13.1〜17.8秒 | 同じ約5秒 |
| 地形の取得など | 18.5〜26.5秒 | 18〜24秒 | |
| **メインスレッドが止まる区間** | **26.5〜75秒（約49秒）** | **24〜47秒（約23秒）** | シーン読み込み・溜まったイベントの後処理・CEF の起動 |
| **1フレーム数秒〜十数秒が続く区間** | **75〜102秒（約27秒）** | **47〜62秒（約15秒）** | ブロック15,082個の生成・残りのイベント処理 |
| 定常状態になるまで | 約112秒 | **約68秒** | |

### まだ残っている大きな塊
読み込み中に Profiler を記録した回（[heavy-frames-with-profiler-raw.txt](heavy-frames-with-profiler-raw.txt)）で構造を見た。この回は Profiler の負荷で全体が遅く、秒数はそのまま使えないので、構造だけ参考にする。
- **溜まったイベントの後処理**：まだ1フレームで約17秒（`RouteResponse` 15.4秒、`RouteEvent` 1.9秒）。束は1,559個まで減ったが、束の中の状態をプロセッサへ全部流しているので、電線のプロセッサが約24万回、機械のプロセッサが約29万回呼ばれていた。ブロック状態は最新値で上書きするだけなので、**溜まった束を再生するときに座標ごとの最新だけを適用すれば**、ここはほぼ消える見込み（推測）。
- **シーン読み込み**：約11秒（`SceneManager.Internal_SceneLoaded` 9.7秒）。
- **ブロック生成**：約11秒（`Instantiate` 15,082回で3.6秒、残りは生成後の初期化）。
- **サーバー起動**：約11.6秒（`ServerStarter.Start` の1フレーム、GC.Alloc 約1,460万回）。
- CEF の起動 約3秒。

## 2. 名前の無い約6ms の正体
### 親マーカー別の集計
生データ: [invoke-parents-raw.txt](invoke-parents-raw.txt)

Profiler の階層で、名前の無い `UpdateFunction.Invoke` の self 時間を、親マーカー別に集計した。**5.56ms/フレームが `VContainerUpdate` の下**で、他の親はどれも0.1ms未満だった。

### Tick を1つずつ外して測る
生データ: [ablation-raw.txt](ablation-raw.txt)。スニペットは `snippets/ablate-*`。

VContainer の Update に登録された Tick は9個ある。1つずつ一覧から外して約4秒ずつ `VContainerUpdate` の平均を測り、すぐ戻した（2周）。

| 外した Tick | VContainerUpdate（ms、2周） |
|---|---|
| （全部あり） | 4.8〜6.0 |
| **TrainUnitClientSimulator** | **0.40 / 0.41** |
| それ以外の8個（GameFrameRecorder、各カリング管理、プレイヤー関連、列車デバッグ表示） | どれも全部ありと誤差の範囲（4.8〜6.1） |

### TrainUnitClientSimulator.Tick の内訳
生データ: [tick-tree-raw.txt](tick-tree-raw.txt)。マーカーは `perf/train-simulator-tick-markers` で追加した。

| マーカー | 中身 | ms/フレーム |
|---|---|---:|
| `TrainSimulator.FluidPipeWaterLevels` | `FluidPipeWaterLevelPresenter.UpdateAll` | **2.74** |
| `TrainSimulator.BeltItems` | `BeltItemObjectPresenter.UpdateAll` | 1.14 |
| `TrainSimulator.AdvanceTicks` | 列車 tick の進行（受信イベントの適用を含む） | 0.92 |
| `TrainSimulator.TrainVisuals` | 列車の描画更新 | 0.47 |
| `TrainSimulator.BeltMergeCellCovers` | ベルト合流マスの覆い | 0.13 |

**パイプの水位が重い理由（コードを読んでの推測、未計測）**：`FluidPipeWaterLevelPresenter.UpdateAll` は、流体の複製の版が進むたび（ほぼ毎フレーム）に、全ノードを回して水位を書き込んでいる。値が変わっていないパイプも対象になる。さらに、水位の表示を持たないノード（タンクや機械など）は「まだ表示が無い」扱いになるため、毎回 `TryGetBlockGameObject` と `GetComponentInChildren` で探し直していると見られる。液体は別タスクで作り直し中なので、その担当へ渡す材料として残す。

## 次の候補
1. 溜まったブロック状態の束を再生するときは、座標ごとの最新だけを適用する（読み込みの約17秒、推測）。
2. パイプ水位の更新を、変わったノードだけ・表示を持つノードだけにする（約2.7ms/フレーム）。液体の作り直しで扱う。
3. 描画対象の数を減らす（定常時 約20〜25ms/フレーム）。[GameObject 数の内訳](../2026-10-22-gameobject-count/README.md) を参照。
