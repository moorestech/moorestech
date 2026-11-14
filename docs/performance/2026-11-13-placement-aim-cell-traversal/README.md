# 設置照準を占有セルの走査に替えた前後の計測（2026-11-13）

[ADR 0100](../../adr/0100-placement-aim-hits-occupied-cells-by-traversal.md) の変更（設置照準の当たり判定を、ブロックごとの `BlockPlacementHitFaces`（ブロック1個につき GameObject 1個）から、占有セルの索引の走査に替える）の前後で、ワールド読み込み・設置モードの出入り・GameObject 数・定常時の fps・照準1本の時間を、同じ夜（23:40〜翌0:30）に変更前→変更後の順で続けて測った。方法は [2026-11-12 の記録](../2026-11-12-placement-preview-frame/README.md) と同じ。

## 結論
- **設置モードに入る遷移は 560ms → 224ms（2回目以降、-60%）。** 1回目は 614 → 290ms（-53%）、出る遷移は 167 → 79ms（-53%）。中身は `GameObject.ActivateAwakeRecursively` で、入る 500 → 180ms、出る 125 → 49ms（実測）。変更前に残っていた約0.5秒は `BlockPlacementHitFaces` 15,082個の有効化が大半だったと考えられる（推測。今回は有効化の対象を分けて測っていないが、変更後に当たり判定が無くなると 0.5 → 0.18秒に下がっている）。
- **GameObject・Transform は 15,082個減った**（ブロックごとに1個）。Transform 総数 428,711 → 413,625（-15,086）、うち activeInHierarchy は 315,905 → 315,901〜903 でほぼ同じ。ブロック配下の Collider は 87,798 → 72,716（-15,082）。`BlockPlacementHitFaces` は 15,082個 → 型が無い。
- **ブロック生成のフレームの `GameObject.AddComponent` は 246,207回 → 125,551回（-120,656。ブロック 15,082個 × 8 と一致）。** ブロック生成を含むフレームは 25,457 → 24,964ms（-493ms）。ただし1回の読み込みどうしで、ばらつきは見積もっていない。
- **ワールド読み込みは Renderer 21.75万に達する時刻で 77.3 → 59.4秒（-17.9秒）だが、「開始→定常」は 83（timeline 回）／72（subtree 回）→ 66／73秒で、差は回によってばらつく。** 変更前の timeline 回が遅い（2026-11-12 の変更後は 70.3秒）ので、この差の全部が変更によるとは言えない（読み込みごとのばらつきが大きく、AddComponent 120,656回 ≒ 約0.5秒分では説明できない。推測：その回は Editor 側の状態などで遅かった）。読み込みの短縮は、この測定では結論にしない。
- 定常 fps・`PlayerLoop` はほぼ変わらない（26.75 → 27.24fps、30.7 → 30.5ms。範囲が重なっておりノイズの範囲）。
- **ADR 0100 決定9の基準（照準1本の平均が全方向で 0.1ms 以下）は満たせなかった（不合格）。** 何にも当たらない水平の照準1本の平均は、+X 方向 0.51ms、+Z 方向 0.45ms、XZ 対角 1.27ms、-X+Z 対角 1.88ms（実測。いずれも 0.1ms の 4.5〜19倍）。Editor の Debug 最適化・JIT 後の Mono での値で、走査したセルは 1,056〜3,924個。外れる線（外接直方体を横切って何にも当たらない線）は4方向とも見つかっている。
- （2026-11-14 追記）その後、占有セルの索引を八分木にして基準を満たした（最悪 0.051ms／本、db9e02118f）。下の「照準1本の時間：八分木にした後」。

| 指標 | 変更前 110ccc5674 | 変更後 f1f4c3fbc3 | 差 |
|---|---:|---:|---:|
| Transform 総数（うち activeInHierarchy） | 428,711（315,905） | 413,625〜413,627（315,901〜315,903） | -15,086（-4） |
| ブロック配下の Collider 数 | 87,798 | 72,716 | -15,082 |
| ブロック配下の GameObject 数 | 358,596 | 343,514 | -15,082 |
| `BlockPlacementHitFaces` 数 | 15,082 | 型が無い | -15,082 |
| ブロック生成フレーム（ms）と `GameObject.AddComponent` 回数 | 25,457・246,207回（`Internal_SceneLoaded` 9,874ms） | 24,964・125,551回（同 8,879ms） | -493ms・-120,656回 |
| 開始→定常（秒） | 83（timeline 回）／72（subtree 回） | 66／73 | -17／+1 |
| Renderer 21.75万に達した時刻（開始ボタンから秒、timeline 回） | 77.3 | 59.4 | -17.9 |
| 設置モードに入る遷移 1回目（ms、n=3） | 614<br><sub>604〜632</sub> | 290<br><sub>251〜355</sub> | -324（-53%） |
| 同 2回目以降（n=12） | 560<br><sub>532〜604</sub> | 224<br><sub>214〜236</sub> | -336（-60%） |
| 同：6フレームの超過時間 1回目／2回目以降（ms） | 834／776 | 466／373 | -368／-403 |
| うち `GameObject.ActivateAwakeRecursively`（入る、15回の平均） | 500<br><sub>473〜526</sub> | 180<br><sub>169〜188</sub> | -320 |
| 設置モードから出る遷移（ms、n=12） | 167<br><sub>162〜193</sub> | 79<br><sub>72〜85</sub> | -88（-53%） |
| うち `ActivateAwakeRecursively`（出る、12回の平均） | 125<br><sub>120〜151</sub> | 49<br><sub>44〜53</sub> | -76 |
| 定常 fps（Profiler なし、3回） | 26.75<br><sub>26.47〜27.18</sub> | 27.24<br><sub>26.97〜27.51</sub> | +2% |
| `PlayerLoop` ms | 30.7<br><sub>30.0〜31.4</sub> | 30.5<br><sub>30.0〜30.9</sub> | -1% |
| 照準1本の平均 ms（+X／+Z／XZ 対角／-X+Z 対角） | 測れない（走査が無い） | 0.51／0.45／1.27／1.88 | 基準 0.1ms：不合格 |

## 計測環境
| 項目 | 値 |
|---|---|
| マシン | windows-desktop（i7-12700／RTX 3060 Ti） |
| 実行形態 | Unity Editor 6000.3.8f1 の PlayMode、「ローカルでプレイ」。コード最適化は Debug |
| 変更前 | master 110ccc5674（#1655 のマージ。ADR 0099 を含む） |
| 変更後 | `docs/placement-aim-cell-traversal` f1f4c3fbc3 |
| 外部リポジトリ | 前後とも同じ（`.moorestech-external-revisions.json` に差分なし）：moorestech_master 14e6a758c5、moorestech-client-private 1d6e12013f |
| ワールド | `2026-10-22-world_1`（ブロック 15,082個） |
| 計測時刻 | 変更前 2026-11-13 23:40〜翌0:03、変更後 0:05〜0:30。同じ Editor プロセス（コンパイルで入れ替え） |

Editor が落ちていたので、スケジュールタスクで起動し直した。起動時に出た「Recovering Scene Backups」のダイアログは「No」で閉じた（計測には影響しない）。計測後は windows-desktop を元の 52b999a351 に戻し、PlayMode を止めた。

## 計測方法
前回と同じスクリプトを、変更前→変更後の順に実行した。

| 手順 | スクリプト | 生データ |
|---|---|---|
| ワールド読み込みの時系列（Profiler なし） | `../2026-11-08-world-load-remeasure/load-measure.sh timeline` | [before](before-timeline/timeline-raw.txt)・[after](after-timeline/timeline-raw.txt)（標準出力：[before](before-timeline/load-measure-stdout.txt)・[after](after-timeline/load-measure-stdout.txt)） |
| 1秒超のフレームの内訳（Profiler あり） | `load-measure.sh subtree` | [before](before-subtree/heavy-frames-raw.txt)・[after](after-subtree/heavy-frames-raw.txt)（標準出力：[before](before-subtree/load-measure-stdout.txt)・[after](after-subtree/load-measure-stdout.txt)） |
| 定常時ベンチマーク（1回の読み込みで3回） | `../benchmark/run-benchmark.sh <label>` | `../benchmark/results/*_before-aim-traversal_*`・`*_after-aim-traversal_*`、比較表 [compare.md](compare.md) |
| Transform 内訳・設置モードの出入り（各条件3回の読み込み） | `../benchmark/run-buildmode-entry.sh <label>` | `../benchmark/results/2026-11-13T2355・2358・2026-11-14T0001_before-aim-traversal_buildmode.txt`、`2026-11-14T0012・0015・0019_after-aim-traversal_buildmode.txt` |
| 照準1本の最悪時間（変更後のみ） | [run-trace-cost.sh](run-trace-cost.sh)（`run-buildmode-entry.sh` と同じ読み込み・定常待ち・後片付けで、`../benchmark/snippets/placement-aim-trace-cost.cs` を実行） | [after-trace-cost.json](after-trace-cost.json) |

- 設置モードの出入りは、1回の読み込みで入る→出るを4回＋最後にもう1回入る。1回目の入る（n=3）と2回目以降（n=12）を分けて集計した。「超過時間」は遷移のフレームから6フレームの、直前30フレームの中央値を超えた分の合計。
- 照準のスニペットは、設置モード外で `PlacementAimCellTracer.TryTrace` を直接 1,000回呼んだ平均（1回目の JIT は捨てる）。占有セルの外接直方体は (49,0,-384)〜(1104,113,2485)。+X・+Z・対角2本それぞれで、外接直方体を最長で横切る外れ線を探して撃った。`hits` は4方向とも 0（全区間を走査した）。

## 結果
### 設置モードの出入り（実測、ms、Stopwatch）
入る（1回目）・出る・入る・出る・入る… の順。1回目を太字にした。

| 読み込み | 変更前：入る | 変更前：出る | 変更後：入る | 変更後：出る |
|---|---|---|---|---|
| 1 | **632**・569・532・551・555 | 163・167・166・169 | **265**・222・228・233・214 | 84・76・82・85 |
| 2 | **604**・560・544・553・561 | 169・162・162・193 | **355**・236・218・226・219 | 81・77・76・75 |
| 3 | **607**・604・556・576・560 | 166・164・163・164 | **251**・226・219・219・232 | 80・79・79・72 |

設置モード中（`placement-frame-count.cs`、各条件3回とも同じ）：`BlockPreviewBoundingBox` 有効・描画中は前後とも 177個、`PreviewOnlyObject` 有効 10,289・描画中 172、`BlockPlacementHitFaces` 有効は変更前 15,082個・変更後は型が無い。描画される枠の数は変わらない。

### ワールド読み込み（実測）
1秒超のフレーム（subtree 回、ms）

| フレーム | 変更前 | 変更後 |
|---|---:|---:|
| サーバー起動（`ServerStarter.Start`） | 13,177 | 13,500 |
| 中身の分からないフレーム | 3,987 | 4,470 |
| シーン読み込み＋初期応答（ブロック生成） | 25,457 | 24,964 |
| その次 | 1,457 | 1,663 |

ブロック生成のフレーム（`Internal_SceneLoaded` 配下）：`GameObject.AddComponent` 246,207回（1,505ms）→ 125,551回（950ms）、`GameObject.ActivateAwakeRecursively` 57,508回（570ms）→ 12,262回（208ms）、`Instantiate.Produce` 20,133回で変わらない（1,393 → 1,613ms）。`Internal_SceneLoaded` の total は 9,874 → 8,879ms（-995ms、1回ずつ）。

### 定常時（`compare.py`、実測）
fps は Profiler なしの値。2026-11-12 の記録と同じく、表示待ちのマーカーが記録されていない回があり（compare.py の警告）、fps は参考にとどめる。

| 指標 | before-aim-traversal（3回）<br>110ccc5674 | after-aim-traversal（3回）<br>f1f4c3fbc3 |
|---|---:|---:|
| fps（Profiler なし） | 26.75<br><sub>26.47〜27.18</sub> | 27.24<br><sub>26.97〜27.51</sub> (+2%) |
| フレーム ms 平均（Profiler なし） | 37.4<br><sub>36.8〜37.8</sub> | 36.7<br><sub>36.4〜37.1</sub> (-2%) |
| フレーム ms p95（Profiler） | 54.4<br><sub>48.8〜58.8</sub> | 50.3<br><sub>49.4〜51.7</sub> (-8%) |
| 受信イベント 件/秒 | 59<br><sub>56〜62</sub> | 56<br><sub>55〜57</sub> (-5%) |
| 受信処理 µs/件（応答を除く） | 1,583.5<br><sub>1,519.5〜1,663.8</sub> | 1,595.9<br><sub>1,452.0〜1,881.0</sub> (+1%) |
| 受信処理 ms/フレーム（応答を除く） | 3.3<br><sub>3.0〜3.5</sub> | 3.2<br><sub>2.9〜3.8</sub> (-1%) |
| ブロック状態 件/秒 | 4,208<br><sub>4,007〜4,398</sub> | 4,030<br><sub>3,905〜4,169</sub> (-4%) |
| 受信 payload KB/秒 | 786<br><sub>750〜820</sub> | 754<br><sub>730〜779</sub> (-4%) |
| `PlayerLoop` ms | 30.7<br><sub>30.0〜31.4</sub> | 30.5<br><sub>30.0〜30.9</sub> (-1%) |
| `UniTaskLoopRunnerYieldUpdate` ms | 3.3<br><sub>3.0〜3.5</sub> | 3.2<br><sub>2.9〜3.8</sub> (-1%) |
| `PacketExchange.DispatchReceivedPackets` ms | 3.3<br><sub>3.0〜3.5</sub> | 3.2<br><sub>2.9〜3.8</sub> (-1%) |
| `PacketExchange.Deserialize.ProtocolBase` ms | — | — |
| `PacketExchange.ReadPacketHeader` ms | 0.0<br><sub>0.0〜0.0</sub> | 0.0<br><sub>0.0〜0.0</sub> (+5%) |
| `PacketExchange.Deserialize.EventStream` ms | 0.1<br><sub>0.1〜0.1</sub> | 0.1<br><sub>0.0〜0.1</sub> (-11%) |
| `PacketExchange.RouteEvent` ms | 3.2<br><sub>3.0〜3.4</sub> | 3.2<br><sub>2.8〜3.7</sub> (-1%) |
| `PacketExchange.RouteResponse` ms | — | — |
| `BlockState.Deserialize` ms | 1.2<br><sub>1.1〜1.3</sub> | 1.1<br><sub>1.1〜1.2</sub> (-9%) |
| `BlockState.FluidPipe.GetStateDetail` ms | — | — |
| `BlockState.FluidPipe.ApplyWaterLevel` ms | — | — |
| `RenderPlayModeViewCameras` ms | 10.2<br><sub>10.0〜10.5</sub> | 10.1<br><sub>9.9〜10.2</sub> (-1%) |
| `EditorLoop` ms | 9.2<br><sub>8.4〜9.6</sub> | 8.6<br><sub>8.3〜8.9</sub> (-6%) |
| `GC.Collect` ms | 0.0 | 0.4 (+1614%⚠️) |
| `GC.Alloc` 回/フレーム | 4,805<br><sub>4,756〜4,853</sub> | 4,627<br><sub>4,565〜4,732</sub> (-4%✅) |

### 照準1本の時間（変更後のみ、実測）
ADR 0100 決定9の基準：平均が全方向で 0.1ms 以下。

| 向き | 平均 ms/本 | 走査したセル | 判定 |
|---|---:|---:|---|
| +X | 0.5084 | 1,056 | 不合格 |
| +Z | 0.4450 | 2,870 | 不合格 |
| XZ 対角（+X+Z） | 1.2749 | 3,924 | 不合格 |
| XZ 対角（-X+Z） | 1.8810 | 3,924 | 不合格 |

1セルあたりにすると約 0.16〜0.48µs（+Z 0.155、対角 0.325・0.479、+X 0.481）。0.1ms に収めるには、この環境で1本あたり約 200〜650セルが上限になる。Editor の Debug 最適化の Mono での値で、Release ビルドでは小さくなる可能性があるが、今回は測っていない（推測）。

### 照準1本の時間：空チャンクを飛ばした後（2026-11-14、実測）
8aa8912014 で、占有セルの索引に 16×16×16 マスのチャンクごとの占有数を持たせ、走査で空のチャンクを出口の直前のセルまで飛ばすようにした。同じ windows-desktop・同じベースラインワールド・同じ外れる線（`run-trace-cost.sh`）で測った。生データは `after-chunk-skip-trace-cost.json`。

| 向き | 飛ばす前 ms/本 | 飛ばした後 ms/本 | 判定 |
|---|---:|---:|---|
| +X | 0.5084 | 0.0644 | 合格 |
| +Z | 0.4450 | 0.0958 | 合格 |
| XZ 対角（+X+Z） | 1.2749 | 0.2092 | 不合格 |
| XZ 対角（-X+Z） | 1.8810 | 0.2527 | 不合格 |

軸に沿う向きは 0.1ms 以下に収まったが、対角の2方向はまだ基準の約2.1〜2.5倍ある。

### 照準1本の時間：八分木にした後（2026-11-14、実測）
db9e02118f で、占有セルの索引を根を広げる八分木にした（設計は `docs/superpowers/specs/2026-11-14-placement-aim-octree-design.md`、候補を比べた試作は `index-design/`）。16マスのチャンク版（8aa8912014）と同じ日に、同じ windows-desktop・同じベースラインワールドで続けて測った（`run-aim-cost.sh`）。生データは `chunk16-aim-cost.txt`・`octree-aim-cost.txt`。

最悪のレイ（何にも当たらず外接直方体を横切る線。ADR 0100 決定9の基準 0.1ms）:

| 向き | 16マスのチャンク ms/本 | 八分木 ms/本 | 判定 |
|---|---:|---:|---|
| +X | 0.0671 | 0.0089 | 合格 |
| +Z | 0.0988 | 0.0323 | 合格 |
| XZ 対角（+X+Z） | 0.2188 | 0.0509 | 合格 |
| XZ 対角（-X+Z） | 0.2608 | 0.0356 | 合格 |

実プレイに近い照準 1,000本（ブロックから 20m 以内の地面に立ち、目の高さから俯角 -60°〜+15°・方位ランダム、物理レイの距離で区切る。`placement-aim-realistic-cost.cs`）:

| | 16マスのチャンク | 八分木 |
|---|---:|---:|
| 平均 ms | 0.0211 | 0.0050 |
| p50 ms | 0.0062 | 0.0031 |
| p90 ms | 0.0465 | 0.0101 |
| p99 ms | 0.2628 | 0.0368 |
| 最大 ms | 0.6850 | 0.0766 |

全ブロック（15,082）を新しい索引へ登録する時間（3回の中央値）は 198.3ms → 194.5ms で、八分木にしても増えていない。

## 推測（未確認）
- 設置モードに入る遷移の短縮（560 → 224ms）は、`BlockPlacementHitFaces` の GameObject 15,082個の有効化が無くなったためと考えている。入る `ActivateAwakeRecursively` が 500 → 180ms、出る 125 → 49ms と、ともに約 -64%・-61% 下がっている。
- ブロック生成のフレームの短縮（-493ms）は、`AddComponent` 120,656回ぶん（約 555ms の減少：1,505 → 950ms）と整合するが、1回ずつの読み込みで、ばらつきを見積もっていない。
- ワールド読み込みの「開始→定常」「Renderer 21.75万」の差は、timeline 回の変更前が 2026-11-12 の変更後（70.3秒）より遅く、読み込みごとのばらつきが大きい。変更の効果として確かめるには同条件の読み込みの回数が足りない。
- 定常 fps が変わらないのは、設置モード外では `BlockPlacementHitFaces` が非アクティブで、定常時の activeInHierarchy の Transform 数が同じためと考えている。

## 計測の仕組みの変更
- [run-trace-cost.sh](run-trace-cost.sh)：定常後に `placement-aim-trace-cost.cs` を実行するだけの計測。`run-buildmode-entry.sh` と同じ流れ（作業ワールドの複製・ローカル開始・定常待ち・後片付け）。
