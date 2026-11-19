# オブジェクトの種類ごとの描画コスト（2026-11-19、Mac）

カメラ描画の時間のうち、どの種類のオブジェクトにどれだけかかっているかを、種類ごとに描画を止めて差を測った。あわせて、batchmode の Editor で同じ数字が取れるか（通常の Editor と比率が合うか）を確かめた。

## 結論
- 通常の Editor では、**鋼鉄のパイプが URP の描画時間の約56%（約112ms／198ms）**を占める。次は鉄のパイプ（14%）、上位10種以外のブロック（13%）、電気汎用工作装置（11%）、蒸気機関（9.5%）。ブロック以外（Terrain・鉱床・エフェクト・プレイヤー）は1種あたり3〜4%、レール・列車・ベルト上のアイテム・Terrain の木と草はほぼ0。
- 鋼鉄のパイプは、ドローコールでも三角形でも全体の約15%なのに、時間では56%。量に比べて1つあたりが重い。原因は未確認（シェーダーの重さや、材質の毎フレームの更新を疑っている。推測）。
- **batchmode は通常の Editor の代わりにならない。** 描いているもの（ドローコール・三角形、種類ごとの減り方）は2つで一致したが、時間の比率が合わない（鋼鉄のパイプは通常 56%、batchmode 15%。化学プラントは 7.5% と 0.8%）。batchmode の Editor は Game ビューが無くカメラを描かないので、計測用のコードから `Camera.Render()` で RenderTexture へ描かせており、画面へ出す経路と GPU 待ちの入り方が違うためと考えている（推測）。

## 条件
| 項目 | 値 |
|---|---|
| マシン | MacBook（Apple M3、メモリ 24GB） |
| Unity | 6000.3.8f1、Metal、Editor の PlayMode、コード最適化 Debug |
| 画質 | Unity の品質 High、ゲーム内の描画距離 300m・影あり・MapObject あり、LOD bias 1、vSync 1／targetFrameRate 60（実際は 2〜5fps なので効いていない） |
| 解像度 | 1920×1080（通常は Game ビュー、batchmode は RenderTexture） |
| ワールド | テスターの `world_1` の 2026-10-21 時点のコピー（save.json の SHA256 `12b82416…`）。ベンチマークのベースライン `2026-10-22-world_1`（`6BF6222F…`）の1日前で、同一ではない |
| 視点 | `snippets/place-player.cs`：(622.21, 82.99, 420.90)、三人称・距離 3.5m・下へ 2.52°・方位 321.86°・fov 70。半径20m 内のブロック 409 |
| 規模 | Renderer 約21.8万。この視点で1フレームあたりドローコール約12万・三角形約3,770万・SetPass 約5,100 |
| Profiler | 記録を止めて計測（下の「メモリ」を参照） |

## やり方
1. Renderer を親子関係で種類に分ける（ブロックは種別ごと、MapObject、鉱床、列車、レール、プレイヤー、エフェクト、ベルト上のアイテム、その他）。ブロックは描画対象の数が多い上位10種を個別に、残りを「上位10種以外」にまとめる。
2. 種類ごとに `Renderer.forceRenderingOff=true` で描画を止める（カリングの登録は残る）。Terrain は `drawHeightmap` と `drawTreesAndFoliage` で止める。ベルト上のアイテムは途中で生まれるので毎フレーム止め直す。
3. 「基準 → 種類1 → 基準 → 種類2 → … → 基準」と並べ、種類の差は「止めた窓 − 前後の基準の窓の平均」。窓は通常 6秒（最初の2秒を捨てる）、batchmode 10秒（最初の3秒を捨てる）。順番を変えて2周し、2つの差の平均を出す。何も止めない X0 と、全部止める ALL も入れた。
4. 切り替えと記録は PlayerLoop の中で行う（スニペットを呼ぶたびに Editor が止まるのを避けるため）。毎フレーム `ProfilerRecorder` で `Inl_UniversalRenderTotal`（URP の描画全体）・`RenderPlayModeViewCameras`・ドローコールなどを取る。

## 結果：通常の Editor と batchmode（`Inl_UniversalRenderTotal`）
基準の平均は通常 198ms・batchmode 233ms。窓1つの雑音（標準偏差）は通常 約8ms・batchmode 約13ms。X0（何も止めない）の差が +2〜3ms 出ているので、±3ms 程度の差は雑音とみなす。

| 種類 | 描画対象の数 | 三角形（千） | 通常 差 ms | 通常 %ALL | batchmode 差 ms | batchmode %ALL |
|---|---:|---:|---:|---:|---:|---:|
| ALL（全部止める） | | | −198.0 | 100 | −301.2 | 100 |
| ブロック：鋼鉄のパイプ | 4,851 | 2,890 | **−111.7** | **56.4** | −44.2 | 14.7 |
| ブロック：鉄のパイプ | 2,601 | 1,550 | −27.3 | 13.8 | −25.6 | 8.5 |
| ブロック：上位10種以外 | | | −26.6 | 13.4 | −46.5 | 15.5 |
| ブロック：電気汎用工作装置 | 7,122 | 2,526 | −22.4 | 11.3 | −19.8 | 6.6 |
| ブロック：蒸気機関 | 4,224 | 2,260 | −18.9 | 9.5 | −12.7 | 4.2 |
| ブロック：化学プラント | 2,846 | 1,281 | −14.8 | 7.5 | −2.4 | 0.8 |
| ブロック：鉄の歯車ベルトコンベア | 4,597 | 138 | −14.1 | 7.1 | −19.3 | 6.4 |
| ブロック：直線歯車ベルトコンベア | 2,709 | 337 | −13.7 | 6.9 | −18.2 | 6.1 |
| 鉱床 | 203 | 277 | −8.4 | 4.2 | +17.9 | −6.0 |
| その他（Environment・BeltMergeCellCovers 以外） | | | −8.4 | 4.2 | +0.2 | −0.1 |
| Terrain：地面 | | | −8.1 | 4.1 | −7.3 | 2.4 |
| エフェクト | 93 | 0 | −6.7 | 3.4 | −6.1 | 2.0 |
| ブロック：ガソリンエンジン発電機 | 1,168 | 528 | −6.6 | 3.3 | −6.4 | 2.1 |
| プレイヤー | 27 | 91 | −6.2 | 3.1 | −3.3 | 1.1 |
| ブロック：基本土台 | 2,199 | 26 | −5.1 | 2.6 | −6.1 | 2.0 |
| ブロック：電柱 | 1,086 | 224 | −3.6 | 1.8 | −3.7 | 1.2 |
| その他：Environment | 57 | 647 | −3.3 | 1.7 | −5.7 | 1.9 |
| レール | 149 | 347 | −0.2 | 0.1 | +1.4 | −0.4 |
| Terrain：木と草 | | | +0.3 | −0.2 | −2.3 | 0.8 |
| その他：BeltMergeCellCovers | 389 | 40 | +2.1 | −1.1 | −2.6 | 0.9 |
| X0（何も止めない） | | | +2.8 | −1.4 | +2.2 | −0.7 |
| ベルト上のアイテム | 822 | 10 | +3.3 | −1.7 | −2.6 | 0.9 |
| 列車 | 40 | 466 | +3.7 | −1.9 | +10.4 | −3.5 |

- 「描画対象の数」は `Renderer.isVisible`（影だけのものも含む）。三角形は描画対象のメッシュの合計。
- 種類ごとの差を足すと ALL を超える。種類どうしで共通にかかる分（影やバッチの組み方など）が、それぞれの差に重ねて入っているためと考えている（推測）。
- batchmode の ALL は窓ごとのばらつきが大きい（−370〜−232ms）。鉱床・列車の正の差も、batchmode の雑音の範囲と見ている。
- 描いている量は2つで一致した：ドローコールの減り方は、たとえば上位10種以外 24.3%／24.4%、電気汎用工作装置 13.7%／13.7%（`results/compare-draw-calls.txt`）。三角形も同じ（`results/compare-triangles.txt`）。違うのは時間の比率だけ。
- batchmode で `Camera.Render()` の前後を Stopwatch で測った値（`results/batch-camera-render-stopwatch.txt`）でも、鋼鉄のパイプは 15.7% で、通常の 56% とは合わない。

## メモリ（この Mac で測るときの注意）
- 最初の通常 Editor の計測で、Editor のスワップが **48GB** に膨らみ、ディスクの空きが 221MB まで減って Editor が終了した。ログには `Dropping Profiler frames data to reduce memory pressure.`・`Used paged memory: 91% (48846MB of 53248MB).` が出ていた。
- 原因は、保存されたレイアウトで Profiler ウィンドウが開いていて、記録がオンのままだったこと（`UserSettings/Layouts/default-6000.dwlt` の `m_Recording: 1`、Editor の設定 `Profiler.DefaultRecordState = 1`）。Renderer 約21.8万のシーンで毎フレームのデータが溜まり続けた。
- `snippets/profiler-off.cs` で記録を止めて Profiler ウィンドウを閉じた後は、Unity の RSS は最大 約7GB（通常）・約6.3GB（batchmode）で、空きは 77GB 以上を保った（`results/*_mem.log`）。
- `run.sh` は、空きが 30GB 未満なら始めない。`memlog.sh` は、空きが 15GB を切ったら PlayMode を止める。

## 測れていないこと
- 鋼鉄のパイプが量に比べて重い理由（シェーダー・材質・GPU の時間の内訳）。
- `forceRenderingOff` ではカリングの登録が残るので、カリングの分は差に入っていない。
- Windows（ベンチマークのマシン）・Release・ビルドしたプレイヤーでの値。この Mac の Editor（Debug、2〜5fps）の値なので、絶対値ではなく順位の目安として読む。

## ファイル
| ファイル | 内容 |
|---|---|
| `run.sh` | 起動済みの Editor（通常でも batchmode でも）で、ワールドに入って決まった視点へ移し、計測して `results/<label>.json` に保存する。`WORLD_SRC=<ワールドのフォルダ> run.sh <label>` |
| `memlog.sh` | Unity の RSS・スワップ・空き容量を10秒おきに記録する（`run.sh` が起動する） |
| `analyze.py` | 結果 JSON から種類ごとの差を出し、複数の結果を並べる。`analyze.py gui=results/gui3.json batch=results/batch2.json [--metric <値の名前>]` |
| `snippets/mac-enter-world.cs` | 複製したワールドでローカル開始（`benchmark/snippets/enter-world.cs` の Windows 専用部分を除いたもの） |
| `snippets/place-player.cs` | 決まった位置・視点へ移す（計測に使った版の複製） |
| `snippets/profiler-off.cs` | Profiler の記録を止めて、ウィンドウを閉じる |
| `snippets/batch-render-on.cs` | batchmode のとき、毎フレーム `Camera.main` を 1920×1080 の RenderTexture へ描かせる |
| `snippets/camprobe-start.cs` / `camprobe-collect.cs` | 種類分け・予定表・記録／回収と後始末 |
| `results/` | 生データ（`gui3`＝通常、`batch2`＝batchmode）、条件、メモリのログ、比較表 |

batchmode の Editor は `Unity -batchmode -projectPath <moorestech_client>`（`-quit` なし）で起動すると、uloop でスニペットを実行できる。PlayMode にも入れるが、カメラは描かれない（5秒・301フレームで描画0回を確認）。
