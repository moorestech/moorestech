# tick同期の機械的処理をstream単位で共通化する

2026-09-26。train/railが所有するtick同期を、セッションtickの時計・streamごとの採番・順序バッファ・クライアント進行計算と、ドメイン固有の差分・hash・snapshotに分離する。現在のtrainを実利用者として維持し、共通部はTrain/Rail型を参照しない。

## 目的と出所

- 出所: ユーザー裁定 2026-09-26「差分通知の部分だけ、今回の実装を踏まえmasterブランチからリファクタリングPR作成」「鉄道べったりだったのを今後ギアやベルコンの差分通知に拡張する必要があるので、まずはこれだけ単体PR」。gear/belt本体・GPU移植はこのPRへ入れない。
- 出所: 既存ユーザー裁定「tickとseq idの仕様はこのまま」。tickとseq idの仕様を維持する。
- 出所: agent前提（既存実装の互換性調査）。上記の仕様維持を、tick内採番・hash(n-1)+diff(n)・空diffのsimulationトリガ・watermarkの意味を保つこととして実装する。
- 出所: ユーザー裁定 2026-09-26「あなたはオーケストレーションに徹して。レビューはまた別にsol起動してやって」。実装と独立レビューは別担当とし、レビューにはsolを使う。
- 出所: ユーザー共通AGENTS.mdの別指示（2026-09-24「レビュー用サブエージェント」）。レビューのmodelを`gpt-6-sol`、reasoning effortを`high`に明示する。

## 時計とstreamの所有権

出所: ユーザー裁定 2026-09-26（最終review D2/A）「全体更新の開始時に時計を進める」「これ採用します」。streamごとの採番所有は既存agent前提を維持する。

永続累積tickの `GameUpdater.CurrentTick` と通信のセッション内uint tickは別物である。共通の `ServerTickClock` は後者を所有し、`MasterTickUpdater.Update` の入口で旧tickを保持して1回だけ進める。電力・gear・fluid更新後の既存train境界で保持した旧tickのtrain hashを発火し、train/rail streamの `TickSequenceState` を新tick/seq0に進め、その後trainをsimulateする。電力・gear・fluid・train・block更新の順序とhash計算位置は維持する。将来のgear通知handlerはDIされた同じ `ServerTickClock.Tick` をgear更新中に読めば当該更新のtickを得られる。通知の採番はそのstreamの別 `TickSequenceState` が所有する。初期wire tickは0、最初の全体更新中は1となる。

seqはstreamごとの状態であり、全ドメイン共通の採番singletonにしない。現在のtrainとrailは従来どおり1stream。trainのsequence ownerはsimulation serviceから分離して各packetが参照する。別domainのownerは別のsequence stateを持ち、同じtickを入力できる。別streamのイベントをtrainが受信しないことによるseqの穴を作らない。

クライアントの適用済み位置、最大buffer tick、event buffer、推定tick計算もstreamごとのinstanceである。trainのdomain contextがそれらを所有し、network handlers、snapshot appliers、simulator、debug表示を接続する。snapshot watermarkのpurgeはそのcontextだけへ作用する。

## 共通部とtrain側の責務

出所: agent前提（既存 `TrainUnitTickState`、`TrainUnitFutureMessageBuffer`、`TrainUnitClientSimulator`、`TrainUnitHashVerifier` の役割分離）。

共通部は `((ulong)tick << 32) | seq` の比較、exact-key event flush、古いevent破棄、単調な適用位置、フレームからの進行計算を持つ。train/rail hashの型、4tick間引きとdummy、欠落時のforce-slip、hash不一致時のfatal終了、snapshot生成・cache/view置換はtrain側に残す。既存のgate interfaceをドメイン非依存の進行gateへ移し、train verifierが実装する。

driverからviewを購読して動かす機構へ変えず、既存ITickableのtrain simulatorが共通driverを明示的に呼び、その結果でtrain visualを更新する。既存機構への受動的統合は、現行streamをそのまま接続して機械的処理だけ委譲する形で実現する。別の並行simulation、凍結対象リスト、汎用domain登録registryは導入しない。

## 同期と非同期の境界

出所: ユーザー裁定 2026-09-26（最終review D1/C）「このPRで再同期を廃止し、hash不一致時の終了まで実装する（推奨）」。初回snapshot成功後に順序付き差分で進行する。railとtrainのpayloadは同じwatermarkで、両cacheのhash一致・view構築成功を確認してから初期完了を通知し、player runtimeと乗車復帰を開始する。null・stale・適用例外・hash不一致では完了しない。空listは有効である。

snapshot外部境界は失敗を区別可能な例外として初期待機へ届け、同期replayへrethrowしない。検出した同期境界でstream停止と保存なしfatal終了を先に確定し、その後で初期待機をfaultにする。以後の受信・event flush・visual更新を止め、地形構築を待って終了を遅延させない。finalizerはdispatch直後・地形開始前にtrain初期待機を観測する。InitializeScenePipelineはこのtyped failureを通常の保存終了やメニュー復帰へ流さず、既に確定したfatal終了へ接続する。実行中のtrain/rail hash不一致もdomain/tick/期待値/実値をError記録して同じ終了口へ入る。

GameShutdownEventが異常理由と終了許可を先に確定し、保存参加者・remote save・embedded ShutdownAsyncを起動せずaffected clientを終了する。exit-intent/clean-exit印と再起動は作らない。EditorはPlayModeだけ停止する。外部専用serverは停止しない。二重通知を防ぎ、次sessionで終了flagをresetする。初期snapshot待機・main-thread dispatch・通常通信待機は維持し、resync要求/応答/ack待機/完了通知と選択的rail取得modeを削除する。

出所: ユーザー裁定の既存記録 [.decisions/2026-08-03-Train適用失敗のrethrowは削除しADRを正とする.md](../../.decisions/2026-08-03-Train適用失敗のrethrowは削除しADRを正とする.md)。

## 今回の境界

出所: agent前提（実装範囲）。保存形式、通常eventのMessagePack key/tag、hash cadence、dummy、catch-up係数は維持する。期待hashがなく未来hashがあるforce-slipは証明済み不一致ではないため維持する。seq/wire tickは永続化しない。機械的共通部を2つの非train fixtureで動かし、採番・適用・watermarkが相互に干渉しないことを確認する。実際のgear/belt adapterは別PRで接続する。
