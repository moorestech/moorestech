# パイプの半透明部品を1つのシェーダーにした前後（2026-11-19〜20）

パイプの窓・水・水位を1つのシェーダー（`Moorestech/FluidPipeTransparent`）で描き、窓と水を1つの Renderer にまとめた変更（PR #1697）の前後を、ベースラインのワールド・決まった視点で測った。変更前は `bf92c800bb`、変更後は `12b9e23dbb`。

## 結論
- Windows（スキンメッシュを切って計測）: fps 15.8→19.6（+24%）、PlayerLoop 55.7→45.3ms、カメラ描画 30.9→21.7ms、SetPass 2,590→487。
- Mac: URP の描画時間 328→128ms（−61%）、SetPass 5,101→809。鋼鉄のパイプの寄与（描画を止めたときに減る時間）は 187→9ms、鉄のパイプは 30→7ms。
- ドローコールの数はほぼ変わらない。描く物の量ではなく、シェーダーの切り替え（SetPass）が減った分だけ速くなった。
- Windows では、変更前の計測中に GPU のデバイス喪失（`DXGI_ERROR_DEVICE_HUNG 887a0006`）で Editor が3回落ちた。プレイヤー以外のスキンメッシュ 4,701個を全部切っても起きたので、スキンメッシュだけが原因ではない。変更後の計測中（1回の読み込みで3回）には起きなかったが、回数が少なく、今回の変更で起きにくくなったとは言えない。

## なぜ遅かったか（変更の要点）
半透明は奥から順に1つずつ描く。パイプ1本に別々のシェーダーの半透明部品が3つ（窓＝URP/Lit、水・水位＝別々の Shader Graph）あり、奥から順に描くとパイプごとにシェーダーが替わって SetPass が出ていた。SRP Batcher は同じ variant なら材質が違っても切り替えなしで続けて描けるので、3部品を同じ variant にした。窓と水は境界の中心が同じ点で距離が同点になり、同じシェーダーにすると描く順がパイプごとにばらつくので、1つのメッシュの2つのサブメッシュ（窓→水）にまとめて順番を固定した。詳しくは PR #1697。

## Windows の結果
| 項目 | 変更前（2回） | 変更後（3回） |
|---|---:|---:|
| fps（Profiler なし10秒） | 15.78<br><sub>15.4〜16.1</sub> | 19.59<br><sub>19.5〜19.6</sub> |
| PlayerLoop ms | 55.7<br><sub>55.2〜56.3</sub> | 45.3<br><sub>44.3〜46.2</sub> |
| カメラ描画 ms（`RenderPlayModeViewCameras`、ProfilerRecorder） | 30.9<br><sub>30.6〜31.3</sub> | 21.7<br><sub>21.6〜21.8</sub> |
| URP の描画処理 ms（`Inl_UniversalRenderTotal`） | 14.7<br><sub>14.3〜15.1</sub> | 14.3<br><sub>14.2〜14.3</sub> |
| SetPass | 2,590 | 487 |
| ドローコール | 53,583 | 51,882 |
| Batches | 47,408 | 40,574 |
| Renderer 数 | 約21.77万 | 約21.60万 |

- URP の描画処理（メインスレッドの側）はほぼ変わらず、カメラ描画全体が約9ms縮んだ。SetPass の減少は、レンダースレッドと GPU の側に効いている（推測）。
- 変更後は Renderer が約1,640個少ない。窓の Renderer を水の Renderer にまとめて消したため。
- 変更前は3回目の計測の途中でデバイス喪失が起き、2回分しか取れていない。

### 条件
| 項目 | 値 |
|---|---|
| マシン | Windows／i7-12700／RTX 3060 Ti |
| 実行形態 | Unity Editor 6000.3.8f1 の PlayMode、D3D12、コード最適化 Debug、品質 High、vSync 1／targetFrameRate 60、Game ビュー 1920×1080 |
| ワールド | ベンチマークのベースライン `2026-10-22-world_1`（save.json の SHA256 `6BF6222F…`） |
| 視点 | `snippets/place-player.cs`：(622.21, 82.99, 420.90)、三人称・距離 3.5m・下へ 2.52°・方位 321.86°・fov 70。半径20m 内のブロック 409 |
| スキンメッシュ | プレイヤー以外の SkinnedMeshRenderer 4,701個（蒸気機関 2,288、電気汎用工作装置 1,710、ガソリンエンジン発電機 374、油井 287 など）を `snippets/disable-skinned.cs` で無効にした。定常待ちの間・計測位置へ移した後・各計測窓の直前に毎回切り直し、各窓の直前に有効なまま残った数は0 |
| 窓 | ベンチマークと同じ fps 10秒・Profiler 6秒・受信イベント 6秒に、描画カウンタ 6秒（`snippets/render-start.cs`／`render-collect.cs`、Profiler の記録なしの ProfilerRecorder）を足した。1回の読み込みで3回 |

スキンメッシュを切ったのは、変更前の計測中にデバイス喪失が2回続き、別の調査でスキンメッシュの GPU 変形が疑われていたため。パイプの負荷の差を見るのが目的なので、前後とも同じく切った。スキンメッシュを切らない状態の前後の値は取れていない。

## Mac の結果
| 項目 | 変更前 | 変更後 |
|---|---:|---:|
| 1フレーム ms | 462 | 334 |
| URP の描画時間 ms（`Inl_UniversalRenderTotal`） | 328 | 128 |
| SetPass | 5,101 | 809 |
| ドローコール | 120,572 | 120,574 |
| 鋼鉄のパイプの寄与 ms／SetPass | −187／−3,134 | −9／−96 |
| 鉄のパイプの寄与 ms／SetPass | −30／−1,361 | −7／−98 |

- 値は、何も止めない基準の窓（各49個）の平均。寄与は、その種類だけ描画を止めた窓と前後の基準の窓の差。
- 計測は `perf/object-category-render-cost` ブランチの `docs/performance/2026-11-19-object-category-render-cost/run.sh`（種類ごとに描画を止める計測）を、そのまま前後で1回ずつ回した。結果は `results/mac/`。比べ方は同じブランチの `analyze.py`。
- 条件: MacBook（Apple M3、メモリ 24GB）、Unity Editor の PlayMode、Metal、Debug、品質 High、描画距離 300m・影あり、1920×1080。ワールドはテスターの `world_1` の 2026-10-21 時点のコピー（save.json の SHA256 `12b82416…`、ベースラインの1日前で同一ではない）、視点は Windows と同じ。スキンメッシュは切っていない。
- 変更前の基準（328ms）は、同じ条件で前日に測った値（198ms）より重く出た。計測中のスワップが約12GBあり、マシンの状態の違いと見ている（推測）。前後で同じ状態なので比べられる。

## Windows のデバイス喪失
| 回 | 計測していたコード | スキンメッシュ | 起きた所 |
|---|---|---|---|
| 1 | 変更前 | 有効 | 計測位置へ移した直後 |
| 2 | 変更前 | 有効 | 計測1回目の fps 窓 |
| 3 | 変更前 | 4,701個を無効 | 計測3回目の途中 |

- 3回とも Editor.log に `D3D12Fence::Wait ... Device removal.`・`d3d12: Device failed error (887a0006)`。GPU メモリは足りていた（Local の CurrentUsage 約1.8GB／Budget 約7.6GB）。
- 落ちた後は「Unity Bug Reporter」（送っていない）か「Fatal Error!（Unrecoverable D3D12 device error）」のダイアログが出て、次の起動で「Recovering Scene Backups」が出る。
- 過去の調査 [2026-10-01 D3D12クラッシュ](../../research/2026-10-01-d3d12-crash/README.md) とは失敗の経路が同じかどうか確かめていない。

## 再計測の手順
1. Windows で計測したいコミットを checkout して Editor でコンパイルする（マスターデータは `.moorestech-external-revisions.json` の固定先に合わせる）。
2. Mac から `docs/performance/2026-11-20-fluid-pipe-transparent-setpass/run-benchmark-noskin.sh <label>`。結果は `results/windows/`。`snippets/shader-check.cs` を先に実行すると、水の材質のシェーダー（変更前は Shader Graph、変更後は `Moorestech/FluidPipeTransparent`）で計測するコードを確かめられる。
3. Editor が落ちたら、画面を確かめてからダイアログを閉じ、起動し直す。

## ファイル
| ファイル | 内容 |
|---|---|
| `run-benchmark-noskin.sh` | ベンチマークの `run-benchmark.sh` に、計測位置への移動・スキンメッシュの無効化・描画カウンタの窓を足したもの |
| `snippets/disable-skinned.cs` | プレイヤー以外の SkinnedMeshRenderer を無効にし、数と内訳を返す |
| `snippets/render-start.cs` / `render-collect.cs` | URP の描画時間・カメラ描画・SetPass・ドローコール・Batches のフレーム平均 |
| `snippets/place-player.cs` | 決まった位置・視点へ移す（`perf/object-category-render-cost` のものの複製） |
| `snippets/shader-check.cs` | 水の材質のシェーダー名とグラフィックス API を返す |
| `results/windows/` | Windows の結果 JSON（変更前 r1・r2、変更後 r1〜r3） |
| `results/mac/` | Mac の結果 JSON と条件 |
