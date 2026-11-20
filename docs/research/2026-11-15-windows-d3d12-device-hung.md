# Windows の Unity Editor で重い場所を描くと GPU が止まって落ちる（DXGI_ERROR_DEVICE_HUNG）

調査: 2026-11-15。報告書: 2026-11-20。**原因は未確定。** スキンメッシュがあると起きやすくなることまでは確かめたが、それだけが原因ではない記録がある（下の「食い違う記録」）。

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
