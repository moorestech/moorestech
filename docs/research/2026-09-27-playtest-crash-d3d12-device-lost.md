# プレイテストのクラッシュ報告: D3D12 デバイスロスト（2026-09-27）

## 要約

Steam プレイテストの配布版で、テスター1名から「機械を動かしてたりしたらクラッシュした」というクラッシュ報告が届いた。落ちた直接の原因は GPU ドライバ側のデバイスロスト（`d3d12: Device failed error (80004005)`）で、C# の例外ではない。

RTX 2060 の検証機で同じビルド・同じセーブを使って再現を試したが、VRAM と RAM を外部から限界まで圧迫しても同じ落ち方は出なかった。報告者の録画に焼き込まれた計測表示から、**フレームレートに上限がかかっていない（最大 391 FPS）**ことと、**PC 全体の RAM が増え続けていた**ことが分かった。この2点がゲーム側で手を打てる有力候補である。

## 報告の中身

| 項目 | 値 |
|---|---|
| 報告 | `reports/<steamId>/20260926_155211_8c654865`（`moorestech_logs/harness/playtest/` に取り込み済み） |
| 受信 | 2026-09-27 00:52 JST、kind `crash` |
| ビルド | `playtest-staging-20260926-1418`（commit `9cec17ddc`、StandaloneWindows64） |
| 環境 | NVIDIA GeForce RTX 4080 Laptop GPU（VRAM 12GB）、RAM 32GB、D3D12 |
| 同梱物 | `logs/Player-prev.log`、`crashDumps/crash.dmp`（未解析）、録画断片 `seg_000067〜80`、スナップショット16本（最後は tick 43786）、パケットログ |

同じテスターが 13 分前に出したバグ報告（R でリサーチツリーを開いた後 TAB でカーソルが消える）は別件。

## ログから分かる経過（`Player-prev.log`）

1. `[ffmpeg] Error during demuxing: Cannot allocate memory` で録画プロセスが止まる。同時に `[FfmpegProcess] ... ERROR_BROKEN_PIPE`。
2. `D3D12Fence::Wait(...) error ... May cause crash or visual artifacts` が3回出る（GPU の処理が止まりかけている兆候）。
3. `d3d12: Device failed error (80004005)` → `Unrecoverable D3D12 device error!` → `Crash!!!`。
4. スタックトレースは UnityPlayer 内だけ。Unity は Local / Non-Local とも「out of memory ではない」と記録している（Local: Budget 11.77GB、CurrentUsage 3.0GB、CurrentReservation 6.0GB、AvailableForReservation 64MB）。

「メモリ確保の失敗（ffmpeg）→ GPU の停滞 → デバイスロスト」という順番で起きている。起動直後に UnityDebugSheet の `CanvasGroupDrawerBackdrop.SetProgressInternal` で NullReferenceException が1回出ているが、クラッシュとは時間的に離れている。

## 録画の計測表示

録画の右上に外部ツールの計測表示が焼き込まれている。15 断片の先頭と末尾のフレームを切り出して読んだ。

| 項目 | 値 | 読み取れること |
|---|---|---|
| FPS | 88〜391 | 上限がかかっていない |
| GPU 使用率 | 37〜73% | GPU を使い切ってはいない |
| GPU 温度 | 80〜87℃ | 高いが横ばい |
| VRAM | 11.4 / 11.7GB | 全区間で上限に張り付いたまま |
| RAM（PC 全体） | 16.3 → 20.1 → 21.7GB | 単調に増えている |

断片の間に時間の飛びがある可能性があるため、RAM の増加速度は確定値ではない。

## 再現の試み（RTX 2060 検証機）

検証機: RTX 2060（VRAM 6GB）、RAM 32GB、ドライバ 32.0.16.1074。

### 揃えた条件

- 同じビルド（`playtest-staging-20260926-1418`）を Steam 経由で起動した。
- 生成ワールドは seed 196・`placementLedgerDigest` が一致しており、地形はそのまま使えた。
- 報告同梱のスナップショット `tick_43786.json` を `%APPDATA%\.moorestech\Saves\world_1\save.json` として置いた。元のワールドは `world_1.pre-repro` に退避してある（**未復元**）。

### 結果

| 試行 | 条件 | 結果 |
|---|---|---|
| 放置 | 10 分 | 落ちない。VRAM 5.6/6.1GB で頭打ち、プロセスの確保メモリ（private）約 12.9GB で横ばい |
| 自動操作 | 移動・インベントリ・ビルドメニュー・リサーチツリー・視点操作を 30 分繰り返す | 落ちない |
| VRAM 圧迫 | CUDA で最大 4GB を確保して放置 | 落ちない（WDDM がメインメモリへ退避するだけ） |
| VRAM 圧迫（強） | 1GB ずつ 10GB まで確保し、常に書き込み続ける（GPU 使用率 100%） | 落ちない |
| RAM 圧迫 | コミット可能な残りを 0.4〜1GB まで減らす | 検証機の画面が白くなり、Ctrl+Alt+Del も効かなくなった |

最後の試行では、OS ごと固まりかけた（SSH は通っていた）。負荷を止めると画面は戻り、ゲームは生き残っていた。この間、システムイベントログに `nvlddmkm` の Id 153 が1件記録された。一方でゲームのログには D3D12 のエラーも Fence 警告も ffmpeg の確保失敗も出ていない。報告者は「ゲームだけが落ち、OS は生きてクラッシュ報告を送れた」ので、症状が違う。**再現はできていない。**

### フレームレートの実測

PresentMon で測ると、検証機のゲームは `SyncInterval=1`（垂直同期オン）で約 60 FPS だった。起動オプション `-screen-quality Low`（垂直同期オフの品質設定）を付けても変わらなかった。**これまでの検証はすべて 60 FPS で行っており、報告者の「上限なし」という条件を再現できていない。**

## 見立て

VRAM と RAM の逼迫だけでは説明がつかない。有力な候補は次の2つ。

1. **フレームレートの上限がない**
   - 製品コードで `Application.targetFrameRate` を設定しているのは、デバッグシート（`Client.DebugSystem/DebugSheet/DebugSheetController.cs`）の FPS 上限選択だけ。
   - 標準の品質設定 High は `vSyncCount: 1` のため、`targetFrameRate` は Unity の仕様で無視される。報告者の環境では何らかの理由（ドライバの設定、ノートPCの画面構成など）で垂直同期も効いておらず、結果として上限なしで描画していた。
   - 発熱したノートPCの GPU に上限なしで描かせ続けると、GPU のタイムアウトからドライバの強制リセットに至りやすい。他の重いゲームより総合負荷が低くても落ちる理由として筋が通る。
2. **メモリの増加**
   - PC 全体の RAM が増え続け、ffmpeg の確保失敗を経てデバイスロストに至っている。
   - 検証機ではゲーム単体の確保メモリが約 12.9GB あり、放置中は横ばいだった。操作の種類（機械の稼働・UI の開閉など）によって増えるかは未確認。

VRAM が上限に張り付くのは検証機でも同じ（5.6/6.1GB）で、ゲームが予算いっぱいまで確保する性質とみられる。単独では原因と言えない。

温度と電力の要因は完全には否定できないが、他の重いゲームでは落ちていない前提なら優先度は低い。

## 次の手

1. **上限なし FPS での耐久テスト**: 検証機のゲームを垂直同期オフ・上限なしにして、報告者の条件で 40 分回す。ゲームのコードを変えずに行うには、Mod の DLL として読み込ませる方法がある（`Mod.Loader/ModsResource.cs` が `Assembly.LoadFrom` で任意の DLL を読み込む）。同時に FPS・メモリ・VRAM を 10 秒ごとに記録する。
2. **メモリ増加の計測**: 機械を動かす・UI を開閉するなど報告者に近い操作で、プロセスの確保メモリを時系列で測る。
3. **フレームレート上限の導入**: 1 の結果次第で、製品版に上限（または設定項目）を入れる。挙動の変更なので、実装前に moores-grill-with-docs を通す。
4. **crash.dmp の解析**: Windows 機の WinDbg でドライバ側のスタックを確認する。
5. **配布ビルドへの遠隔操作の口**: 開発版ビルドに、外からコマンドや任意の C# を送って実行できる仕組みを作る案がある（Mono なので実行中の DLL 読み込みが可能）。再現検証の手間を大きく減らせる。別途設計する。

## 再現手順のメモ

- 検証機は `scripts/playtest/verify-on-windows.sh` と同じ `MOORESTECH_VERIFY_*` の環境変数で SSH できる。
- 画面操作が要る処理（ゲーム起動・クリック・スクリーンショット）は、SSH のセッションからは見えない。対話セッションで動くスケジュールタスク経由で実行する（`scripts/playtest/windows/run-smoke.ps1` の `Start-SteamInInteractiveSession` と同じ方式）。
- 前回のゲームが異常終了していると、タイトル画面で「前回ゲームが正常に終了しませんでした」ダイアログが出るので、「送らない」を押してからロードする。
- 生成ワールドの報告には `world.json` しか入っていない（ADR 0064）。地形は同じビルドの `runs/<label>/build/game/worldSnapshots/` から引き当てられる。
