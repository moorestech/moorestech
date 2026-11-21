# ドローコールの内訳と GPU Resident Drawer（2026-11-21、Windows）

カメラ描画の時間が、どのパスのドローコールから来ているかを、パスを1つずつ切って測った。続けて、URP の GPU Resident Drawer（GRD）を有効にした前後を、同じワールド・同じ視点で測った。

## 結論
- **GRD を有効にすると、fps 13.6→21.1（+55%）、カメラ描画 38.1→15.9ms、ドローコール 6.03万→9,940。** 三角形の数（約1,878万）は変わらないので、描くものは同じまま、描画を組み立てる回数だけが減った。画面も変わらない（下の「見た目」）。
- 変更前のドローコール 6.03万のうち、影が約1.93万（32%）。影を全部切るとカメラ描画は約8.5ms減る。
- GRD の後、**GPU は1フレーム約5.5ms しか使っておらず、律速はメインスレッド（約40ms）**。カメラ描画の残り約15.7ms（メインスレッド）は、カリングと描画リストの組み立て（約5.5ms）、提出（約7.4ms）が大半。SSAO・アウトライン・ポストプロセス・カメラテクスチャは、切っても URP の描画時間が 0.3ms も変わらない。影は切ると約3.6ms・ドローコール約2,150減る（下の「カメラ描画の中身」）。

## 条件
| 項目 | 値 |
|---|---|
| マシン | Windows／i7-12700／RTX 3060 Ti |
| 実行形態 | Unity Editor 6000.3.8f1 の PlayMode、D3D12、**`-force-gfx-mt`（グラフィックスジョブなし）**、コード最適化 Debug、品質 High、vSync 1／targetFrameRate 60、Game ビュー 1920×1080 |
| コミット | `8606f8974e`（master）。変更後は、この上に下の「変更」を作業ツリーで当てたもの |
| ワールド | ベンチマークのベースライン `2026-10-22-world_1`（Renderer 約21.6万） |
| 視点 | `benchmark/snippets/place-player.cs`：(622.21, 82.99, 420.90)、三人称・距離 3.5m・下へ 2.52°・方位 321.86°・fov 70。半径20m 内のブロック 409 |
| スキンメッシュ | 切らない（GPU スキニング） |

グラフィックスジョブなしで測ったのは、6000.3.8f1 の既定（Split）ではこの視点で GPU が止まるため（`docs/research/2026-11-15-windows-d3d12-device-hung.md`）。過去のベンチマーク（Split・スキンメッシュを切って計測）とは条件が違うので、fps の絶対値は比べない。

## 変更（GRD を有効にする）
`snippets/enable-grd.cs` で Editor から書いて保存した。
- `UniversalRP-{Low,Medium,High}Quality.asset`：`m_GPUResidentDrawerMode` 0→1（Instanced Drawing）
- `URP Data.asset`・`URP Data - Skit.asset`：`m_RenderingMode` 0→2（Forward+）。GRD は、アセットの renderer data が全部 Forward+ でないと有効にならない（URP の `IsGPUResidentDrawerSupportedBySRP`）
- `ProjectSettings/GraphicsSettings.asset`：`m_BrgStripping` 0→2（BatchRendererGroup のシェーダーバリアントを全部残す。ビルドで GRD 用のバリアントが消えないように）

## 結果
各値は窓のフレーム平均。変更前・変更後とも、読み込み2回（2周と1周）の計3周の平均と範囲。何も切らない窓は1周に2つあるので6窓、切った窓は3窓。

| 項目 | 変更前 | 変更後 |
|---|---:|---:|
| fps（Profiler なし10秒） | 13.64<br><sub>13.36〜13.95</sub> | 21.12<br><sub>20.96〜21.24</sub> |
| PlayerLoop ms（Profiler） | 68.4<br><sub>67.0〜70.6</sub> | 43.6<br><sub>42.5〜44.6</sub> |
| カメラ描画 ms（`RenderPlayModeViewCameras`、Profiler） | 38.2<br><sub>37.6〜38.9</sub> | 15.9<br><sub>15.7〜16.1</sub> |
| ジョブ待ち ms（`WaitForJobGroupID`） | 12.1 | 6.5 |
| URP の描画処理 ms（`Inl_UniversalRenderTotal`） | 35.3 | 14.5 |
| ドローコール | 60,293 | 9,941 |
| SetPass | 500 | 507 |
| 影を落とす数（`Shadow Casters Count`） | 19,128 | 2,102 |
| 三角形 | 1,878万 | 1,879万 |

`Shadow Casters Count` も GRD の後は減って見えるが、GRD が描く分はこのカウンタに入らないためと見ている（推測）。

### パスを1つずつ切った窓（変更前、3窓の平均）
| 切ったもの | URP ms | ドローコール | 影を落とす数 | 三角形（万） |
|---|---:|---:|---:|---:|
| なし | 35.3 | 60,293 | 19,128 | 1,878 |
| 影（全ライトの `shadows=None`） | 26.5 | 41,031 | 0 | 1,392 |
| カメラの深度・不透明テクスチャの要求 | 35.3 | 59,824 | 18,983 | 1,860 |

この2回の計測（`before-grd`・`after-grd`）の SSAO とアウトラインの窓は、実際には何も切れていなかった。実行時の Renderer Feature は複製で名前に `(Clone)` が付くのに、`pass-toggle.cs` が名前の完全一致で探していたため。直した後の値は GRD の後のものだけ（下の「カメラ描画の中身」）。変更前の SSAO・アウトラインの寄与は測っていない。

### GRD の対象外
`snippets/grd-check.cs`（変更後の視点で）：Renderer のうち MeshRenderer 以外（スキンメッシュ・パーティクルなど）3,725、`MaterialPropertyBlock` 付き 0、DOTS インスタンシング非対応のシェーダーは `Unlit/Outline` の12個だけ。ほぼ全部が GRD に乗っている。GRD の後は `Renderer.isVisible` がカメラのカリング結果を表さなくなる（変更前 43,572、変更後 210,811）ので、この値で映る数を数えるスニペット（`placement-frame-count.cs` などの「描画中」）は GRD の後では読み方が変わる。

## カメラ描画の中身（GRD の後、2周の平均）
`run.sh` に、FrameTimingManager の CPU・GPU のフレーム時間（`render-start.cs`／`render-collect.cs`、Play 前に `frame-timing-on.cs`）と、Profiler の階層（`camtree-collect.cs`）を足して測った（`results/*_grd-camtree_*.json`、コミット `e00af69f66`。fps 21.06／20.97）。

### スレッドごとの時間（FrameTimingManager、何も切らない窓）
| 項目 | ms/フレーム |
|---|---:|
| CPU のフレーム | 48.1 |
| メインスレッド | 40.0 |
| 描画スレッド | 12.0 |
| **GPU** | **5.5** |

GPU はほぼ空いている。描画を速くするには、GPU の処理（SSAO・影の解像度・ポストプロセスなど）を軽くしても効かず、メインスレッドの処理を減らす必要がある。

### パスを1つずつ切った窓（GRD の後、修正後の `pass-toggle.cs`）
| 切ったもの | URP ms | メインスレッド ms | GPU ms | ドローコール | SetPass |
|---|---:|---:|---:|---:|---:|
| なし | 14.6 | 40.0 | 5.6 | 9,942 | 500 |
| 影 | 11.0 | 35.0 | 5.1 | 7,795 | 427 |
| SSAO | 14.3 | 38.9 | 6.0 | 9,934 | 495 |
| アウトライン（`OutlinePass`） | 14.6 | 39.9 | 6.5 | 9,902 | 463 |
| カメラの深度・不透明テクスチャの要求 | 14.8 | 40.6 | 5.6 | 9,947 | 501 |
| ポストプロセス | 14.7 | 40.4 | 5.6 | 9,945 | 499 |

実行時の Renderer Feature は `ScreenSpaceAmbientOcclusion`・`OutlinePass`・`UiSrgbComposite` の3つで、`ScreenSpaceOutlines` は無かった。

### メインスレッドのカメラ描画（Profiler、`RenderPlayModeViewCameras` 15.7ms の内訳）
| 処理 | ms | 中身 |
|---|---:|---|
| カリング（`CullScriptable`） | 5.5 | 描画対象の取り出し 3.1（うち `QueuePrepareIntegrateMainThreadObjects` 2.2）、`SceneCulling` 1.7（うち GRD のカリング 0.9）、地形 0.3 |
| 提出（`ScriptableRenderContext.Submit`） | 7.4 | 半透明の描画の組み立て 2.2、不透明 1.7、影の準備 1.8（うち `QueuePrepareIntegrateMainThreadObjects` 1.3）、影の描画 0.8 |
| URP のパスの組み立て（`ScriptableRenderer.Execute`） | 1.3 | |
| 影を落とす物のカリング | 0.3 | |

- `QueuePrepareIntegrateMainThreadObjects` が、カリングと影の準備の中で合わせて約3.5ms。描画対象を取り出すときに、ジョブで処理できない Renderer をメインスレッドで取り込む処理と見ている（推測。どの Renderer かは未確認）。
- 半透明（2.2ms）が不透明（1.7ms）より重い。半透明は GRD でまとめて描かれず、1つずつ組み立てていると見ている（推測）。
- 描画スレッド（Profiler、1フレームの壁時計）では、`DrawBuffersBatchMode` 9.1・`RenderLoop` 7.1・GPU スキニング 4.6・`ResourceBarriers` 3.8・GPU 待ち 9.8。
- ジョブのワーカーはほとんど空いている（各ワーカーの大半が `Idle`）。
- カメラ描画の外の約24ms（スクリプト・受信処理・Editor など）が、メインスレッド 40ms の残り。

## 見た目
`SHOT=1` で同じ視点のゲーム画面を撮った（`results/*_before-grd.png`・`*_after-grd.png`）。違うのは高架のベルト上を流れるアイテムの位置（撮った時刻の違い）だけで、地形・ブロック・影・アウトライン・UI は同じに見える（目視）。

## 確かめていないこと
- グラフィックスジョブあり（Split、6000.3.8f1 の既定）での値と、GRD で GPU 停止が起きやすく／起きにくくなるか。
- ビルドしたプレイヤー（Release）・Mac（Metal）での効果と見た目。`m_BrgStripping` を変えたことによるビルド時間・バリアント数の増え方。
- ワールド読み込み時間への影響（Renderer 約21.6万を GRD に登録する分）。定常になるまでの時間は前後で大きく変わらなかったが、測っていない。
- 設置プレビュー・削除モードなどで材質を差し替える表示が、GRD の後も正しく出るか。
- `Render Thread` の ProfilerRecorder は、この Editor では値が出なかった（全窓 0）。描画スレッドの時間は FrameTimingManager の値を使う。

## 再計測の手順
1. windows-desktop で計測したいコミットを checkout し、グラフィックスジョブなしの Editor（スケジュールタスク `moorestech-unity-gfxmt`）を起動してコンパイルする。Profiler の記録は `snippets/profiler-off.cs` で止めておく。
2. Mac から `[REPEAT=2] [SHOT=1] docs/performance/2026-11-21-draw-call-breakdown/run.sh <label>`。結果は `results/<日時>_<label>_<コミット>.json`。
3. `summarize.py results/*.json` で条件ごとの平均を出す。

## ファイル
| ファイル | 内容 |
|---|---|
| `run.sh` | ワールドを1回読み込み、決まった視点で、fps 窓・Profiler 窓・パスを1つずつ切った描画カウンタの窓を REPEAT 周取る |
| `summarize.py` | 結果 JSON を条件ごとの平均にまとめる |
| `snippets/pass-toggle.cs` | 影・SSAO・アウトライン・カメラテクスチャ・ポストプロセスを1つずつ切る（`run.sh` が COND を書き換えた写しを送る） |
| `snippets/render-start.cs` / `render-collect.cs` | 描画カウンタと FrameTimingManager の CPU・GPU 時間のフレーム平均（2026-11-20 のものに、影を落とす数・三角形・スレッドの時間を足した） |
| `snippets/camtree-collect.cs` | Profiler の記録から、メインスレッドのカメラ描画の階層と、ほかのスレッドのマーカーを出す |
| `snippets/frame-timing-on.cs` / `frame-timing-off.cs` | Play 前に FrameTimingManager を有効にする／ProjectSettings の値（オフ）へ戻す |
| `snippets/grd-check.cs` | GRD が動いているかと、対象外の Renderer の数 |
| `snippets/enable-grd.cs` | GRD を有効にする設定を書いて保存する |
| `snippets/capture.cs` | ゲーム画面を PNG に保存する |
| `snippets/profiler-off.cs` | Profiler の記録を止める（2026-11-19 のものの写し） |
| `results/` | 結果 JSON と画面の PNG |
