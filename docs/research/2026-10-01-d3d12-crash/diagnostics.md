# 再利用できる診断と、その証拠の限界

元の D3D12 Close 相当 E_FAIL は未再現・原因未確定。ここでは、原因を確定した道具ではなく、何を観測でき、何が欠けると結論を出せないかを記録する。試行の実施状態は `trials.md` を参照。

## 実行記録の単位

試験の実体は、ビルド・入力・ゲーム PID と開始時刻・操作結果・観測区間・終了と復元記録の組である。ファイル名、プロセス名、controller の終了コードだけでは成立を判定しない。Z の native 出力は歴史的な X 名のままなので、名前から X の測定へ分類すると誤る。[1]

ログ・診断に混在する過去起動分は、現在の client/server 識別、時刻、native header で区分する。CEF helper と server は別の役割で、片方の終了や残存をもう片方の寿命へ代用しない。既存証拠は上書きせず、取得元と保存後のhashを照合する。[1][2]

## 1. プロセス・資源・所有権の監視

周期計測はゲーム、録画子、CEF の private bytes、working set、CPU秒、PID/開始時刻を採る。安全監視は温度・GPU電力・空きメモリと有効なセンサー値を別に確認する。入力時は所有ゲームの前面状態を確認し、異常時の停止対象も所有 PID と開始時刻へ限定する。[3]

- 約32分の初期ログは再起動を跨ぎ、同一プロセス30分ではない。I4だけが同一ゲーム30分の成立記録を持つ。
- 周期標本間の短命な録画子、瞬間ピーク、並行状態遷移は見落とし得る。列挙上の録画子最大1を、瞬間的な重複なしの証明にしない。
- プロセス列挙は同じ Windows Job の構成員一覧ではない。private bytes の合計と Job accounting 値を無条件に同一視しない。
- 取得 API の違う空き commit/仮想メモリ値を混ぜない。温度・電力の観測最大値も瞬間上限の保証ではない。
- 動的な診断コードは新しい assembly を読み込む。測定頻度自体がゲームのメモリ増加に寄与し得るため、外部の周期計測と少数のゲーム内部 snapshot を分ける。
- controller の終了はゲーム・監視・録画子の終了確認ではない。別々に確認し、通信や worker の完了不明を成功へ変換しない。

## 2. 録画状態の読取り

既存 GameFrameRecorder から、録画使用可否、current child の生存、readback in-flight、pool/queue、ring、RenderTexture 識別・寸法・生成状態を読める。実行ビルドの field と実配布 managed DLL を先に照合する。反射は非原子的であり、各項目が完全に同一時点とは限らない。[4]

M は child 退出後に pool2/queue0、writer生存、readback停止、RT不変を記録した。この観測は「子退出だけでは pool 枯渇を再現しない」条件を区別する。一方、G/Hには元の BrokenPipe と同じ writer 例外の記録がなく、G/Hおよび後続の保存18ログから pool 枯渇の実機再現を主張できない。A以前の前報にはBrokenPipeの実施報告があり、詳細は `trials.md` の冒頭で区別する。[4][5]

## 3. 既存 D3D12 InfoQueue の読取り

Unity が既に所有する録画 RT の resource から device をたどり、InfoQueue の mute、storage/retrieval filter、破棄数・件数・メッセージを読む。新 device 作成、filter変更、clear、GPU命令追加は行わない設計。[6]

- 1回最大32件、メッセージとfilterの各 native buffer は16KiB上限。HRESULT・実返却長・内部ポインター範囲を検査し、欠損を明示する。queue は並行更新されるので atomic snapshot ではない。
- Unity の借用 resource pointer を直接 Release しない。QueryInterface/GetDevice で取得した所有参照と、取得した RCW の回数を分け、一回ずつ対応させる。共有 RCW を強制全解放しない。
- 実配布 Mono の GetUniqueObjectForIUnknown は未対応だった。採用版は実配布 IL で RCW cache/refcount 経路を確認した。通常の .NET API 名が存在するだけでは実行可能としない。
- GetNativeTexturePtr は描画同期を伴い得る。故障の瞬間を変えない計測ではなく、安定状態の確認用途として使った。
- G 後段では mute0、保持22件は全て Warning、操作後も増加なし。storage filter は INFO と一部 ID を除外していた。特定の共有 resource 検証も見えないため「D3D12検証が全て正常」としない。

## 4. CEF の限定 native observer

固定 source の隔離コピーへ観測だけを加え、既存 allocator/list/resource の識別、Reset/Close HRESULT、既存 Unity submit の入口・戻り、共有 frame の受信と return、InfoQueue callback を記録した。製品 plugin の恒久的な置換や修正ではない。元 DLL を試行前後のhashで復元した。[2]

callback は固定長のイベントを上限付き queue へ送り、専用 writer が保存する。callback 自体は file I/O や GPU API を追加しない。INFO本文の保存を抑えて件数だけ残す改訂で、O の容量到達問題を対処した。[2][7]

明示終了要求は観測受付を閉じ、受付済み書込みの静止を期限付きで待ち、callback解除と queue排出、accepted/written/dropped/missing を footer へ残す。これは **GPU完了待ちではない**。明示終了後の描画と Quit は記録対象外。[2]

配送の完全性と、目的操作の網羅性は別の判定にする。Q は配送完了でも介入未到達、R は単一介入成立でも対照なし、Z は個別条件成立でも汎用判定器の操作名不一致を保持した。drop0だけで「再現試験完了」としない。[7][8]

producer の既存統計ログは、pump/BeginFrame/paint と publication/adoption の停止位置を狭める。同期的で無期限になり得るログ取得 API は gameplay 計測へ追加せず、既存ログを後から解析した。ログ出力自体も処理・I/O時機を変える。[2][7]

## 5. 正式 callback・fence identity と保守的な判定器

公開 rendering callback での取得、canonical COM device/fence、allocator/list世代、registry revision/device epoch、正確な先行 Execute の戻り、Reset前のQPC読取りを別々に記録する。allocator世代とresource世代は異なる軸で、数字が同じでも同一寿命として結合しない。[9]

- native QPC と managed Stopwatch は周波数が同じでも原点が一致しなかった。両側の実 Windows-QPC 契約がない記録は、位相の絶対時刻比較へ使わない。
- helper の成功は実 queue Wait の実行を意味しない。target0、fence不在、既待機targetなど、成功の早期 return がある。
- GetCompletedValue の UINT64_MAX、同一性変更、履歴欠損、時計不一致、読取り順序不整合は通常の完了値へ昇格させない。
- **completed >= returned marker も、completed < marker も、それだけで個別 list の完了・実行中を確定しない。** 既存 Execute の caller 契約・marker意味と失敗 list の帰属が別に必要。
- registry の競合・参照所有 fixture、Windows向けABI/type検査、classifier の異常入力検査は CPU の成立条件を検証する。Unity callback到達・COM/driver実行は後段の実機記録を要する。
- 観測用 callback の挿入、保持参照、ログ、debug layer はタイミングを変える。観測版で無事だったことを、元配布版の安全性へ一般化しない。

## 6. CPU-only の機構試験

| 試験 | 実際に確かめたこと | 元クラッシュと区別する点 |
|---|---|---|
| WARP の旧 event 通知 | 旧通知を残すと、新 target 未達のまま event wait が成功。新event・通知消費済み対照では timeout。[10] | 元ゲームの fence/event と同じ実物でも、元の通知履歴でもない。 |
| 元 Agility Core と一致する WARP | Core hashと関数帰属を確認して同機構を再確認。[11] | CPU software 実装。元 OS/driver/GPU 経路を再現していない。 |
| target 登録の重なり | 同一eventへ低・高targetを登録し、低target通知で高target側waitも早期成功。共用6件成功、別event等9件timeout、最終15件完了。[12] | 1プロセスで未処理登録を意図的に構成。二つのCPUthreadが同時waitした試験ではなく、元の重なり発生は未証明。 |
| 録画 pool の小試験 | 原 FfmpegProcess/FrameBufferPool を変更せず実pipe破断。2×64KiB fixtureでpool枠喪失・queue残存を再現。[13] | GPU/GameFrameRecorder/Windows Mono を実行せず、元のthrow siteやpool状態を確定しない。 |
| writer 失敗後の6条件 | 2×256KiB fixture。失敗前から2枚目がqueueにあるだけでpool0/queue1が残存。pool1なら後続childへ送信可、pool0なら0枚。[14] | 原で当該queue条件が成立した証拠はない。1 recorderの配列上限約7.03MiBで大きなcommit差を説明しない。 |
| PNG回収のfixture | 既存PNGのみの容量制限付きコピー、source/hash維持、境界・reparse拒否など5条件を検証。[15] | テスト用fixtureであり、報告者の実資料を新たに回収したものではない。保存成功とPNG/元capture帰属も別に検証する。 |

これらは具体的な機構や別不具合の存在を示す。原クラッシュへの因果を埋めるには、元でその条件が成立した証拠と、失敗した Unity list までの接続が必要である。既に成立した CPU 故障を繰り返すだけでは、その欠落を補えない。[16]

## 7. 次回に持ち越す記録原則

最初の不正な API と list/resource/allocator、先行 wait の target/completed/event、録画 writer の throw site・child世代・queue/pool を時間と同一性で結べる記録があれば、候補を区別できる。現在の原dumpには必要なheapや履歴がなく、サーバー側capture保存や動画の正常decodeで代替できない。[16]

検査が拒否したときは理由を保存する。X/Yでは「タイトル以前の不適切な前提」「前面所有権の不成立」を、ゲーム故障や安全審査拒否へ誤分類しない。Xでは画面状態・所有前面の確認前に入力した操作誤りも残り、影響は不明である。これを対象ゲームへの正しい入力と数えない。開始成功・task受付・console空・transport終了を、操作完了の代用にしない。[1][17]

新しい情報がない同条件の正常試行を重ねても、元の機構を除外する根拠にはならない。調査は未確定のまま終了したのであり、製品修正の有効性を検証済みとする成果ではない。

## 最後に確認した終了状態

最後の機器実測は **2026-09-30 13:31:45.748 UTC**。元DLL・world27ファイルのhashと個数を復元、一時SDK layer無し、自動pagefile、試験game/task/process不在、録画と監視の終了、他用途の既存プロセス識別維持を記録した。Zの242標本は最大64°C/95.55W、最小空きcommit10286MiB・物理12101MiB。その後の資料監査は現在の機器状態の再確認ではない。[1][16]

## 証拠索引

以下は非公開資料の、対象 repro ディレクトリからの相対パス。接続・機器操作の実行手順を公開する索引ではない。

- [1] `investigation-20260929/cef-diagnostic-20260930/trial-z-result/README.md`、`investigation-20260929/cef-diagnostic-20260930/trial-y-attempt/evidence/outcome-review.md`
- [2] `investigation-20260929/cef-diagnostic-20260930/tooling/README.md`
- [3] `investigation-20260929/status-and-trials.md`、`investigation-20260929/trial-i4-findings.md`、`investigation-20260929/process-memory-causal-refinement.md`
- [4] `investigation-20260929/recording-lifetime-findings.md`、`investigation-20260929/trial-m-child-exit/README.md`
- [5] `investigation-20260929/remaining-recording-causes/README.md`
- [6] `investigation-20260929/tools/infoqueue/README.md`、`investigation-20260929/FOLLOWUP.md`
- [7] `investigation-20260929/cef-diagnostic-20260930/provenance/op-status.md`、同ディレクトリの `q-result-status.md`
- [8] `investigation-20260929/cef-diagnostic-20260930/provenance/r-result-status.md`、`investigation-20260929/cef-diagnostic-20260930/trial-z-result/actual-z/root-validation.json`
- [9] `investigation-20260929/cef-diagnostic-20260930/provenance/v-status.md`、同ディレクトリの `w-status.md`
- [10] `investigation-20260929/fence-event-probe-findings.md`
- [11] `investigation-20260929/fence-agility-probe-findings.md`
- [12] `investigation-20260929/fence-overlap-probe-findings.md`
- [13] `investigation-20260929/recording-buffer-probe-findings.md`
- [14] `investigation-20260929/post-failure-state-probe/README.md`
- [15] `investigation-20260929/capture-recovery/README.md`
- [16] `CURRENT-STATE.md`、`investigation-20260929/original-queue-occurrence/README.md`、`investigation-20260929/final-screenshot-command-link-review.md`
- [17] `investigation-20260929/cef-diagnostic-20260930/trial-x-attempt/evidence/findings.md`
