# UnityのFence待機・登録・早期再利用候補

## 結論

**未完了のFence待機から、要求targetを使った回収・Resetへ進み得る静的経路を特定した。ただし元実行がその条件を満たした証拠はなく、Close E_FAILの原因とは確定していない。**

同じeventの古い通知や登録重複も、未達のまま早く戻る候補として調べた。通常のScreenshotと録画readbackを「同じeventを共有する二者」とする直接の説明には、別Fenceを構築する実装上の反証がある。

## 元版の待機処理

Unity 6000.3.8f1 の一致DLL/PDBでは、D3D12Fenceごとに名前なし・初期nonsignaledのauto-reset eventを1個作り、Waitごとには作り直さない。WaitとWaitWithMessagePumpは同じevent fieldを使う。

通常Waitの静的処理は次の通り。

1. 完了値がtarget以上ならreturnする。
2. 未達なら同じeventをSetEventOnCompletionに登録し、そのHRESULTを検査する。
3. 30秒または60秒のCPU待機を呼ぶが、戻りDWORDを条件判定しない。
4. 完了値を再照会し、未達なら警告を出してreturnする。target到達まで再待機するloopはない。

未達警告は通常Waitとmessage pump付き待機で共通の処理から出る。ログに `D3D12Fence::Wait` とあっても、どちらの待機routineだったか、timeout/WAIT_FAILED/早い通知のどれだったかは区別できない。

元の3警告はtarget/completedがそれぞれ `2837/2836`、`337361/337359`、`359163/359162`。同じFenceの系列と結合するidentityがないので、数値順に一つのFenceの経歴として並べない。

## 登録とevent通知の候補

| 候補 | 成立に必要な条件 | 元実行での不足 |
|---|---|---|
| 以前のtargetの通知を後の待機が消費 | 未達復帰後の旧登録が通知し、同eventのsignaled状態が次のtargetまで残る | 登録・通知・CPU waitの時系列、同一Fence/event |
| 異なるtargetの同時待機 | 同event上の低target通知が高target側のwaiterを解放する | 実際の待機期間の重なり、外側の直列化の有無 |
| 無効eventで待機が失敗 | 無効/閉じたhandleをCPU待機へ渡す | 元handle、破棄時刻、待機戻り値、LastError |

Wait routine自体にResetEventや待機の直列化lockがないことは確認した。ただしauto-reset eventには通常の成功待機で明示ResetEventは不要であり、「ResetEventがない」だけをバグとは扱わない。外側のdispatcherやqueueで順序が保証されている可能性も残る。

SetEventOnCompletionのthread-safetyと、異なるtargetを待つ複数CPU waiterに1個のeventが正しく通知を配る保証は別である。event登録失敗のHRESULTはUnityが検査するため、登録OOMを無検査で見逃す単純な説明とは一致しない。

通常構築でScreenshot側のFenceとAsyncReadback側のFenceは、別allocation・別constructor・別event作成を通る。別fieldだからとの推測だけではない。また元録画C#は非同期Request/callbackを使い、blocking WaitForCompletion/WaitAllRequestsを呼ばない。readbackの通常Updateはpollで、強制完了側のUpdateだけがCPU event待機へ進む。

従って「Screenshotと録画が並行したから同eventの登録が重複した」とは説明できない。同じ描画Fenceを使うFlush/EndOfFrameBookkeeping/FinishRendering間、複数readback間の同一Fence共用は候補として残るが、別callerがあることは同時待機の証明ではない。

## 独立CPU/WARP試験で再現した機構

ゲームから独立したWARP deviceでFence/eventの公開APIだけを使い、CPUのFence::Signalで進行値を変えた。queue、command list、allocator、textureは作成せず、描画/GPU仕事を投入していない。この試験は元クラッシュではなく、通知とCPU待機の機構を切り分けるものだった。

最初はsystem Core、その後は元モジュール記録と一致確認済みのAgility D3D12Core 1.618.1.0を独立device factoryで選択した。後者はFenceのGetCompletedValue実装addressが属するmoduleとSHA256を確認し、同名DLLをロードしただけの判定を避けた。元実行のmapped全byteや通知処理全体の同一性を保証するものではない。

| 与えた条件 | hash照合済みAgility Coreでの結果 |
|---|---|
| target1未達の10ms待機をtimeoutさせ、後からSignal1し、同eventをtarget2に再利用 | 3/3で即時待機success、完了値1のままtarget2未達 |
| target1の旧通知を消費してからtarget2を登録 | 3/3でtimeout、完了値1 |
| 新規nonsignaled eventにtarget2を登録 | 3/3でtimeout、完了値1 |
| 新規eventを明示的にsignaled化してtarget2を登録 | 3/3で即時待機success、完了値1 |

各条件の最後にSignal2し、target2の完了と待機成功を確認した。system Coreでも同じ方向の結果だった。最初のsystem試行は外側ExitCodeが欠測であり、完走ログだけからexit0扱いせず、監督を修正した別runで終了codeも確認した。

別probeでは、1本のCPU threadから同eventへ未達target1とtarget2を両方登録し、Signal1後にtarget2側の待機を行った。登録順を逆にした条件も含め各3/3で早期successとなった。別eventの対照とtarget2だけの登録では各3/3でtimeoutし、target1側の対照通知は成功した。全15試行（5条件×3回）の最後はSignal2後に成功した。

これは先行timeoutや明示SetEventがなくても、**未処理登録の重なりを人為的に与えれば**早期復帰が起きる証拠である。2本のthreadの同時CPU待機でも、Unity自身が登録を重ねた再現でもない。CPU Signal、WARP、OS/driverは元のGPU通知経路と異なり、Core照合だけでその差は消えない。

初期nonsignaled、直列待機、登録ごとに対応通知を1回消費、外部通知・旧登録なし、Fence値単調という正常系列のモデルでは、通知は成功待機で消費され、古いsignalは自発的には蓄積しない。完了済みfast-pathも新規登録を作らない。従ってこの機構だけで最初の異常を説明できず、通知を消費しない離脱や登録重複などの初期条件が別途必要である。これは明示した仮定内の論理であり、未知のruntime通知処理全体を否定する定理ではない。

なお、CPU待機がtimeoutした直後、再照会までにtargetへ到達すれば、Unityは未達警告を出さずに戻り、未消費通知が残る説明も成立し得る。先行警告がないことだけでも否定できないが、そのraceが元で起きた証拠もない。probeで確認した早期復帰から元の最初のFence未達やClose E_FAILへは、依然として同一objectの時系列が欠けている。

## 未達後の回収・Reset候補

元版のEndOfFrameBookkeepingは、ring内の要求targetを保存する。初回照会で既に達成していれば実完了値へ置換するが、未達ならWaitを呼んだ後も保存した要求targetを回収閾値として使う。

回収poolはentryのFence値が閾値以下ならcommand list wrapperをResetする。stateが対象状態ならallocator Reset、次いでlist Resetを行い、それぞれHRESULTを検査する。Reset自体には追加のFence完了検査がない。

この経路が不正再利用になるには、少なくとも次の全条件が要る。

- Waitが未達のまま戻った。
- Reset時点でも実完了値が要求target未満だった。
- 実完了値より大きく要求target以下のentryが実際にpoolにあった。
- そのwrapperがReset対象状態で、同じallocatorを使うGPU仕事がまだ実行中だった。

元記録はこれらを同一object上で結合していない。Wait後からResetまでに完了が進む場合もある。GPU使用中のallocator Resetは未定義動作となり得るが、必ずReset自身がE_FAILを返すとは限らない。逆に後のCloseが失敗しただけでも、この経路の成立は証明できない。

同様の局所的なWait→Reset経路や、available poolから取り出してResetする経路もあるが、local Waitの有無だけで安全性を決めない。poolへ戻した時点の条件が必要である。

## Screenshotとの静的な接続

ファイル名だけのScreenshot要求から、通常倍率の描画device dispatch、workerへ送るcommand、D3D12 CaptureScreenshot、Flushへ進む静的経路を照合した。固定vtableとworker dispatchは原heapのreceiverを回収したものではない。

元の最後の要求が消費されたか、direct/workerのどちらを通ったか、どのFenceを待ち、どのlistに記録したかは未観測。server snapshotの完了や画像ファイルの有無から、この接続を代替できない。

## Unity版比較で分かったこと

公式Windows Mono support packageを実行せず展開し、DLL/PDBのidentityを確認して限定比較した。元 .8、修正前後を絞る .13/.14、後続 .18 を対象とした。

| 版 | EndCurrentRenderSubPassで確認した相違 | 共通経路への判断 |
|---|---|---|
| 6000.3.8 / .13 | 後述の上限超過時追加blockなし | 元版のWait未達後に要求targetを保持する方針は存在 |
| 6000.3.14 | outstanding countが50超で強いflush。提出された場合に同期・signal・wait・回収を追加 | Wait/フレーム末尾の要求target回収は残る |
| 6000.3.18 | 閾値500超で、完了値の更新があればその値で回収・trim・signal。さらに回収後も2000超なら待機へ | 高閾値側と共通フレーム末尾にWait→要求target回収が残る |

.18の分岐には既存のrender/environment gateとflushの提出結果条件もある。単純に「countが2000超なら必ず待つ」でも「500超なら全て待たない」でもない。

.14の差はUUM-138597のpending command buffer蓄積時のflush説明と整合するが、公開source patchとの一致ではなく、分離した版差とrelease noteによる帰属推論である。.18の差もUUM-139697の説明と整合するが、.14→.18には複数修正があり、閾値変更を全て同issueへ帰属しない。

後発の上限処理が変わったことは、元 .8 に元々なかったblockの変化でもある。これを元クラッシュの修正証明にしない。共通Wait/回収が残ることも、それが元原因だったという逆向きの証明にはならない。

## その他の公式候補

| 候補 | 接点 | 同一原因と判定できない理由 |
|---|---|---|
| UUM-138597 | 近いPrepareExecute/DoExecute stack、pending蓄積への修正 | 元のpending数、render gate、list/allocator履歴なし |
| UUM-141699 | DX12のmemory-intensive操作でのcrash修正 | 元版の適用範囲、失敗API/HRESULT、具体条件不明 |
| UUM-127793 | render-command転送メモリ削減・診断改善 | 元の確保失敗箇所・消費主体を同定しない |
| UUM-131824 / 131707 | scratch allocation関連の修正 | 131824公開stackはPresent側で、元Closeとは異なる |
| UUM-128104 / 135024 | copy queue trackingの時点変更 | 元copy queue/resource状態、同一stack・再現条件なし |
| UUM-139697 | 上限超過時の待機方針変更 | 共通の要求target回収は残り、元runtimeとの対応なし |

これらは候補metadataである。新版で通常操作が成功しても、同じ故障を起こす比較baselineがなければ、どの修正が作用したかを判定できない。

## 実機ZのFence観測の限界

最終試行Zでは、native 6,761 event、欠落0、sequence重複なしの記録を得た。allocator/list世代、device epoch、Fence identityを照合し、介入Resetをそのallocatorの直前Execute記録へ結合できた。比較可能な3件の完了値はいずれも対応marker以上だった。

ただし公開frame fenceの大小は、特定のlistがGPU上で完了した証明ではない。元のUnity内部の失敗listとの共通IDもない。初期の未取得区間はMissingとして保持し、測れなかった区間を正常とは数えていない。

既存CEF Executeが走ったthreadと、正式renderer callbackのthreadは別だった。これはcaller契約上の懸念を補強するが、内部workerへの転送とCPU同期も存在する。thread差だけで競合writerや元クラッシュ原因を確定できず、正式callbackでFenceを取得した事実が既存Executeのcaller契約を修復するわけでもない。

Zに元Close E_FAILの再現はなく、これらの観測をallocator原因の除外やCEF無罪の証明にも使わない。

## 未解決条件と終了判断

原因を区別するには、元と同じ失敗実行で、最初のvalidation errorとlist/resource、または同一allocatorの提出・完了・Resetと失敗Closeを結ぶ履歴が必要である。event説には同一Fence/eventの登録target・通知・待機戻り・寿命がさらに必要になる。

保存資料の再読や通常操作の反復だけではこれらは得られない。今回の調査は条件付きの実装候補と欠落を明確にした段階で終了し、原因確定・修正完了とは記録しない。

## 非公開証拠索引

基準は調査reproディレクトリ。証拠固定commitは `5dad531e25f8af56764967643145ae17b3e14a1d`。

- `investigation-20260929/fence-event-lifecycle-review.md`
- `investigation-20260929/fence-registration-failure-review.md`
- `investigation-20260929/screenshot-registration-causal-refinement.md`
- `investigation-20260929/screenshot-registration-integration-review.md`
- `investigation-20260929/allocator-reuse-static-review.md`
- `investigation-20260929/allocator-reuse-review-root.md`
- `investigation-20260929/unity-binary-comparison/binary-comparison-findings.md`
- `investigation-20260929/cef-diagnostic-20260930/provenance/u-official-release-review.md`
- `investigation-20260929/cef-diagnostic-20260930/unity318-static/binary-comparison-findings.md`
- `investigation-20260929/cef-diagnostic-20260930/official-patch-followup/status.md`
- `investigation-20260929/cef-diagnostic-20260930/trial-z-result/actual-z/fence-thread-findings.md`
- `investigation-20260929/cef-diagnostic-20260930/z-causal-followup/status.md`

- `investigation-20260929/fence-event-probe-findings.md`
- `investigation-20260929/fence-agility-probe-findings.md`
- `investigation-20260929/fence-overlap-probe-result-review.md`
- `investigation-20260929/stale-event-causal-prerequisites.md`
