# Unity 6000.6.10f1 版上げ後の描画崩れ 再現調査（第2回）

調査日: 2026-11-24〜25 ／ タスク: `moorestech-v5oa7` ／ 前回: [2026-11-23-electric-wire-render-corruption.md](2026-11-23-electric-wire-render-corruption.md)

## 結論と現状

**崩れは再現できず、原因は未確定のまま。コードと設定は変更していない。**

ユーザーの録画（2026-11-24 15:59、world_1）にある巨大な黒白の三角形と髪の伸びを、Mac mini（M4）でも MacBook（M5）でも再現できなかった。再現できないため、どの修正案も効いたかどうかを判定できない。調査はここで打ち切った。

## 第3回の追加調査（2026-11-26 未明）

**崩れは今回も再現できなかった。代わりに、Play 中に Editor のメモリが際限なく増える現象と、その主な原因を実測で特定した。崩れとの因果は未検証。**

### Play 中のメモリ急増（両機で再現）

| 条件 | Play 開始後の Editor の footprint |
|---|---|
| Mac mini・素の worktree（804edc2953）・Profiler ウィンドウが開いて記録中 | 30秒 7GB → 90秒 21GB → 3分 31GB（上限で停止） |
| Mac mini・素の worktree・Profiler ウィンドウを閉じ記録を止める | 2分半で 15GB に達し、以後約6分間横ばい（Unity の確保は 6.3GB で一定）。1回だけの計測 |
| Mac mini・MacBook の中身をそろえた worktree・記録は止めたがウィンドウは開いたまま | 3分で 24GB（上限で停止）。この中身で横ばいになるかは未確認 |
| Mac mini・MacBook の中身をそろえた worktree・Profiler 記録中・上限なし | 約4分で 62GB。macOS の「アプリケーションメモリが不足」が出て cmux が一時停止された |
| MacBook・本体の丸ごと複製・Profiler 記録中 | 30秒 18GB → 60秒 23GB（上限で停止）。別の回は 34 分で 52GB |
| MacBook・ユーザーの Editor（12時間起動） | 52〜55GB（うち 50GB は圧縮・スワップ）。macOS のメモリ不足ダイアログが出て、Chrome・Slack・cmux などが一時停止されていた |

- 増えている最中のスタックは、Profiler ウィンドウが記録フレームを解析する処理（`profiling::ProfilerFrameDataDeserializer`）が確保の大半を占めた（Mac mini で sample）。MacBook の複製の停止中の sample にも同じ解析処理と Profiler の書き出しスレッドがあった。
- Profiler ウィンドウは `UserSettings/Layouts`（Library を消しても残る、git worktree には付いてこない）と、マシン共通の既定レイアウトの両方に入っていた。MacBook の本体の `UserSettings/Layouts/CurrentMaximizeLayout.dwlt` にもある。
- Profiler を閉じた素の worktree でも 15GB 使う。MacBook の中身をそろえた場合に Profiler を閉じてどこまで増えるかは測れていない（`-gfx-ring-buffer-size 1` の回は 19GB だったが、この起動条件自体がメモリを増やすため参考にならない）。

### MacBook だけで出る手がかり

- **MacBook では Unity が GPU の page fault を起こし、GPU が再起動している。** macOS の診断記録 `/Library/Logs/DiagnosticReports/gpuEvent-Unity-*.ips` が 11/25 の 12:03・15:55・16:07・16:07 の 4 件あり、すべて `restart_reason_desc: "BIF0 page fault"`（GPU が対応付けの無いアドレスを読んだ。`is_read: true`）。どの Unity が起こしたか: 15:55・16:07・16:07 は本体の Editor（PID 1499、14:29 起動。Claude はその時間帯に MacBook を操作していない）。12:03 は `~/moorestech-copy` の Editor（11:56〜12:39 起動）か本体の Editor（PID 49696。12:07 に稼働を確認）のどちらか（推定。同じ秒に Chrome Helper の gpuEvent もある）。11/26 に Claude が MacBook で動かした実験は含まれない。**訂正（11/26 10:20）:** `/Library/Logs/DiagnosticReports/Retired/` に 11/23 10:43:25 の `gpuEvent-Unity`（同じ BIF0 page fault・`is_read: true`）があった。崩れを見た 11/23 10:45〜10:53 の 2 分前で、時刻が合う。11/24 15:59 の崩れについては gpuEvent が見つかっていない（古い報告が消されたかは不明）。Mac mini には `Retired` を含めて Unity の gpuEvent が 0 件。page fault が崩れの直前に起きた例が 1 件あるが、因果はまだ確かめていない。15:55 前後はユーザーが操作中だった（powerd の user active が連続）。Mac mini には Unity の gpuEvent が 1 件も無い（今回メモリ圧迫まで回した後も 0 件）。写しは [evidence-2026-11-26/macbook-gpuEvent-Unity.txt](evidence-2026-11-26/macbook-gpuEvent-Unity.txt)。
- Unity が認識する GPU の機能は両機で同じ（Apple GPU Family 9、MSL 3.0、Mac GPU Family 2）。Unity 側は同じ描画経路を通っていて、違うのは GPU とドライバだと考えられる（推測）。
- Mac mini で Metal の API 検証とシェーダー検証（`MTL_DEBUG_LAYER=1`・`MTL_SHADER_VALIDATION=1`）を有効にして起動し、Play を約 1 分回したところ、範囲外読み込みや失敗アサーションの報告は 0 件だった（メモリ圧迫で自動停止したため短い）。
- 電線だけを切り出した試験（空のシーンに `CatenaryWireMeshBuilder` で 541 本、属性は Position・Normal のみ、`GearChainLine.mat`〈URP Lit〉で 60 回描画、Metal の API 検証・シェーダー検証あり）を Mac mini（M4）と MacBook（M5、本体の複製・Play なし）で回した。両機とも、描画あり（M5 で背景以外の画素 27,552）・検証の報告 0 件・GPU 側の頂点バッファ読み戻しと CPU 頂点の不一致 0・新しい gpuEvent 0。電線メッシュ単体では UV・接線の欠落による範囲外読み込みは起きない。ただし MacBook でも worktree では崩れないと報告されているので、弱い否定にとどまる。スクリプトは [evidence-2026-11-26/wire_gpu_readback_test.cs.txt](evidence-2026-11-26/wire_gpu_readback_test.cs.txt)。
- MacBook（32GB）で、本体の複製・ミラーと同じワールド・Profiler を閉じた状態で Play を 3 回試した（11/26 10:00〜10:40、本体の Editor を閉じた後）。シェーダー検証あり（`MTL_SHADER_VALIDATION=1`。有効になったことを示す行は Editor.log に出ないので未確認）では、読み込み中に Editor が 14GB→23.5GB へ増え、カメラが出る前に安全装置で止めた。検証なしでも読み込み中に 20GB・メモリ圧迫レベル 2 が 20 秒続いて止めた。どの回も検証の報告 0 件・新しい gpuEvent 0 件（`Retired` を含めた件数が 6 のまま）。ゲーム画面まで届いていないので、崩れの否定材料にはならない。MacBook でこのワールドを遊ぶと、ふだんからメモリ圧迫レベル 2 以上で動いていることになる（推測）。
- GPU の page fault の時刻に、メモリ枯渇を示すシステムログは見つからなかった。12:03 は Editor 起動から間もない。メモリ枯渇が条件という仮説には反する材料。
- `Ran out of Graphics Ring Buffer space` は、MacBook の本体のログで Play 1 回ごとにほぼ 1 回（Game ビュー最大化の描画中、`ScriptableRenderContext.Submit`）。ユーザー作の `~/moorestech-copy` のログにも 1 回。Mac mini の通常の起動では 0 回。
- Mac mini でも `-gfx-ring-buffer-size 1` で起動すると同じ警告が出たが、撮影した 6 枚に崩れは無かった（短時間で、メモリ上限で止めたため判定は弱い）。
- MacBook の複製の Editor が Play 中に止まり、GPU への投入（カーネル内の `IOGPUCommandQueueSubmitCommandBuffers`）と読み戻し（`waitUntilCompleted`）で 25 分以上待ち続けた。GPU 使用率は 0%、システムログに GPU エラーは無し。このとき 52GB の Unity が 2 本、32GB の機械で動いていたので、メモリ不足との切り分けはできていない。

### 除外できたもの

- 動的バッチング: URP の全画質設定で `m_SupportsDynamicBatching: 0`。
- Metal の `invalid return type 'Mtl_VertexOut'`（lilToon の IDMask 頂点シェーダー）: 両機のログに出るため識別子にならない。

### 説明できていないもの

- client-private の TerrainData（`GeneratedTerrains`）: Mac mini の mirror の Play 中は Terrain コンポーネントが 0 個だった。一方、地形の prefab は MainGame から有効な状態で参照されており、前回の調査でも `Environment/Terrain` があった。食い違いの理由は調べていない。
- MagicaCloth: メッシュを毎フレーム書き換えるのは上着 1 個だけで、髪は対象外という読解（Fable、未検証）。単独では電線と Editor の文字を説明できないが、上着の書き換えが引き金という仮説は残る。

### 崩れの確認がどれだけ薄いか

今回、崩れの有無を画面で確かめたのは、MacBook の複製で 8 枚（録画と別の場所、Game ビュー最大化なし）と、Mac mini で 6 枚（`-gfx-ring-buffer-size 1`）だけ。「再現しなかった」は弱い根拠でしかない。

### 未検証の仮説

本命: Unity 6000.6.10f1 の Metal 描画が、M5 の GPU で不正なアドレスを読む（page fault の記録あり。11/23 は崩れの 2 分前に page fault）。対応付けのある範囲で不正に読めば巨大な三角形や文字化けになり、対応の無い範囲なら GPU 再起動になる（推測）。対抗: Profiler の記録によるメモリ枯渇で GPU 側のデータが壊れる（page fault の時刻に枯渇の記録が無く、弱まった）。確かめるには、MacBook で Profiler ウィンドウを閉じ、Editor を起動し直してから、いつもどおり遊んで崩れるかを見る。ユーザーの Editor が 55GB を抱えたままでは、MacBook で別の Editor を安全に動かせないため、今回は実行していない。

## 症状（ユーザー録画より）

| 項目 | 内容 |
|---|---|
| 見た目 | 画面を覆う黒と白の巨大な三角形、キャラクターの髪が伸びる |
| 崩れていた物 | 電線（実行時生成メッシュ、位置と法線のみ、URP/Lit）と、手に持つ斧（MeshRenderer）。髪（SkinnedMesh）は崩れの見え方から関与を推測しただけで未確認 |
| 環境 | MacBook（Apple M5）、Metal、Unity 6000.6.10f1、URP 17.6 |
| ワールド | `world_1`（ユーザー提供の zip） |

## 試したことと結果

すべて world_1 を zip から展開した複製で行い、ユーザーのセーブと設定は触っていない。

| 機種 | 条件 | 結果 |
|---|---|---|
| Mac mini（M4） | PlayMode 起動、各方向の見回し | 崩れなし |
| Mac mini（M4） | GPU Resident Drawer オンで起動し、セーブ上のプレイヤー位置（570, 84, 517）で16方向を見回して録画（48秒） | 崩れなし、エラーログ0件 |
| MacBook（M5） | 起動直後 | 崩れなし |
| MacBook（M5） | 高所からの見下ろし | 崩れなし |
| MacBook（M5） | 2990x1922 で39枚の連続撮影 | 崩れなし（画面の黒・白の割合を機械判定） |
| MacBook（M5） | PlayMode の開始と停止を繰り返す | 1〜2周目は崩れなし。3周目で画面スリープにより Unity が固まり、4周目は撮影が一部欠けた |

## 原因の仮説（すべて推測）

| 仮説 | 手がかり | 反する材料 |
|---|---|---|
| GPU Resident Drawer（版上げと同時の 35b4f8e9fb で有効化）が M5 で描画データを壊す | 版上げ直後から発生。M5 でだけ出る | 前回調査で実行中に GRD をオフにしても板は残った（形が毎フレーム変わった）。起動時からオフの比較はしていない |
| Graphics Ring Buffer の枯渇 | 前回 `Ran out of Graphics Ring Buffer space` が1回出た | 崩れとの前後関係は未確認 |
| Unity 6000.6.10f1 と Metal（M5）の不具合 | M4 で一度も出ない | 一致する公開不具合は見つからなかった（外部AI相談でも同じ結論） |

## 次に再開するときの手順

1. 崩れが出たときの操作を特定する。ユーザーが普段の遊び方で再現したときの直前操作（飛行モード、長時間プレイ、ウィンドウ操作など）とログを取ってもらうのが最短。
2. M5 で再現したら、起動時から GPU Resident Drawer をオフにした状態と比べる。オフは URP アセット `UniversalRP-{High,Medium,Low}Quality.asset` の `m_GPUResidentDrawerMode` を 0 にする（Editor 経由で変更すること）。
3. オフで消えたら、全体を切るか、電線と斧だけを GRD の対象から外すかを決める。

## 注意（やってはいけないこと）

- `-gfx-ring-buffer-size 1` で Unity を起動すると、メモリを 37GB まで使って他のアプリが止まった。小さい値で試さない（第3回でこの注意を読まずに再実行し、Mac mini でメモリ不足ダイアログを再び出した）。
- Profiler ウィンドウを開いたまま Play に入らない。数分で数十 GB に膨らみ、macOS が他のアプリを一時停止する。自動の検証では Play の前に `ProfilerWindow` を閉じ `ProfilerDriver.enabled = false` にし、Editor の footprint に上限（Mac mini では 16GB）を付けて監視する。
- MacBook で長時間の自動操作をするときは、画面のスリープを止める（`caffeinate -u -d`）。止めないと Unity が固まる。

## 参照先

| 種類 | 場所 |
|---|---|
| タスク台帳 | `moorestech-v5oa7` |
| 前回の調査 | [2026-11-23-electric-wire-render-corruption.md](2026-11-23-electric-wire-render-corruption.md) |
| GRD 有効化 | 35b4f8e9fb（PR #1700） |
| 電線メッシュの生成 | `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/StateProcessor/ElectricWire/CatenaryWireMeshBuilder.cs` |

## 崩れた瞬間の実測（2026-11-26 12:10〜12:14、ユーザーの MacBook の本体 Editor）

ユーザーが「壊れた」と知らせた直後に、崩れた状態のまま読んだ値。

| 項目 | 値 |
|---|---|
| Editor 起動 | 12:02:14（崩れまで約 8 分） |
| GPU 再起動（gpuEvent-Unity, BIF0 page fault） | 12:10:36・12:10:50 の 2 回 |
| Editor の phys_footprint | 53GB |
| スワップ | 24.6GB 中 23.6GB 使用、メモリ圧迫レベル 2 |
| Profiler ウィンドウ | 0 個（開いていない） |
| `ProfilerDriver.enabled` | True、1,999 フレーム記録中 |
| Unity の確保量 | totalAlloc 33.6GB・reserved 51.7GB・GPU ドライバ 7.6GB |
| Editor.log | `Ran out of Graphics Ring Buffer space`（MaximizedHostView の Game ビュー描画中） |

- Game ビュー（[崩れ](evidence-2026-11-26/macbook-broken-1212-game.png)）: ツールバーの「Game」「Display 1」・解像度の文字が消え、「Stats」が「tats」になる。ゲーム画面に白い筋と黒い大きな影。
- メッシュ 1,500 個（電線 5 本を含む）の GPU 頂点バッファを読み戻すと、CPU の頂点と全件一致。頂点バッファの中身は壊れていない。
- SkinnedMeshRenderer の GPU 出力と BakeMesh は 1〜3 ずれたが、座標の基準の違いの可能性があり、崩れていない環境で同じ比較をしていないので判断保留。
- `ProfilerDriver.enabled = false` と全フレーム消去で、footprint は 53GB→35GB、圧迫レベルは 2→1 に戻った。**崩れは戻らなかった**（[停止後](evidence-2026-11-26/macbook-broken-1213-after-profiler-off.png)）。
- ウィンドウ無しで記録が有効だった理由: MacBook の EditorPrefs に `Profiler.RememberLastRecordState = 1`・`ProfilerEnabled = 1` がある。以前の記録状態が残り、Play のたびにウィンドウ無しで記録が始まっていた（推測。プロジェクトのコードに記録を有効にする箇所は無い）。

### 12:14 時点の見立て（推測。12:35 の A 回で否定された）

1. Profiler の記録が既定でオンになっていて、Play 中に Editor のメモリが数十 GB まで増える。
2. MacBook（32GB）のメモリとスワップが逼迫し、GPU が対応付けの無いアドレスを読んで再起動する。
3. 再起動で GPU 側だけにあった資源（フォントのテクスチャ等）の中身が失われ、Unity は気づかずに描き続ける。頂点バッファは CPU と共有のメモリなので無事。崩れは Editor を再起動するまで残る。

worktree で崩れないのは、ワールドの規模や Play の長さが違い、ここまで逼迫しないためと考えられる（未確認）。

## 本体での比較（2026-11-26 12:32〜13:10、Claude が MacBook 本体の Editor をワールド写しで起動）

| 回 | 条件 | 結果 |
|---|---|---|
| A | Profiler 記録オフ（`ProfilerEnabled=0`）、既定のリングバッファ | Play 開始から約 1 分でユーザーが崩れを確認。footprint 24〜26GB・圧迫レベル 2。**新しい gpuEvent 0 件**。`Ran out of Graphics Ring Buffer space` が 12:36:08 に 1 回 |
| C・C2 | 記録オフ、`-gfx-ring-buffer-size 256` | 2 回とも Play 開始から約 20 秒で同じ警告。その後 Editor が無応答（CPU 140% で回り続ける） |

- A により、**Profiler の記録も GPU の再起動も崩れの必須条件ではない**。崩れたすべての回で共通して出ているのは `Ran out of Graphics Ring Buffer space` だけ（Mac mini では既定設定で一度も出ていない）。
- C の無応答時のスタック（[サンプル](evidence-2026-11-26/macbook-rb256-hang-mainthread-sample.txt)）: メインスレッドは `GfxDeviceClient::SetShadersMainThread` → `ThreadedStreamBuffer::HandleOutOfBufferToWriteTo` で書き込み先の空きを待ち、`UnityGfxDeviceWorker` は `ThreadedStreamBuffer::HandleOutOfBufferToReadFrom` で読み出し待ち。メインスレッドから描画スレッドへ命令を渡すリングバッファで、両側が待ち合ったまま進まない。
- `-gfx-ring-buffer-size` の単位と既定値は公開文書に見当たらない（未確認）。
- 見立て（推測）: Unity 6000.6.10f1 の `ThreadedStreamBuffer` が、1 回の書き込みが残り容量を超えたときの処理を誤る。既定サイズでは警告のあと命令列が壊れたまま描画スレッドに渡り、巨大な三角形・Editor の文字欠け・上着の消失になる。256 指定では同じ処理が待ち合いで固まる。各回のログは [macbook-ab-runs-log.txt](evidence-2026-11-26/macbook-ab-runs-log.txt)。
- 確かめ方: 描画スレッドを使わない `-force-gfx-direct` で同じ条件を回し、崩れなければリングバッファが原因。
