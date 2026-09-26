# tick同期の機械的処理をstream単位で共通化する

2026-09-26。train/railが所有するtick同期を、セッションtickの時計・streamごとの採番・順序バッファ・クライアント進行計算と、ドメイン固有の差分・hash・snapshotに分離する。現在のtrainを実利用者として維持し、共通部はTrain/Rail型を参照しない。

## 目的と出所

- 出所: ユーザー裁定 2026-09-26「差分通知の部分だけ、今回の実装を踏まえmasterブランチからリファクタリングPR作成」「鉄道べったりだったのを今後ギアやベルコンの差分通知に拡張する必要があるので、まずはこれだけ単体PR」。gear/belt本体・GPU移植はこのPRへ入れない。
- 出所: 既存ユーザー裁定「tickとseq idの仕様はこのまま」。tick内採番・hash(n-1)+diff(n)・空diffのsimulationトリガ・watermarkの意味を維持する。
- 出所: ユーザー裁定 2026-09-26「あなたはオーケストレーションに徹して。レビューはまた別にsol起動してやって」。実装と独立レビューは別担当とし、レビューはgpt-6-sol/high。

## 時計とstreamの所有権

出所: agent前提（`GameUpdater`、`TrainUpdateService`、`MasterTickUpdater`の現実の呼出順、AGENTS.mdの汎用基盤へのドメイン語彙混入禁止）。

永続累積tickの `GameUpdater.CurrentTick` と通信のセッション内uint tickは別物である。共通の `ServerTickClock` は後者を所有し、MasterTickUpdaterの既存train更新位置で1回だけ進める。直前に旧tickのtrain hashを発火し、直後にtrain/rail streamの `TickSequenceState` を新tick/seq0に進め、その後trainをsimulateする。電力・gear・fluid・block更新の順序は維持する。

seqはstreamごとの状態であり、全ドメイン共通の採番singletonにしない。現在のtrainとrailは従来どおり1stream。trainのsequence ownerはsimulation serviceから分離して各packetが参照する。別domainのownerは別のsequence stateを持ち、同じtickを入力できる。別streamのイベントをtrainが受信しないことによるseqの穴を作らない。

クライアントの適用済み位置、最大buffer tick、event buffer、推定tick計算もstreamごとのinstanceである。trainのdomain contextがそれらを所有し、network handlers、snapshot appliers、simulator、debug表示を接続する。snapshot watermarkのpurgeはそのcontextだけへ作用する。

## 共通部とtrain側の責務

出所: agent前提（既存 `TrainUnitTickState`、`TrainUnitFutureMessageBuffer`、`TrainUnitClientSimulator`、`TrainUnitHashVerifier` の役割分離）。

共通部は `((ulong)tick << 32) | seq` の比較、exact-key event flush、古いevent破棄、単調な適用位置、フレームからの進行計算を持つ。train/rail hashの型、4tick間引きとdummy、欠落時のforce-slip、hash不一致時のresync、snapshot生成・cache/view置換はtrain側に残す。既存のgate interfaceをドメイン非依存の進行gateへ移し、train verifierが実装する。

driverからviewを購読して動かす機構へ変えず、既存ITickableのtrain simulatorが共通driverを明示的に呼び、その結果でtrain visualを更新する。既存機構への受動的統合は、現行streamをそのまま接続して機械的処理だけ委譲する形で実現する。別の並行simulation、凍結対象リスト、汎用domain登録registryは導入しない。

## 同期と非同期の境界

出所: agent前提（最新masterの実装調査）。差分Applyとsnapshot cache/view適用は既に同期voidである。`SendTrainResync` の単なる `return await` はUniTask直接返却へ畳めるが、要求自体は非同期のまま。ack失敗時に現役resync gateだけを解除する待機、初期snapshot到着/適用完了source、main-thread受信dispatchのYieldは残す。

初期snapshotはrail→trainの順でhandshake応答より前にpushされる。train snapshotのcache/view生成が終わってから初期完了を通知し、その後にplayer runtimeと乗車復帰を開始する。失敗はLogErrorと待機sourceのfaultで伝え、同期replayへrethrowしない。

出所: ユーザー裁定の既存記録 [.decisions/2026-08-03-Train適用失敗のrethrowは削除しADRを正とする.md](../../.decisions/2026-08-03-Train適用失敗のrethrowは削除しADRを正とする.md)。

## 今回の境界

出所: agent前提（リファクタリング範囲）。MessagePack key/tag、保存形式、hash cadence、catch-up係数、欠落時の判断、stale snapshot完了通知の既存挙動を変えない。seq/wire tickは永続化しない。機械的共通部を2つの非train fixtureで動かし、採番・適用・watermarkが相互に干渉しないことを確認する。実際のgear/belt adapterは、それぞれのpayload・hash・世代復旧契約を決めるPRで接続する。
