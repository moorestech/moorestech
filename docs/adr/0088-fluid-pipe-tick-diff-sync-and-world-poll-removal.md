# 0088: パイプの流体状態をtick差分同期へ移し、全ワールド取得ポーリングを廃止する

## Status
Accepted

## Context
ユーザー原文「さっき議論したパイプのtick差分ももうそろそろとりかかるのでどれからやればいいか考えて」「じゃあこのブランチにすすめて 全部まとめて1PRにするので」

2026-10-22 の実測（Editor、8628ブロックのベルト世界、10秒）: ブロック状態変化イベントは 10,633件/秒、うち鉄のパイプが 4,484件/秒。パイプは `FluidNetworkDatastore.NotifyPipeStates` が毎tick全パイプ分を発火し、1件ごとにサーバーで直列化3回・クライアントで逆直列化2回とバイト列コピーが走る。クライアントのメインスレッド割り当て 439KB/フレームのうち約半分は `WorldDataHandler` の全ワールド取得（毎秒10回、全ブロックの座標・ID・向き・InstanceId を取り直して同一IDなら即returnする空振り）。これらの割り当てが約66MB/sとなり、5〜6秒ごとのGCで100〜150ms両スレッドが止まる（列車の描画停止として体感される）。

流体シミュ（`FluidSimulationStepper`）はノード・面・境界ポートを座標順に持ち、乱数・時刻に依存しない決定論的な設計で、「将来のクライアント同時計算の前提」と明記されている。外部入力は境界ポートへの流出（`port.Deliver`）と機械からパイプへの流入（`FluidPipeComponent.AddLiquid`）だけ。ベルトは既に `BeltConveyor/Sync/`（差分・全量・複製・ハッシュ）で同じ形の同期を持つ。

## Decision
- **パイプの流体状態はベルトと同形のtick差分同期へ置き換える。** 毎tickの差分は「境界ポートごとに機械へ実際に渡した量」と「ノードごとに機械がパイプへ入れた量（受け入れ済み量と流体ID）」だけ。クライアントの複製は差分を与えて `FluidSimulationStepper` を同じ順で回す。網を再構築したtickと途中参加では全量（ノード: 座標・容量・量・流体ID、面: 両端ノード番号・上限・速度・許可方向、ポート: ノード番号・上限・速度）を送る。
  出所: ユーザー裁定 2026-10-22 質問「流体の毎tick同期で、クライアントへ送る内容はどれにしますか？」→ 選択「境界の出入りだけ送り、網の中はクライアントが同じ計算を回す」（[[2026-10-22-パイプの流体状態は境界の出入りだけを差分で送りクライアントが同じ計算を回す]]）
  棄却案: 全ノードの量と流体IDを毎tick全量で送る／現行のBlockStateイベントを変化したパイプだけに間引く
- **計算は double のまま進め、サーバーとクライアントの不一致はハッシュで検出して列車・ベルトと同じ恒久停止にする。** 現在はサーバーがクライアントと同一プロセス（Mono）で動くため一致する。専用サーバー等で不一致が出たら固定小数点化を別タスクで検討する。
  出所: ユーザー裁定 2026-10-22 質問「複製が double で同じ計算を回す場合、サーバーとクライアントの浮動小数点の一致をどう扱いますか？」→ 選択「double のまま進め、不一致はハッシュで検出して止める」
  棄却案: 先に流体シミュを整数（固定小数点）へ書き換えてから同期する
- **パイプの BlockState（`FluidPipeStateDetail` の `IBlockStateDetail` 実装と毎tick通知）を廃止し、水位表示と WebUI のブロック詳細は複製から読む。** 水位は複製の版が進んだ回だけ、ベルトのアイテム表示と同じ描画更新の順番でパイプの表示へ押し込む。
  出所: ユーザー裁定 2026-10-22 質問「パイプの BlockState（FluidPipeStateDetail）は今後どう扱いますか？」→ 選択「パイプのBlockStateを廃止し、水位表示とWebUIは複製から読む」
  棄却案: 毎tick通知だけ止め、BlockStateは初回pullと手動取得用に残す
- **クライアントの全ワールド取得ポーリング（`WorldDataHandler.UpdateWorldData`、毎秒10回）を廃止する。** 初回ハンドシェイクの全量適用と、設置・撤去イベントの購読はそのまま。
  出所: ユーザー裁定 2026-10-22 質問「この1PRに何を入れますか？」→ 選択「#20＋ポーリング廃止＋パイプtick差分」。前段の原文「クライアントが全ブロック情報を毎秒10回取り直している って必要なのかね？」
- **やり残し #20（セーブ時に破壊予約が空であること）は既存テストで担保し、本番コードへ確認ロジックは入れない。** `BlockRemovalReservationTest.tick進行で予約破壊がtick末尾に確定する` が「予約→1tick→ブロック消滅」を見ており、これがセーブ地点より前に予約が使い切られる証明になる。
  出所: ユーザー裁定 2026-10-22 PR範囲の選択で #20 を含めた後、2026-10-23 原文「これは過剰では？あまりそのためだけによごしたくない」で `MasterTickUpdater` のセーブ前確認（`HasPendingRemovals` とエラーログ）を取り下げ
  棄却案: `IBlockRemovalReservationService.HasPendingRemovals` を足し `MasterTickUpdater` がセーブ前にエラーログで知らせる（一度も鳴らない前提の確認のために依存と分岐を増やす）
- **#21（破壊予約の内部化）と #19（train系の省略可能引数の削除）はこのPRに含めない。**
  出所: ユーザー裁定 2026-10-22（同上の選択）
  棄却案: #21 も束ねる／#21・#19 も束ねる
- 流体の差分は毎tick、空でも送る（複製を1tick進める合図）。送信はブロック更新・ベルト搬送の後（機械からパイプへの流入が確定した後）、tick末尾の操作適用の前。再構築tickの全量は再構築直後・境界ハッシュの前に次の連番で送る。連番はそれぞれ1つ消費する。
  出所: agent前提（ベルトの `BeltTransportTickEventPacket` と同じ連番運用。ADR 0087「再構築したtickは再構築直後・境界ハッシュ前に全量を連番付きで送る」）
- 流体ハッシュは列車の束（`TrainUnitTickDiffBundleMessagePack`）に4つ目のハッシュとして載せ、間引きtickはダミー。クライアントの検証器は列車・レール・ベルト・流体の4つを同じtickで照合する。
  出所: agent前提（ベルトハッシュの前例をそのまま拡張）
- **流体ハッシュは tick で変わる項目（ノードの量・流体ID、面の速度、ポートの速度）と各件数だけを混ぜる。** 座標・容量・面の相手と上限・向き許可・ポートの上限は再構築まで変わらず、再構築tickと途中参加の全量送信（と `FluidFullStateAssert` のテスト）で揃うので、4tickごとの照合には載せない。構造が食い違えば量が次のtickからずれるので検出は遅れない。計算は全量を写さず `FluidSimNode[]`・`FluidSimFace[]`・`IFluidBoundaryPort[]` を直接なめる。
  出所: ユーザー 2026-10-23 原文「hash計算もめちゃ軽くしたい」→ agent が「インライン化＋配列直接」「4本の鎖」「可変項目だけ」の3案と実測（22,800ノードで 3.9ms）を提示 → ユーザー原文「全部ためしてみて」。同一実行内の実測: 現行 3.9ms → インライン化 2.9 → 配列直接 2.5 → 可変項目だけ 0.7ms。配置の連続・散在で差は無く（5.3ms 対 5.4ms）、同じオブジェクトを演算なしで舐めるだけなら 0.25ms なので、重さはメモリでなく Mono 上の演算と呼び出し。
  棄却案: 全量を写してから混ぜる（割り当て 1ノード約105B、本PRの前半で廃止済み）
- **流体ハッシュは要素ごとに独立したハッシュ（種別・番号・値を乗算と右シフトで撹拌する）を作り、順序に依存しない加算で畳む。** 要素間に依存が無いので、どの順でも、何分割しても、別スレッドで分けて足しても同じ値になる。列車・ベルトの FNV-1a（前の値に次を掛ける直列の鎖）は分割すると値が変わるため並列化できず、流体はその形を踏襲しない。
  出所: ユーザー 2026-10-23 原文「そもそも誰がhashを直列でしか計算できないように組んだの？trainはそうだけどどこから計算しても結果がおなじになるようにしなければこういうところで並列化できないが？」。直前まで agent は FNV-1a の直列鎖を「4本の鎖で並走して合流」で速くしていたが（0.7→0.49ms）、鎖の本数が値を決める形で分割の自由が無かった。同一実行内の実測（22,800ノード）: 4本鎖 FNV 0.95ms、MurmurHash3 の撹拌で要素化 2.05ms、乗算と右シフトの軽い撹拌で要素化 1.3ms。順序非依存の代価は1.4倍で、2分割して足した値が全体と一致することを確認。
  棄却案: FNV-1a を4本の鎖で並走（本数固定で値が決まり、分割できない）／MurmurHash3 の撹拌をそのまま使う（軽い撹拌の1.6倍遅く、変化検出の用途に過剰）
- **列車（`TrainUnitSnapshotHashCalculator`・`RailGraphHashCalculator`）とベルト（`BeltTransportStateHash`）の FNV-1a 直列鎖も同じ形へ移す。** 部品を `ClassLibrary.ElementHash`（Begin／Add／Finish／Count）に1つ置き、4系統すべてがそれを使う。混ぜる項目は変えず、畳み方だけを要素ごとの独立ハッシュの加算にする。列車は識別子（TrainUnitInstanceId）を要素に含めるので並べ替えと List の確保が不要になり、Guid・float の取り込みも配列確保なしにした。
  出所: ユーザー 2026-10-23 原文「ちょっと気持ち悪いのでtrainとベルコンもこの直列計算をやめて並列化可能なアルゴリズム採用にしたい」
  棄却案: 流体だけ順序非依存にして列車・ベルトは据え置く（直前の agent 判断。3つの系統で畳み方が違うのは読み手に負担）
- 機械側の流体タンク状態（`FluidMachineInventoryStateDetail`）と機械の進捗状態は本PRの対象外。石窯 1,677件/秒・加工機 809件/秒の残りは同じ「tick差分」の方向で次に扱う。
  出所: agent前提（範囲を流体パイプに限る裁定の帰結）
- grill 規約の Codex 独立質問の段は、個人スキル codex-audit 不在のため省略した。
  出所: ユーザー裁定 2026-10-22 質問「Codex 段は実行できません。どうしますか？」→ 選択「省略して進む」

## Placement
- Game.Fluid/Sync: `State`（`FluidNetworkFullState`・`FluidNodeState`・`FluidFaceState`・`FluidPortState`・`FluidNetworkStateHash`）、`Diff`（`FluidTickDiff`・`FluidPortDeliveryRecord`・`FluidNodeInflowRecord`・`FluidNetworkDiffRecorder`）、`Replica`（`FluidNetworkReplica`・`FluidNetworkReplicaAssembler`・`FluidReplicaBoundaryPort`）、`Message`（全量・差分の MessagePack）。
- Game.Block/Blocks/Fluid: `FluidNetworkDatastore` が全量切り出し・ハッシュ・差分記録・再構築通知を持つ（`IFluidNetworkLookup`）。ノード・面・ポートは再構築ごとに配列へ置き換えて保持する。`FluidTickUpdater` が差分の束を流す。`FluidPipeComponent` から `IBlockStateDetail`・`IBlockStateObservable` を外す。
- Server.Event/EventReceive/FluidNetworkSync: `FluidNetworkTickEventPacket`（差分・再構築全量）・`FluidNetworkFullSnapshotEventPacket`（登録時push）。`TrainUnitTickDiffBundleEventPacket` に流体ハッシュ。
- Client.Game/InGame/FluidNetwork: `FluidNetworkClientCache`、`Network/FluidNetworkEventNetworkHandler` と2つの buffered event、`View/FluidPipeWaterLevelPresenter`。`TrainUnitHashVerifier`・`TrainUnitFutureMessageBuffer`・診断に流体ハッシュ。`FluidPipeStateChangeProcessor` は水位の表示だけを持つ `FluidPipeWaterLevelView` へ。
- Client.Game/InGame/World: `WorldDataHandler` からポーリングを削除。

## Verification
- サーバー: 差分が毎tick1件・連番に欠番なし、再構築tickは差分の後・ハッシュの前に全量。イベントだけで進めた複製がサーバーの全量・ハッシュと一致（ポンプ→パイプ→機械の流れ、パイプの追加で再構築）。
- クライアント: 受信口→順序待ちキュー→複製→検証器の一式で列車束の流体ハッシュを通す。複製の食い違いで恒久停止。水位表示が複製の値を反映。WebUI のパイプ詳細が複製から埋まる。
- 既存のベルト同期テストの連番数・登録push数を4系統へ更新。流体の既存テスト（FluidTest 等）が通る。
- 破壊予約: 既存の `BlockRemovalReservationTest`（1tick進めた後にブロックが消えている）をそのまま #20 の担保とする。
- ポーリング: `WorldDataHandler` に周期取得が無い（コンパイルと既存テスト）。
