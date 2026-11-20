# Windows の Unity Editor で重い場所を描くと GPU が止まって落ちる（DXGI_ERROR_DEVICE_HUNG）

調査: 2026-11-15、追加調査 2026-11-20〜21。報告書: 2026-11-21。**Unity 6000.3.8f1 の D3D12 の不具合で、6000.3.9f1 で直っている。** 同じコミット・同じワールドと視点・既定の設定のまま、6000.3.9f1 では 314秒・426秒の2回とも止まらなかった（8f1 では5回中5回が 110秒以内に止まる）。止まるのはグラフィックスジョブ（Split／Native）と GPU スキニングの組み合わせのときで、止まる瞬間に GPU は使用率 100%・約190W で約2秒回り続けてから TDR でリセットされる。6000.3.9f1 の修正のうち、DX12 のスクラッチメモリを早く再利用していた不具合（UUM-131824）が当たっていると見ている（下の「6000.3.9f1 での確認（2026-11-21）」）。

## 要約
- ベースラインワールド `2026-10-22-world_1`（15,082ブロック）で、プレイヤーとカメラを決まった位置と向きに移すと、数秒〜数十秒で GPU が応答しなくなる。Windows がドライバーをリセットし（TDR）、Unity Editor が「Unrecoverable D3D12 device error!」で止まるか落ちる。
- master（b3c9c11c5b）でも起きる。設置照準の変更（PR #1670）や、設置モードのプレビューの変更（ADR 0101）とは関係ない。設置モードに入る前の通常の画面で起きる。
- 切り分けでは、影・特定のブロック・Editor のフォーカスは原因ではなかった。ブロックの描画を全部止めると起きない。ブロックの中のスキンメッシュ（4,697個）だけを止めても、30秒以内には起きない。スキンメッシュの数が多いほど起きる。
- ただし、別の計測（2026-11-20、下の「食い違う記録」）では、スキンメッシュを全部止めた状態でも、長い計測の途中で同じエラーが1回起きている。

## 環境
| 項目 | 値 |
|---|---|
| OS | Windows 11 Home |
| CPU | Intel Core i7-12700 |
| GPU | NVIDIA GeForce RTX 3060 Ti（VRAM 8GB）、ドライバー 610.88 |
| Unity | 6000.3.8f1、Editor の PlayMode、D3D12、品質 High、Game ビュー 1920×1080 |
| ワールド | ベンチマークのベースライン `2026-10-22-world_1` |
| 位置と視点 | プレイヤー (622.21, 82.99, 420.90)、三人称・距離 3.5m・下へ 2.52°・方位 321.86°・fov 70（`docs/performance/benchmark/snippets/place-player.cs`。この報告では `evidence/.../snippets/place-player.cs` に写しがある） |
| この視点の描画量 | Batches 約5.1〜5.6万、頂点 約1,900〜2,100万、影を落とすもの 約1.9万、12〜15fps（Game ビューの統計、止まる直前） |

Mac（Apple M3、Metal）では、同じ場所で遊べていた（落ちたという記録は無い）。

## 症状
画面には「Fatal Error! Unrecoverable D3D12 device error! Run with -force-d3d12-debug and see logs for more info.」のダイアログが出て、Editor のメインスレッドが止まる（[crash-dialog.png](../evidence/2026-11-15-windows-d3d12-device-hung/crash-dialog.png)）。メインスレッド以外で起きたときは、ダイアログを出せずに落ち、Unity Bug Reporter が開く。サーバーの刻み（別スレッド）は動き続けるので、Editor.log だけ見ると動いているように見える。

Editor.log（実測、3つのコミットで同じ）:
```
D3D12Fence::Wait(...) error: got 18446744073709551615. Device removal.
d3d12: Device failed error (887a0006).
d3d12: GfxDevice was not out of Local memory
 Budget: 7608467456  CurrentUsage: 1767858176
d3d12: GfxDevice was not out of Non-Local memory
Unrecoverable D3D12 device error! Run with -force-d3d12-debug and see logs for more info.
```
- 887a0006 は `DXGI_ERROR_DEVICE_HUNG`。GPU のメモリは足りている。
- Windows のシステムイベントログに、同じ時刻の `nvlddmkm` Id 153（GPU が応答しなくなりリセットされた）がある。
- 同じログの前の方に `d3d12: upload buffer was too small for the requested resource! Size: 16777216. Requested: 67108864.` が出ている。これとの関係は確かめていない。

## D3D12 デバッグ層（`-force-d3d12-debug`）で取った記録
デバッグ層付きで起動して同じ手順を踏むと、移してから約45秒以内に同じく止まった（[debug-repro.log](../evidence/2026-11-15-windows-d3d12-device-hung/logs/debug-repro.log)、[dred.txt](../evidence/2026-11-15-windows-d3d12-device-hung/logs/dred.txt)）。
- デバッグ層のメッセージ: `ID3D12Device::RemoveDevice: ... DXGI_ERROR_DEVICE_HUNG: The Device took an unreasonable amount of time to execute its commands, or the hardware crashed/hung. As a result, the TDR ... has been triggered.`
- DRED の、止まったときに実行中だった描画のコマンドリストの中身: 描画 212、バッファのコピー 18、「状態の切り替え → コンピュートシェーダー」の組 19、深度のクリア。コピー18回の直後にコンピュートシェーダーが19回続く並びは、GPU でのスキンメッシュの変形（`gpuSkinning: 1`）と見ている（推測。DRED にはどのコマンドで止まったかの印が出ていない）。
- デバッグ層は D3D12 の API の使い方の誤り（`D3D12 ERROR`）を1件も出していない。

## 再現の手順
1. ベースラインワールドを読み込み、Renderer の数が落ち着くまで待つ（[crash-repro.sh](../evidence/2026-11-15-windows-d3d12-device-hung/scripts/crash-repro.sh) の前半。`docs/performance/benchmark/run-buildmode-entry.sh` と同じ読み込み）。
2. `place-player.cs` でプレイヤーとカメラを上の位置と視点へ移す。
3. 数秒〜数十秒で止まる。

スクリプトは調査の作業フォルダ（Git の管理外）に置いて使った写しで、パスはそのフォルダを前提にしている。

## 切り分け（実測、2026-11-15）
どれも上の手順で読み込み、定常を待ってから、条件を入れて同じ位置・視点へ移した。「止まらない」は、観察した時間（表の右の列）の間に止まらなかったという意味で、それより長く安全だとは言えない。

| # | コミット | 条件 | 結果 | 観察時間 | 記録 |
|---|---|---|---|---|---|
| 1 | 784bb70d16 | そのまま（2回） | 止まる | — | 計測の結果ファイル（削除済み）と画面 |
| 2 | 49fbb72f77 | そのまま | 止まる | 60秒以内 | crash-repro-49fbb72f77.log |
| 3 | master b3c9c11c5b | そのまま | 止まる | 60秒以内 | crash-repro-master.log |
| 4 | 784bb70d16 | 影を全部切る（品質設定とライト3つ） | 止まる | 45秒以内 | bisect.log |
| 5 | 784bb70d16 | ブロックの Renderer を全部切る（230,194個） | 止まらない | 45秒 | bisect.log |
| 6 | 784bb70d16 | カメラは動かさずプレイヤーだけ移す（向きは初期の 20°, 0°） | 止まらない | 45秒 | bisect.log |
| 7 | 784bb70d16 | 5 の後、映るブロック種類を頂点の多い順に1種類ずつ戻す | 8種類（約2,200万頂点）までは止まらず、9種類目の原始的な粉砕機（17台・約66万頂点）を戻すと止まる | 各13〜15秒 | session-bisect.log、analyze-session.txt |
| 8 | 784bb70d16 | 5 の後、原始的な粉砕機だけを戻す → 粉砕機以外の全種類を戻す | 粉砕機だけでは止まらず、粉砕機以外を戻すと止まる | 各12秒〜 | session-bisect2.log |
| 9 | 784bb70d16 | ブロックの中のスキンメッシュだけを切る（4,697個）→ 戻す | 切っている間は止まらず（`focused=True`）、戻すと止まる | 20秒 | skinned-session.log |
| 10 | 784bb70d16 | 9 の後、スキンメッシュをブロック種類ごとに少ない順に戻す | 精錬炉 38・油井 287・ガソリンエンジン発電機 374 個までは止まらず、電気汎用工作装置 1,710 個を戻すと止まる | 各15秒 | skinned-type-session.log |
| 11 | 784bb70d16 | 9 の後、蒸気機関 2,288 個だけを先に戻す | 止まる | 15秒以内 | skinned-order-session.log |
| 12 | 784bb70d16 | Editor の `PlayerSettings.gpuSkinning` を切ってから（CPU で変形）、何も止めずに移す | 止まらない（`focused=True`） | 30秒 | noskin-gpu-session.log |
| 13 | 784bb70d16 | `-force-d3d12-debug` で起動し、そのまま | 止まる | 約45秒以内 | debug-repro.log、dred.txt |

- 7・8 から、特定のブロックではなく、映る量が多いと起きる。
- 9〜11 から、スキンメッシュを持つブロックの種類によらず、映るスキンメッシュが多いほど起きる（699個では起きず、1,710個・2,288個で起きた）。
- 9〜12 では、Editor が前面にあること（`Application.isFocused`）を毎回確かめている。フォーカスは原因ではない。
- 12 で切り替えた設定は、試した後に元へ戻し、ProjectSettings には保存していない。

スキンメッシュを持つブロック（ワールド全体、[skinned-type-session.log](../evidence/2026-11-15-windows-d3d12-device-hung/logs/skinned-type-session.log)）:

| ブロック | 台数 | スキンメッシュ | 頂点 | 最大ボーン数 |
|---|---:|---:|---:|---:|
| 蒸気機関 | 52 | 2,288 | 383,812 | 3 |
| 電気汎用工作装置 | 90 | 1,710 | 982,980 | 22 |
| ガソリンエンジン発電機 | 11 | 374 | 334,675 | 11 |
| 油井 | 7 | 287 | 91,140 | 8 |
| ふいご付き精錬炉 | 19 | 38 | 35,264 | 6 |

プレハブのスキンメッシュは、品質 自動（`m_Quality: 0`）・画面外では更新しない・スキンのモーションベクター有効（`m_SkinnedMotionVectors: 1`）だった（電気汎用工作装置 `WeldingProcessingMachine.prefab` で確認）。

## 食い違う記録
2026-11-20 のパイプの半透明の計測（ブランチ `perf/fluid-pipe-transparent-setpass` の `docs/performance/2026-11-20-fluid-pipe-transparent-setpass/README.md`「Windows のデバイス喪失」）では、同じ位置・視点で、プレイヤー以外のスキンメッシュ 4,701個を全部切った状態でも、計測3回目の途中で同じ 887a0006 が起きている。この報告の 9・12 の「止まらない」は 20〜30秒しか見ていないので、この記録とは矛盾しない。

したがって、言えるのは次の2つまで。
- スキンメッシュがあると、移してから数秒〜十数秒で起きる。切ると、少なくとも数十秒は起きない。
- スキンメッシュを切っても、長く描いていると起きることがある。

## 追加の切り分け（2026-11-20）
11-15 の「止まらない」は観察 15〜30秒で、今回の再現（移してから 12〜110秒で止まる）より短く、決め手になっていなかった。そこで条件ごとに 300秒以上見た。どれもコミット 784bb70d16、上と同じ読み込み・定常待ち・位置と視点。10秒ごとに uloop で応答と `Application.isFocused` を見た（全回 `focused=True`）。

| 条件 | Editor の描画スレッド（Editor.log） | メッシュ変形 | 結果 | 記録 |
|---|---|---|---|---|
| そのまま | `kGfxThreadingModeSplitJobs` | GPU（Batched、既定の `meshDeformation: 2`） | 22〜57秒で止まる | batched-1.log |
| メッシュ変形を GPU（非バッチ）に | Split | GPU（`meshDeformation: 1`） | 36秒以内に止まる | gpu-1.log |
| VFX Graph（`VisualEffect` 278個）を全部止める | Split | GPU（Batched） | 12〜56秒で止まる | novfx-1.log |
| 全スキンメッシュの `skinnedMotionVectors` を切る | Split | GPU（Batched） | 12〜107秒で止まる | nomv-1.log |
| メッシュ変形を CPU に | Split | CPU（`meshDeformation: 0`） | 305秒止まらない | cpu-1.log |
| `-force-gfx-mt`（グラフィックスジョブなし） | `kGfxThreadingModeThreaded` | GPU（Batched） | 309秒・426秒、2回とも止まらない | gfxmt-1.log、gfxmt-2.log（末尾のみ） |
| `-force-gfx-jobs native` | `kGfxThreadingModeClientWorkerNativeJobs` | GPU（Batched） | 46秒以内に止まる | gfxnative-1.log |

- メッシュ変形の切り替えは、起動中の Editor で PlayerSettings の `meshDeformation` を SerializedObject で書き換え、Play の前に行った（ProjectSettings には保存していない。`snippets/2026-11-20/set-mesh-deformation.cs`）。
- 止まった回はどれも Editor.log に `D3D12Fence::Wait ... Device removal.` と `Device failed error (887a0006)`（`logs/2026-11-20/editor-*-tail.txt`）。
- `upload buffer was too small（16MB に 64MB を要求）` は、止まらなかった CPU の回にも出ている。今回の停止とは関係ないと見ている。
- 回数は条件ごとに1〜2回。グラフィックスジョブありで GPU スキニングのときは5回中5回が 110秒以内に止まり、グラフィックスジョブなしは計 735秒で0回なので、差ははっきりしている。ただし fps は 13〜14 から 11〜12 に下がる。

### ここから言えること
- 停止は、グラフィックスジョブ（Split でも Native でも）で描いているときに起き、グラフィックスジョブなし（描画スレッド1本）では起きない。
- グラフィックスジョブありでも、スキニングを CPU にすると起きない。GPU スキニングの Batched／非バッチ、スキンのモーションベクター、VFX Graph は関係ない。
- 11-20 のパイプの計測で、スキンメッシュを切っても長い計測で1回起きた件は、グラフィックスジョブありの状態で、プレイヤーのスキンメッシュ（GPU スキニング）が残っていたか、GPU スキニング以外にも同じ経路に乗る処理があるか、どちらか（未確認）。
- アプリのコードではなく、Unity 6000.3.8f1 の D3D12 のグラフィックスジョブで GPU スキニングのコンピュートを記録・投入する経路の不具合（同期の抜けなど）か、ドライバー 610.88 との組み合わせの問題と見ている（推測。DRED に止まった位置が出ず、これ以上は Unity の外から追えない）。完全に一致する既知の不具合は Unity の issue tracker では見つからなかった。

### 止まる瞬間の GPU（2026-11-20）
グラフィックスジョブあり・GPU スキニングの既定で、FrameTimingManager のフレームごとの時間（`snippets/2026-11-20/frame-log-start.cs`、Play 前に `frame-timing-on.cs` で `enableFrameTimingStats` を一時的に入れる）と、`nvidia-smi` の 0.2秒ごとの記録を取った（`logs/2026-11-20/frames-timing-1.csv`、`smi-timing-1.csv`）。
- 止まる直前まで、GPU のフレーム時間は 1〜25ms。CPU が 1フレーム約65ms で律速していて、GPU には余裕がある。GPU 時間が少しずつ伸びていく様子は無い。
- GPU の使用率はふだん 10〜26%・35〜52W。止まった瞬間（23:38:41.0）に **100%・1,860MHz・191W** に跳ね、約2秒そのままで、0% に落ちた（TDR）。
- つまり GPU は何かを待って止まっているのではなく、終わらない処理を全力で実行している。壊れた値（スレッド数・描画数・ループ回数など）を持つコマンドが1つ GPU に入った形に合う。
- Windows のシステムログの `nvlddmkm` Id 153 は、5回とも移してから 8〜30秒（多くは 25〜30秒）。
- `upload buffer was too small` は、止まらなかった `-force-gfx-mt` の回にも出ていたので関係ない。
- `-force-d3d12-debug -force-d3d12-debug-gbv`（GPU ベースの検証）でも試した。移して数秒で止まり、その前に検証のエラーは1件も出なかった（`logs/2026-11-20/gbv-1.log`）。GBV は描画を大きく遅くするので、この結果からは何も言えない。

### 6000.3.9f1 での確認（2026-11-21）
ほかに同じ症状の報告が無いかを調べた。完全に一致する報告（スキニング × グラフィックスジョブ × DX12 で TDR）は見つからなかったが、近いものは次のとおり。
- [UUM-131824](https://issuetracker.unity3d.com/issues/crash-on-checkdevicestatus-when-moving-in-the-scene-view-on-a-specific-scene)：重いシーンを約1分描くと DX12 だけで落ちる。スタックは今回と同じ `D3D12Fence::Wait` → `QueuePresent`。6000.3.2f1 から起き、**6000.3.9f1** で「DX12 のスクラッチメモリを早く再利用していた」として直った（リリースノート「DX12: Fixed crashes related to scratch memory allocation.」）。[フォーラムのスレッド](https://discussions.unity.com/t/dx12-graphics-crash-in-editor-force-dx11-fixes-it/1703990)で、DEVICE_HUNG の報告に対し Unity の社員がこの修正を挙げている。
- [UUM-147012](https://issuetracker.unity.com/issues/23402/editor-device-removal-crash-on-checkdevicestatus-when-editor-is-under-heavy-load)：GPU スキニングの 2D キャラで負荷をかけると DX12 で落ちる。NVIDIA 610.74。Open。グラフィックスジョブを切った確認は無い。

そこで windows-desktop に Unity 6000.3.9f1 を入れ、別のクローン `G:\GitHub\moorestech-unity6000.3.9`（コミット 784bb70d16、client-private 1d6e1201、master データは元と同じ `G:\GitHub\moorestech_master` を共有）をバッチモードで読み込み直してから、同じ手順で見た。8f1 からの差は ProjectVersion と、エディターが自動で上げた memoryprofiler 1.1.11・ui.test-framework 6.3.0 だけ（URP・VFX Graph は 17.3.0 のまま）。

| Unity | 描画スレッド | メッシュ変形 | 結果 | GPU 使用率の最大 | fps | 記録 |
|---|---|---|---|---|---|---|
| 6000.3.8f1 | Split | GPU Batched | 5回中5回、110秒以内に止まる | 100%（止まる瞬間） | 13〜14 | 上の表 |
| **6000.3.9f1** | **Split** | **GPU Batched** | **314秒・426秒、2回とも止まらない** | 57%・84% | 13〜15 | `logs/2026-11-21/` |

fps は 8f1 と変わらないので、描画が遅くなったから起きなかったのではない。

### 原因のまとめ
- 原因は moorestech のコードではなく、Unity 6000.3.8f1 の D3D12 の不具合。6000.3.9f1 で直っている。
- 9f1 の DX12 の修正のうち、どれが効いたかまでは切り分けていない。スクラッチメモリの再利用（UUM-131824）だと見ている。グラフィックスジョブのワーカーが記録したコマンドの定数・アップロード領域を、GPU が使い終わる前に別のデータで上書きすると、GPU スキニングのコンピュートや描画が壊れた値で走って終わらなくなる、という流れなら、グラフィックスジョブのときだけ起きること、GPU スキニングの量が多いほど起きやすいこと、止まる瞬間に GPU が全力で回っていることの3つと合う。（推測）
- 11-20 のパイプの計測で、スキンメッシュを切っても1回起きたのも、同じ不具合で説明できる（スクラッチメモリはスキニング以外の描画も使う。推測）。

### 対策
Unity を 6000.3.9f1 以降へ上げる。上げられない間の回避は `-force-gfx-mt`（Editor）か、Windows のグラフィックスジョブを切ること（ビルド）。

### ビルドへの影響
`ProjectSettings.asset` の `m_BuildTargetGraphicsJobs` で WindowsStandaloneSupport は `1`（グラフィックスジョブあり）、モードの指定は無い（D3D12 の既定）。ビルドした Windows のゲームも、同じ視点で同じことが起きる可能性が高い（未確認）。

### 次に試すこと（候補、未実施）
1. プロジェクトの Unity を 6000.3.9f1 以降へ上げる（パッケージの更新やテストを含めた別の作業）。上げるなら最新の 6000.3 の方が、ほかの DX12 の修正（6000.3.15 のフェンスのリーク、6000.3.28 の描画量が極端なときのデバイスリセットなど）も入る。
2. 上げた後に、ビルドした Windows のゲームでも同じ視点で止まらないことを確かめる。

スクリプト: `scripts/2026-11-20/session.sh <meshDeformation> <観察秒数> <tag> [止めるスニペット]`。環境変数 `UTASK` で、Unity を起動するスケジュールタスク（`moorestech-unity`／`-gfxmt`／`-gfxnative`。引数だけが違う）を選ぶ。

## 以前の D3D12 の調査との関係
`docs/research/2026-10-01-d3d12-crash/README.md`（プレイテスターの配布版で「機械を動かしてたりしたら」落ちた件。原因未確定で終了）は、エラーが `80004005`（コマンドリストの Close 相当が E_FAIL）で、今回の `887a0006`（DEVICE_HUNG）とは違う。機械の動きはスキンメッシュのアニメーションなので、関係する可能性はあるが、確かめていない。

## わかっていないこと
- GPU のどの処理が止まっているのか（DRED に止まった位置の印が出ていない）。
- スキンメッシュを切っても起きる場合の、残りの原因。
- 「upload buffer was too small（16MB に 64MB を要求）」との関係。
- ビルドした Windows のゲームでも起きるのか（Editor でしか試していない）。
- ほかの GPU・ドライバーでも起きるのか（この1台でしか試していない）。

## 次に試すこと（候補、未実施）
1. スキンのモーションベクターを切る（プレハブを保存せず、起動中の Editor で全スキンメッシュの `skinnedMotionVectors` を切る）。GPU での変形のバッファが半分になる。
2. スキンメッシュを切った状態で、数分以上描き続けて、起きるまでの時間を測る。スキンメッシュ以外の要因を探す出発点にする。
3. 同じ位置・視点を、ビルドした Windows のゲームで試す。
4. 蒸気機関・電気汎用工作装置のアニメーションが、スキンメッシュでなくても作れるか（ボーンが3本の蒸気機関は、Transform を動かす形で足りる可能性がある）を見る。

## 影響
- この位置・視点を使う計測（`run-buildmode-entry.sh`、向きごとの計測）は、この Windows 機では今のところ回せない。2026-11-20 のパイプの計測は、スキンメッシュを切って回している。
- プレイヤーの Windows 環境で同じことが起きるかは分からない（ビルドで未確認）。

## 証拠
`docs/evidence/2026-11-15-windows-d3d12-device-hung/`
- `snippets/`: 切り分けに使った `execute-dynamic-code` のスニペット（`place-player.cs`、ブロック・影・スキンメッシュを止める・戻す、映るブロックの集計、GPU スキニングの切り替え、前面の確認）
- `scripts/`: 読み込みから切り分けまでを回したシェルスクリプト、ウィンドウを出さずに画面を撮る・Editor を前面に出すための PowerShell と VBScript
- `logs/`: 各実験のログ、DRED の抜き出し、映るブロックの集計
- `crash-dialog.png`: 止まったときの Editor の画面
