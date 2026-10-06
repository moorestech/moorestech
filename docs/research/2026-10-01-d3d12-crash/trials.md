# 試験・観測の一覧

対象は Unity 6000.3.8f1 / D3D12 の通常描画 command list の Close 相当が E_FAIL となったクラッシュ。**元クラッシュは未再現、根本原因は未確定。** 別の故障を再現した試験、条件未達の試行、CPU 試験、静的監査を区別する。

この文書は保存記録の集約であり、新しい機器確認ではない。最後の機器実測は **2026-09-30 13:31:45.748 UTC**。以下の時刻は UTC。資料番号は末尾索引に対応する。

## 状態の読み方

- **実施**: 記載した操作・測定が行われた。元クラッシュの再現や仮説全体の陰性を意味しない。
- **条件未達**: 起動・部分操作があっても、目的の介入・記録・前提が成立しなかった。陰性回数へ算入しない。
- **準備のみ／静的監査**: 設計・ビルド・保存資料解析。ゲームを新規実行した試験ではない。
- **拒否**: 操作が実行されなかった。入力所有権ガード、OS 実行制限、安全審査の拒否は別の理由として記録する。

## A以前: 先行公開文書に記録された試験

以下は9/27・9/29の先行公開文書（READMEの「先行公開文書との関係」参照）から引き継ぐ**当時の実施報告**。本PRでは当該試行の元ログを再解析していない。後続A〜Zの計測と同じ精度・取得範囲で検証済みと扱わない。

| 当時の記録 | 報告された結果 | 限界と現在の扱い |
|---|---|---|
| 9/27: 放置10分・自動操作30分 | 元クラッシュなし | 当時の同buildとの記述を、後続9/29版との全配布一致へ拡張しない |
| 9/27: VRAM/RAMへの強い圧迫 | 元fatalなし。RAM試行では画面応答が失われ、負荷停止後に復帰 | OS側の別症状。現在の通常負荷という制約内で再実行する案ではない |
| 9/29: 36地点移動、録画子終了、表示切替20回、Pause15回 | 元fatalなし。Pauseでメモリ増加の記録 | 診断由来の増加や子世代、元の失敗状態との一致は未同定 |
| 9/29: ゲームのJob上限とRT確保 | Fence警告後、eviction dump／Mono OOMで終了 | 元Close E_FAILと異なる終了。後続の拒否済み経路を再試行する根拠にしない |
| 9/29: 全体commit枯渇2回 | 前報は2/2でENOMEM→BrokenPipe→録画停止を記録。1回はDWM/CEF helper等のOOMとgame停止、もう1回はgame生存 | 元fatal未再現。writer throw site・pool/queueは未測定。1回目はRT/Pause確保も重ねており単一要因試験ではない |

したがって「検証機でBrokenPipeは一度も発生していない」とは言わない。後続の保存18ログやG/H/MでBrokenPipeがないことと、前報の2件を区別する。固定した証拠索引に当該 `player-trial1-hang.log`／`player-trial2.log` 名のコピーは確認できず、この記載は前報・HANDOFFの実施報告に基づく。別名保存やダンプの存在は、同じログ全文の照合の代用にしない。

## A〜I: メモリ条件・録画停止・連続通常操作

| 名称 | 状態 | 条件と観測 | 言えないこと |
|---|---|---|---|
| A | 条件未達・無効 | 限定コミット確保がロード中と重複。下限1536MiBを下回る空き commit 約1500MiB を検出して停止・解放。監視の重複終了要求も修正した。[1] | 準備済みゲームでの陰性ではない。設定下限を瞬間的にも常に維持したとは言えない。 |
| A2 | 実施 | ロード後、限定確保中に ESC。録画世代切替・ゲーム継続を確認。空き commit 最低1904MiB、最大62°C/65.29W。対象 fatal・ENOMEM なし。[2] | 完全枯渇や、元の録画故障後状態は作っていない。 |
| B | 条件未達 | 限定確保を解放したが、次の呼出しに残した ESC が未実行。[2] | 解放後 ESC の陰性ではない。 |
| B2 | 実施 | 解放確認から約62.42秒後に ESC。ゲーム生存。最低空き commit 2002MiB、最大61°C/45.94W。[2] | 完全枯渇後の試験ではなく、先行 ENOMEM もない。 |
| C | 実施 | 対応 debug layer 有効。限定確保中に最小化・復元・ESC。対象署名なし。最低空き commit 1767MiB、最大57°C/47.27W。[2] | 元と同じ GPU 不正状態を経た証拠はない。 |
| D | 独立実施記録未確認 | 本集約の一次入口・結果一覧では独立した完了結果を確認できない。試験回数に算入しない。 | アルファベットの連続性だけから実施を補完しない。 |
| E | 実施 | 通常負荷で粉砕機 F → Tab → ESC。別に1920×1080→1280×720→復元を1往復。対象 UI、CEF texture 更新、debug 出力経路を確認。最大63°C/95.86W。[2] | InfoQueue の filter/mute 未取得、例外設定の一部適用未確認。エラー不在を原因除外にしない。 |
| F | 実施・誘発条件未達 | 当該録画子だけの private commit 上限を現在値＋8MiBに設定。90秒内に異常なし、制限解除を確認。[2] | 録画停止後 ESC を検証した試験ではない。 |
| G | 実施・予定遅延は未達 | 録画子だけの上限を現在値＋256KiBに設定し約0.106秒で rawvideo ENOMEM・子終了。制限解除後、約98分後に粉砕機→ESC、Unavailable/video 欠損、ゲーム継続を確認。[2] | 予定の1〜3分後条件ではない。監視に中断があり、約100分連続監視ではない。BrokenPipe や pool 枯渇を同時再現していない。 |
| H | 条件未達 | ENOMEM は発生したが controller の終了時刻取得付近で失敗。60秒待ち・ESC 前に中止。上限解除・通常終了を確認。[2] | 録画故障後 ESC の陰性ではない。 |
| H2 | 拒否・負荷操作未起動 | ゲームと監視は準備したが、安全審査で負荷 controller は実行されず、終了・復元。[2] | 準備プロセスの存在を負荷試験実施と扱わない。以降その経路は再試行していない。 |
| I / I2 / I3 | 条件未達 | I は telemetry 引数不足、I2 はロード前 UI 操作、I3 は照準切替未成立で中止。各回終了・復元。[2] | 30分の通常観測を達成した回数に含めない。 |
| I4 | 実施 | 同一ゲームで30分6.673秒、粉砕機→ESC→復帰7回。memory353/GPU3044標本、必須 snapshot16件、欠損なし。対象 fatal・ENOMEM なし。[3] | ゲームは約212.36MiB増加。診断 assembly の増加を含み、漏れの原因は同定できない。録画子は世代交代するため、同一 ffmpeg の30分陰性ではない。 |

初期の約32分 telemetry は複数再起動と限定確保を跨ぐ。I4 の同一ゲーム30分と混同しない。初期 telemetry と安全監視には空きメモリの取得 API 差もあり、無条件に単一系列へ結合しない。[1][2]

## J〜R: 通常 UI・撮影・CEF の観測

| 名称 | 状態 | 条件と観測 | 言えないこと |
|---|---|---|---|
| J | 実施 | 小さい画面サイズ変更の CEF 反映直後 ESC を5回。元 fatal なし、最大55°C/48.69W。[4] | CEF 双方向同期や handle 寿命の一般的安全性を証明しない。 |
| K | 実施・別症状を観測 | 完了待ち3回＋短間隔3回の ESC。撮影 pending2回、同 workspace の画像保存失敗2件。D3D12 異常なし、録画正常。最大63°C/96.74W。[5] | 画像保存失敗を元 Close E_FAIL と同じ故障にしない。元の録画故障後状態ではない。 |
| L | 条件未達 | 対照 resize の反映待ちが5秒 timeout。途中イベント欠落。[6] | 介入条件の陰性ではなく、timeout の原因も未確定。 |
| L2 | 実施 | 対照・介入を各1回完走。撮影 pending 中に resize、CEF 反映の約82.19ms後に画像完成。実画像1920×1080を検証、元 fatal なし。[6] | pending はファイル待ちであり、撮影 GPU 命令と resize の重複を証明しない。 |
| M | 実施 | 同一性を確認した録画子を1回終了。退出確認後約0.218秒で ESC、約0.5157秒で撮影完成。全19標本で pool2/queue0、readback 停止、RT 不変。[7] | 子退出は再現したが、元の BrokenPipe/ENOMEM/pool 枯渇は再現していない。 |
| N | 未実施・候補の優先度を下げた | 撮影中 foreground 切替案。元操作の証拠も、focus から資源再作成へ至る具体経路も得られず実施しなかった。[8] | 正常だった試験として数えない。 |
| O | 実施・診断欠損 | 通常 ESC/resize の対照・介入が約8.72秒で完走。観測5世代の Reset/Close 成功。しかし8MiB上限到達、drop1、終了記録不足。[9] | 完全な診断陰性ではない。初回設営失敗による別起動も診断試験に数えない。 |
| P | 条件未達 | INFO 本文の保存を抑えた版。対照で Screen は縮小したが CEF texture は旧寸法のまま、5秒期限。介入未到達。2052イベント、終了記録不足。[9] | resize 停止を元 fatal と同一視しない。観測内エラーなしでも完全陰性ではない。 |
| Q 準備 | 条件未達 | 当初は別用途の GPU 負荷が高く、world/DLL変更前に停止した。[10] | この段階の復元・再現成否を主張しない。 |
| Q 実行 | 実施・介入条件未達 | 後に起動。対照 resize が停止し介入に未到達。一方 native6725件・drop0・明示終了を確認。中央約4秒は producer pump 継続、paint0、publication/adoption 停止。[11] | callback 不在と早期 guard 拒否は未分離。managed/native 時計の原点差があり絶対時刻結合不可。元 fatal への因果なし。 |
| R | 実施 | 新規起動後、対照より先に撮影 pending→resize を1回。約7.68秒で完走、native6381件・drop0、元 fatal なし。[12] | 対照がないため paired 判定器は未完了。Qとの差は順序・履歴・時機も含み、pending が停止を防いだと因果推論できない。 |

## S〜Z: 保存資料監査から最終測定まで

| 名称 | 状態 | 内容・観測 | 限界 |
|---|---|---|---|
| S | 静的監査 | O/P/Q/R の同一 allocator/list の submit→次 Reset10組を再集計。Q/Rの受信 tuple2962件を照合。[13] | 新しい実機試験ではない。CPU 時間差だけで GPU 完了・安全性は判定できない。 |
| T | 静的監査 | 元の browser 構成、ESC、画面・format の変更経路と保存項目を確認。[14] | 元 ESC が新 CEF resource を開いた証拠なし。「ESC 自体が allocator 再利用を誘発」の候補を格下げした。 |
| U | 静的監査 | 録画停止と RT 寿命、公式修正候補、次の観測の判別力を再評価。[15] | 新しい寿命破壊経路・因果を識別する通常試験は得られず、実機を動かしていない。 |
| V | 設計・CPU fixture | 正式 rendering callback・fence identity の取得案と保守的 classifier を検証。[16] | GPU 完了値の実測なし、native collector の統合も未実施。 |
| W | 実装準備・CPU fixture/compile | retained COM、世代付き registry、callback core を実装。CPU9 fixture と Windows 向け型・ABI検査を通過。[17] | この段階ではゲームへ組込み・DLL実行なし。COM/Unity/driver 実行検証ではない。 |
| X 準備 | 準備・一部拒否 | 観測DLL統合・ビルド・CPU試験。別の配布 compiler 確認 helper は OS 実行制限で実行前に拒否。[18] | helper の Windows compile 成功は未確認。安全審査拒否と OS 制限を混同せず、別経路で再試行していない。 |
| X 起動 | 条件未達 | タイトル操作が、ゲーム開始後にしか作られない endpoint を誤って前提にした。world に入らず介入・native trace なし。最大65°C/95.95W。[19] | 前提誤りは確定したが、空 console だけから実際に停止した箇所は確定できない。一般的なゲーム起動障害ではない。 |
| Y | 条件未達・入力ガード拒否 | タイトル表示と所有 window の前面確認は成功。約21秒後の別 click worker が直前の foreground 判定で拒否し、入力しなかった。[20] | world/撮影/fence 介入に未到達。拒否時の実 foreground は未記録で、他アプリへ原因を帰属できない。安全審査拒否ではない。 |
| Z | 実施 | 同じ worker 内の前面確認付き通常操作で対照・介入・復元を完走。native6761件・drop0。下記参照。[21] | 元録画故障・対象 fatal は再現していない。 |

## Z の成立条件と残る空白

2026-09-30 13:28〜13:31、正式 rendering callback で frame fence を取得後、1920→1280→1920 の対照、通常 Pause、撮影 pending 中の1280変更、画像完成、1920/GameScreen復元を実施した。操作窓は7.8978011秒。[21]

- native6761件は accepted=written、drop0、footer missing=false。これは明示的な観測終了までの配送完全性で、後続 Quit を含まない。
- CEF 受信・既存 Execute・managed 操作と、正式 rendering callback が異なる OS thread だったことを同一試行で測定した。報告者の実行 thread や全合法描画 context の確定ではない。
- 介入 Reset のイベント6671を直前 Execute6647と同一 allocator 世代/device/fence へ結合。返却 marker34103に対し読取り34108。他2組も marker 以上だった。
- 既存 Execute の caller 契約と marker 意味の問題が残るため、この大小関係は**個別 list の GPU 完了・allocator 再利用安全性の保証ではない**。取得前の Reset2件は fence 未測定。
- 4つの resize ごとに receive→同 resource の submit→return を独立集計。publication 番号一致だけでは texture 同定していない。
- 記録範囲の API 失敗と InfoQueue Error/Corruption は0、Warning2033件を保持。元 fatal/Fence不足/rawvideo ENOMEM/BrokenPipe はなし。別の終了時例外に同じ80004005があっても同 fatal と数えない。
- 録画は使用可能で世代0→2。元の書込み失敗・録画不能を再現していない。画像 pending は GPU overlap の直接証拠ではない。
- 汎用 analyzer は引数不足、または対照操作名の不一致により complete=false のまま保存した。記録を書き換えず、成立した個別条件を独立解析と再計算で示した。

## 負荷・終了・復元の到達点

初期 A〜C の限定確保と F〜H の録画子制限は過去に行った条件の記録であり、再実行手順ではない。H2 以降、その拒否済み負荷経路を再試行していない。後半は通常操作・短い診断窓・所有プロセス限定の監視を使った。[2][21]

後半の運用停止条件は78°C/125W、物理空き2GiB、commit空き1.5GiB、独立ゲーム期限10分。起動余裕は55°C/25W未満、物理16GiB/commit18GiB以上などを用いた。数値は今回の運用条件であり、機器の安全定格でも安全保証でもない。[18][20][21]

Z は242監視標本、最大64°C/95.55W、最低空き commit10286MiB・物理12101MiB。最後の13:31:45.748 UTC確認では、元DLLとworld27ファイルのhash/個数復元、一時SDK layer除去、自動pagefile、試験game/task/process不在、録画・監視終了を記録した。他用途の既存プロセス識別も維持した。この結果を現在時刻の再確認として記載しない。[21][22]

初期段階はworld23ファイル、後半は直前の利用状態に合わせ27ファイルを保存・復元した。古いバックアップで後半の状態を上書きしない。診断ツールの導入自体と、一時DLL・設定・試験プロセスの撤去は区別する。[2][5]

## 証拠索引

以下は非公開の調査資料に対する、対象 repro ディレクトリからの相対パス。公開文書に生ログ・個人識別子・接続情報は含めない。

- [1] `investigation-20260929/status-and-trials.md`
- [2] `investigation-20260929/FOLLOWUP.md`、`investigation-20260929/trial-summary.json`
- [3] `investigation-20260929/trial-i4-findings.md`
- [4] `investigation-20260929/trial-j-findings.md`
- [5] `investigation-20260929/trial-k-rapid-escape/README.md`
- [6] `investigation-20260929/trial-l-pause-resize/README.md`
- [7] `investigation-20260929/trial-m-child-exit/README.md`
- [8] `investigation-20260929/cef-native-identity/README.md`
- [9] `investigation-20260929/cef-diagnostic-20260930/provenance/op-status.md`
- [10] `investigation-20260929/cef-diagnostic-20260930/provenance/q-prepared-status.md`
- [11] `investigation-20260929/cef-diagnostic-20260930/provenance/q-result-status.md`
- [12] `investigation-20260929/cef-diagnostic-20260930/provenance/r-result-status.md`
- [13] `investigation-20260929/cef-diagnostic-20260930/provenance/s-status.md`
- [14] `investigation-20260929/cef-diagnostic-20260930/provenance/t-status.md`
- [15] `investigation-20260929/cef-diagnostic-20260930/provenance/u-status.md`
- [16] `investigation-20260929/cef-diagnostic-20260930/provenance/v-status.md`
- [17] `investigation-20260929/cef-diagnostic-20260930/provenance/w-status.md`
- [18] `investigation-20260929/cef-diagnostic-20260930/x-pretrial.md`、`investigation-20260929/cef-diagnostic-20260930/x-resume-1.md`
- [19] `investigation-20260929/cef-diagnostic-20260930/trial-x-attempt/evidence/findings.md`
- [20] `investigation-20260929/cef-diagnostic-20260930/trial-y-attempt/evidence/findings.md`、同ディレクトリの `outcome-review.md`
- [21] `investigation-20260929/cef-diagnostic-20260930/trial-z-result/README.md`、同ディレクトリの `actual-z/` と `transport/`
- [22] `CURRENT-STATE.md`
