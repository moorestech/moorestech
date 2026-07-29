# cef-unity Web UI 更新頻度低下 調査報告（続報）

- 調査日: 2026-07-29（初回報告と同日、続き）
- 前提: 同ディレクトリ `cef-unity-frame-rate-investigation-2026-07-29.md` の追補
- 手法: 静的解析3系統（差分・リソース寿命・計測手段）の並列調査＋tree3実機での最小CEFシーン実測
- moorestech 固定リビジョン: `64f9a5f`（`packages-lock.json` で確認、パッケージ実体 `Library/PackageCache/jp.juha.cefunity@73e14c137c71`）
- cef-unity main: `8fc504e`

## 1. 結論アップデート

初回報告の主結論（cef-unity wrapper の待機・キュー・pump・転送設計が主因）は維持。今回の続報で次が新たに**確定**した。

0. **最重要**: 計装版serverの実測で、**単発の全画面同期Metalコピー（`waitUntilCompleted`）が121〜157msに達してserverメインスレッドを凍結し、「タイマー崩壊→BeginFrame滞留→burst drain→paint低下→rAF 11回/秒」の連鎖が起きる**ことを最小構成ですら定常的に観測した（§2.8）。dirty rectは転送面積の2〜4%しかなく、コピーの96〜98%が無駄である

1. **busy-wait は健常時でも毎フレーム平均2〜3ms・最大7.7msをUnityメインスレッドspinに消費している**（実測、`_enableLog=1` の内蔵カウンタで取得）
2. **BeginFrame の過剰供給は実在する**。CEF内 rAF が24%の秒で62回/秒を超え、最大80回/秒（vsyncロックの通常ブラウザではあり得ない値。+3/+6ms flush が余剰フレームを実駆動している実証）
3. **`64f9a5f`→main の101コミットで、7問題機構のうち修正されたのは damage-streak 発振（`a08f585`）の1件のみ**。他6機構（busy-wait・IPC burst・+3/+6ms flush・1000Hz pump・全画面Metalコピー・リソース寿命）はロジック完全無変更。ピン更新だけでは直らない
4. **Play停止のたびに cef-unity-server がゾンビ化する**（実測+1/サイクル、ゾンビPIDと直前のserver PIDが完全一致）。Rust client が `spawn()` 直後に Child ハンドルを破棄し、PID追跡・waitpid・生死確認が一切ない（`client/src/lib.rs:286-288`）
5. **Mach receive port が Play/Stop 1サイクルにつき+1リークする**（コード確定・解放経路が存在しない、`metal_texture.m:44,59`）。cef-unity 自身が `REFACTORING_REPORT.md` の CLI-10 として自己診断済みだが、main HEAD でも未修正
6. **cef-unity-server は Unity のファイルディスクリプタを継承する**（実測）。tree2 の観測で、server プロセスが Unity のゲームサーバーTCPソケット（ポート11564のLISTEN含む）を保持していた。server存命中に Unity が Play を停止してもポートが解放されず、「Address already in use」型の不具合につながり得る

## 2. 実測結果（今回新規）

### 2.1 実験環境

tree3 の Unity Editor で、ゲーム本体を起動しない最小CEFシーン（空シーン＋Canvas＋RawImage＋`CefUnityBrowserSample`、`_enableLog=1`、`_zeroFrameWaitMs=10`、`_resolutionScale=1`、音声無効）を動的構築。ページは rAF 毎秒回数・最大コールバック間隔を毎秒 `fetch` でホストへ送信する自作プローブ（進捗バー模擬の連続アニメーション、127.0.0.1:25199）。

この構成はゲームサーバー（ポート11564）に依存しないため、他worktreeのPlayModeと並行実験できる。

### 2.2 busy-wait の定量（§8計測項目1・2に対応）

`_enableLog=1` にすると `CefUnityBrowserSample` 内蔵の計測が2秒ごとに出力される（再ビルド不要）。健常状態・連続アニメーション中の実測:

```text
[CefUnity] 0F-wait: fresh=81 fallback(1F)=35 idle=0 block_avg=3.07ms block_max=7.65ms
[CefUnity] 0F-wait: fresh=87 fallback(1F)=33 idle=0 block_avg=1.97ms block_max=7.74ms
```

- fresh+fallback ≈ 116〜119回/2秒 ≈ 毎フレーム busy-wait に突入（idle=0。rAFアニメーションはスクロールと違い待機抑止経路に入らない、初回報告§5.1の裏取り）
- 健常時ですらフレーム予算16.7msの12〜18%をspinに消費。paintが疎になるほどspin時間は上限7ms超へ張り付く（正帰還の入口）

### 2.3 BeginFrame 過剰供給の実証（§8計測項目5に対応）

プローブページのrAF実測（152秒分、起動直後除外）:

| 指標 | 値 |
|---|---:|
| rAF平均 | 56.8回/秒 |
| rAF>62 の秒 | 24% |
| rAF>70 の秒 | 5%（最大80回/秒） |
| maxGap中央値 | 40ms |
| maxGap最大 | 1136ms |

vsyncロックされたブラウザのrAFは60回/秒を超えない。62超が定常的に出る＝Unityの60Hz BeginFrameに加え+3/+6ms flushが実際に余剰フレームを駆動している。また健常時でもmaxGap中央値40ms＝毎秒2〜3フレーム落ちの微小スパイクが常在する。

### 2.4 健常時のserverメインスレッドプロファイル（§8計測項目8・10に対応）

`sample`（5秒、3839サンプル）による cef-unity-server 健常時の分布:

| 区分 | 割合 |
|---|---:|
| mach_msg 待機（アイドル） | 68% |
| `timer_callback`→CEF pump | 22%（うちタイマー機構オーバーヘッド含むDoTimers系30%） |

初回報告の病的状態では765サンプル中755（約99%）が timer_callback 内だった。「健常=68%アイドル ↔ 病的=99% pump張り付き」が定量的な状態判別軸になる。

### 2.5 serverログの周期集計（§8計測項目6に対応・再ビルド不要）

`_enableLog=1` で `$TMPDIR/cef_unity_server.log` に60paintごとのlatency集計が常時出る:

```text
BeginFrame→paint latency (n=60): avg=6.141ms median=3.169ms min=0.097ms max=43.763ms
```

健常時 median≈3ms、max 40〜50ms スパイクが定常的に混入。CEF自体のVERBOSEログも `$TMPDIR/cef_debug.log` に常時出力されている（`--logging` と無関係、`server.rs:940-941`）。

### 2.6 Play/Stop 反復のリソース残留（§8計測項目9に対応）

最小CEFシーンで Play/Stop 5サイクルを自動実行し、各時点の Unity(PID 43268) の Machポート数・ゾンビ数を記録:

| サイクル | 停止時ポート | ゾンビ数 |
|---|---:|---:|
| 1 | 1416 | 5 |
| 2 | 1429 | 6 |
| 3 | 1442 | 7 |
| 4 | 1455 | 8 |
| 5 | 1466 | 9 |

- **ゾンビ: 正確に+1/サイクル**。ゾンビのPID群はサイクル中に記録した cef-unity-server のPID群（47231, 52765, 58002, 60064, 61740）と完全一致
- **停止時ポート残留: +11〜13/サイクルで単調増加**（平均+12.5）。うち+1はコード確定の `g_receive_port` リーク。残りの帰属を切り分けるため、CEF無し空シーンでの対照実験を実施（§2.7）

### 2.7 対照実験（CEF無し空シーン）

同じEditorでCEFコンポーネント無しの空シーンを Play/Stop 3サイクル:

| サイクル | 停止時ポート | ゾンビ数 |
|---|---:|---:|
| 開始時 | 1489 | 10 |
| 1 | 1494 | 10 |
| 2 | 1507 | 10 |
| 3 | 1516 | 10 |

- **ゾンビ: +0/サイクル**。ゾンビ蓄積は100% cef-unity起因と確定
- **ポート: CEF無しでも平均+9/サイクル増加**（Unity Editor自体のPlay/Stopに伴う挙動）。CEFあり時の+12.5との差分≒+3.5/サイクルがcef帰属候補で、うち+1はコード確定の `g_receive_port`。ポート数の外部観測はノイズが大きく、+1リークの直接可視化には root権限の `lsmp` 等による port種別の内訳が必要

### 2.8 計装版serverによるSTATS計測（§8計測項目3〜7に対応）

`64f9a5f`ベースに毎秒集計ログ（STATS行）だけを追加した計装版serverをビルドし（挙動不変を差分レビューで確認）、PackageCacheの`cef-unity-server.app`のみ差し替えて計測した（dylibは変更不要のためEditor再起動も不要。ビルド用worktreeはscratchpadに作成し、cef-unity本体リポジトリは無変更）。

定常状態（1280x720、連続アニメーション）は秒単位で2レジームが交互に現れる:

| 指標 | 良い秒 | 悪い秒 |
|---|---|---|
| ticks（1msタイマー実発火数/秒） | 970〜1060 | **214〜705（タイマー崩壊）** |
| pump合計/単発max | 70〜100ms / 3〜7ms | 260〜390ms / **163〜214ms** |
| maxdrain（1 tickのdrain数） | 1〜2 | **10〜15（burst実証）** |
| bf_f3+bf_f6（追加flush発行/秒） | 0 | 5〜29 |
| flush_ovw（pending flush上書き/秒） | 0 | 14〜34 |
| paint/秒 | 60 | 36〜51 |
| copy_wait合計/単発max | 28〜35ms / 2.6〜6ms | 170〜269ms / **121〜157ms** |
| dirty_px / full_px | 2〜4% | 2〜4% |

- **単発の`waitUntilCompleted`が121〜157msに達する**ことがあり、これがserverメインスレッドを凍結→1msタイマーが回れずticks崩壊→BeginFrameがIPCに滞留→burst drain→paint低下、という連鎖が実測で完結した。全画面同期Metalコピー（§初回報告5.4）は理論上の懸念ではなく、実測された最大のストール源である
- ストール中の同秒にプローブページのrAFは11〜47回/秒まで低下（正常秒は60）。病的1fps状態は、この間欠的ストールがGPU/CPU競合の増大で慢性化したものという説明と整合する
- 121〜157msという異常なGPU同期時間の原因は、同一GPUを共有するUnity Editor本体・CEF Rendererとのキュー競合と推定される（プロセス間GPUスケジューリングの直接計測は未実施）
- **dirty rect面積は転送面積の2〜4%**。進捗バーの数ピクセル変化のために毎paint全画面をコピーしており、コピー帯域の96〜98%が無駄（初回報告§5.4の定量化）
- flush上書き（`flush_ovw`）は悪い秒に毎秒14〜34回発生。+3/+6ms flushの機構が遅延時にほぼ空振りしていることの実証

### 2.9 FD継承の実証

tree2 の観測（lsof）で、cef-unity-server プロセスが親Unityの TCPソケット（moorestechゲームサーバーのポート11564 LISTEN・確立済み接続）を同一デバイスIDで保持していた。Rust `Command::spawn` はRust自身が開いたFDにはCLOEXECを付けるが、Unity(mono)側が開いたソケットには付いておらず、そのまま子へ継承される。

含意: Play停止後もserverが存命なら11564が解放されず、次のPlayでbindに失敗し得る。moorestechのプレイテストで断続的に起きる接続系の不安定さの一因である可能性がある。

## 3. `64f9a5f`→main 差分の確定（101コミット）

7問題機構への影響（diff-analyst調査、全て両リビジョンのコード実読で確定）:

| 機構 | mainでの状態 | 判定 |
|---|---|---|
| ① C# busy-wait spin | `SpinWait(64)`ループ・定数(4.5/7/7.5ms・probe window 60)完全同一。`CefZeroFramePacer.cs`へ構造分離のみ | 無変更 |
| ② damage-streak発振 | `suppression_cooldown`ヒステリシス追加で自己修復 | **修正済み（a08f585）** |
| ③ BeginFrame IPC 全drain→pump1回 | 構造完全同一（再入ガード追加のみ） | 無変更 |
| ④ +3/+6ms flush | 閾値配列 `[3.0, 6.0]` ロジック完全同一（定数名リネームのみ） | 無変更 |
| ⑤ 1000Hz pump | `CFRunLoopTimerCreate(..., 0.001, ...)` コメントまで同一 | 無変更 |
| ⑥ 全画面同期Metalコピー | `iosurface_pool.m` は101コミットを通じ**byte単位で完全同一**（`git diff --quiet`） | 無変更 |
| ⑦ Machポート/キャッシュ寿命 | disconnect関数は依然存在せず。CLI-10は提案止まり | 無変更 |

a08f585 の内容: 低速帯域でdamage-streak抑止が定着せず paint が `1,1,1,0` の4フレーム周期で欠落する発振を、`suppression_cooldown`（60フレーム≒1秒のクールダウン）で自己修復。+39/-1行の小修正。実測で低速ドラッグの規則的穴が消滅。

その他のmacOS関連: 06d8731 の viewport clamp が GPU経路にリグレッションを起こし同日 d46a5db で修正（Retina縦長Game viewでテクスチャ縦伸び+座標ズレ）。内部の第2回監査文書（2026-07-23、docs/REFACTORING_REPORT.md）は旧40指摘のうち「修正済み0件・6件は悪化」と自己申告している。

## 4. リソース寿命の確定（lifecycle-auditor調査）

### Play再起動でリセットされるもの（正常終了時・確定）

serverプロセス全体とCEF子プロセス／IPC接続（`CONNECTION`）／server側 browsers・pending_flush・IOSurfaceプール／`INITIALIZED` 等のフラグ類／NSEventスクロールモニタ（これは `removeMonitor:` で正しく後始末されている）。

### Unity Editorプロセスに蓄積するもの

| 項目 | 性質 | 根拠 |
|---|---|---|
| Mach receive port `g_receive_port` | **+1/サイクル、恒久リーク**（解放コード不存在） | `metal_texture.m:44,59`、CLI-10 |
| ゾンビプロセス | **+1/サイクル**（Childハンドル即破棄・waitなし） | `lib.rs:286-288`、実測一致 |
| `_surfaceCache[4]` の旧IOSurfaceRef | 一時的（最大4枚≒33MB、新セッション数フレームで自然解消） | `metal_texture.m:210-213` |
| `PAINT_COUNT`/`PUMP_COUNT` | 単調増加（診断値のみ・実害なし） | `lib.rs:177-178` |

### shutdown経路の設計欠陥（確定）

`cef_unity_shutdown()` は Shutdownコマンドを fire-and-forget 送信 → 500ms固定sleep → 終了確認なしで抜ける（`lib.rs:358-366`）。クライアントはserverのPIDを保持していないため、shutdownハング（REFACTORING_REPORT.md SRV-9: Mutex poison時に `cef::shutdown()` 未到達）が起きるとserver+CEFヘルパー一式が孤児化し、検出手段がない。初回報告の観測（高CPU serverは対象Editorの子だった）から、今回の1fps症状の主因ではないが、再発時の切り分けとして `pgrep -f cef-unity-server` でプロセス数を確認する価値がある。

**shutdownクラッシュの実測**: 今回のPlay/Stopサイクル実験のcycle1停止時（18:38:31）、純正serverがCEF内部（CrBrowserMainスレッド）で SIGSEGV（`KERN_INVALID_ADDRESS at 0x40`）でクラッシュした（`~/Library/Logs/DiagnosticReports/cef-unity-server-2026-07-29-183831.ips`、byPid=47231はcycle0のserver PIDと一致）。shutdown経路がクリーン終了しないケースは理論上の懸念ではなく実際に起きる。この時はプロセス自体は死んだためリソースはOSが回収したが、クラッシュ位置次第では孤児化しうる。

## 5. 計測手段の確定（instrumentation-scout調査）

| 手段 | 取れるもの | コスト |
|---|---|---|
| `_enableLog=1`（prefab/インスタンスの1フラグ） | busy-wait突入/fallback/block時間、frame/content jitter、paint/pump/afiカウンタ、server側latency集計、Rustログ転送 | ゼロ（再ビルド不要） |
| `$TMPDIR/cef_debug.log` | CEF内部VERBOSEログ（常時出力中） | ゼロ |
| `CEF_UNITY_DEV_TOOLS` シンボル追加 | `cef_perf_probe`（毎フレームCSV）、`cef_novsync`・`cef_no_zero_wait` 等のA/Bトグル | PlayerSettings変更のみ |
| Rust再計装 | IPC drain件数/tick、flush発行数内訳、`waitUntilCompleted`・Mach send所要時間、dirty rect面積 | `git worktree add`で64f9a5fを展開→計測追加→`cargo build --release`→`deploy.sh`→PackageCacheのバイナリ2点を差し替え |

注意: ローカルcef-unity repoのHEADはディレクトリ構造が変わっており（`Interop/Plugins`→`Plugins`）、main直ビルドの成果物差し替えは不可。必ず `64f9a5f` のworktreeでビルドすること。

## 6. 修正方針の更新

初回報告§9の方針は全て有効。今回の確定事実により優先順位と根拠を更新:

1. **全画面同期Metalコピーの廃止（最優先へ昇格・実測裏付け済み）**: 単発121〜157msのストールがpaint低下連鎖の直接の起点であることをSTATSで実証（§2.8）。dirty rectベースの部分コピー化、`waitUntilCompleted`の非同期化（completion handler / 次paint時にフェンス確認）、またはコピー自体の排除（IOSurfaceの直接共有）を検討
2. **busy-wait廃止（実測裏付け済み）**: 健常時でも毎フレーム2〜3ms消費、病的状態で正帰還。Viewer(`CefFrameSource.cs`)のノンブロッキング受信という成功前例がリポジトリ内にある。serverストール時にUnity側も道連れでspinする複合を断ち切る
3. **+3/+6ms flush と1000Hz pumpの廃止（実測裏付け済み）**: rAF>62の実証と、遅延時にflush上書きが毎秒14〜34回空振りしている実測（`flush_ovw`）。CEF要求駆動（`OnScheduleMessagePumpWork`）へ
4. **プロセスライフサイクルの根本修正（新規・確定バグ3件）**: (a) Child保持+shutdown時のwait/kill（shutdown SIGSEGVも実測済み）、(b) `g_receive_port` の解放（CLI-10の実装）、(c) spawn時のFD継承遮断
5. **ピン更新（a08f585取り込み）**: damage-streak発振の解消のみ。単独では不十分だが、周期的な25%欠落の増幅要因を消せる
6. Webフロント側の rAF 非依存化（`holdCraftLogic.ts` の0.25s cap問題）は初回報告どおり

## 7. 未解決・進行中

- **病的状態（1fps・CPU100%超）の再現**: 計装版serverで長時間ソーク実施中（STATS毎秒・rAF時系列・プロセスCPUを自動記録、劣化検知モニタ設置済み）。初回観測では発生まで1時間前後を要した。再現すればSTATSの時系列で発症の因果順序（copy_wait悪化が先か、drain滞留が先か等）を直接確定できる
- 病的状態が最小シーン（実webui無し・ゲーム無し）で再現するかどうか自体が切り分け情報になる: 再現すればcef-unity単独の問題、再現しなければ実ページ/ゲーム負荷との複合が必要条件
- 121〜157msのGPU同期ストールの帰責（Unity Editor・CEF Renderer・OSのGPUスケジューリングのどれとの競合か）は未確定。Metal System Trace等による直接計測が次の一手
- 純正serverでのソーク中（stock phase）にtree3のEditorが外部要因（全Unity一斉終了イベント）で終了したため、純正版の長時間データは不完全。計装版ソークで代替する
- 計装版バイナリはPackageCache内のみの差し替えであり、パッケージ再解決で純正に戻る。純正バックアップはscratchpadに保存済み。調査終了時に復元すること
