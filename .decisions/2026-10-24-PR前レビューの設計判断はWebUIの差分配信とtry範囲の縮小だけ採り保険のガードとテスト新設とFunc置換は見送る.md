# PR 前レビューの設計判断は WebUI の差分配信と try 範囲の縮小だけ採り、保険のガード・テスト新設・Func 置換は見送る

決定: ブランチ fix/train-client-tick-slack-pacing の moores-code-review（2026-10-23〜24）で保留になった設計判断 7 件の裁定。
- D1 WebUI のパイプ詳細: 開いた時点でクライアント側のコピーに載っていないパイプは、閉じて開き直すまで中身を出さない（調べ直さない）。開いている間の配信は、4 フレームごとに量・流体の種類を前回の配信と比べ、変わった回だけ出し直す（ベルトの搬出統計が区切りごとに出し直すのと同じ粒度）。
- D2 tick の受付区間外からの流入: 本番コードにはガードを入れない。壊れていた録画シナリオ 2 本（quality-ui-switching・fluid-inventory-via-interact）だけを、未構築パイプの持ち越し経由で水を入れる形に直す。
- D3 余裕制御（クライアント）と絶対期限ループ（サーバー）のテスト: 足さない。
- D4 流体の受信ハンドラの try 範囲: MessagePack の Deserialize だけを包む。コピーの組み立て（ApplyFullState）の例外はそのまま上げる。差分・作り直し後の全量のデシリアライズ失敗は列車ごと同期を止めて理由を残す。
- D5 RailGraphHashCalculator の Func<> 引数: 残す（AGENTS.md の Func 禁止は古い）。
- D6 ベルトと流体の受信ハンドラの写し、D7 列車の束への名前付きハッシュの足し方: 変更なし。症状を 1 文で書けないので Warning に落とす。
棄却案: D1 複製の更新通知（IObservable）の購読／マスタ判定との併用／直さない。D2 受付区間ガード（ベルトの _isAfterTransportTick と同形）／区間外流入のキュー再生。D3 TrainTickSlackController と ServerTickDeadline の切り出し＋EditMode テスト／録画プレイテストでの計測。D4 ApplyFullState が bool と理由を返す形（ベルト・列車も同時）。D5 解決者 interface／属性配列の事前構築。D6 BufferedEvent の汎用化／Handler までの汎用化。D7 ハッシュ源の登録制（配列化）。
理由: D1 は「載っていないなら何も出さない」で足り、無条件の 4 フレーム配信だけが無駄だった。D2 は本番に違反経路が無く、#20 と同じく保険のガードを本番へ入れない方針。D3・D5 はユーザーが本 PR の範囲外と判断。D4 は外部境界だけを隔離し、ロジックのバグを受信データの破損と区別するため。D6・D7 はゲーム上の症状が無い構造の話で、裁定の対象にしない。
出所: ユーザー裁定 2026-10-24（AskUserQuestion）。原文: D1「結果は『載っていない』のままなにもしない。で採用」「ベルコン segment がスループットを表示する経路あると思うけどそれ参考にしたら？」、D2「シナリオ 2 本を直す」、D3「テストは足さない」、D4「案A」、D5「agent.mdが古いので無視 funcを残す」、D6・D7「意味不明」。
追記 2026-10-24（最終バグ確認後）: D4 の「Deserialize だけを try で包む」は、組み立て（ApplyFullState）で例外が出たときに、購読を握る `EventResponseDispatcher.InvokeIsolated` が例外を飲み込んで起動待ちが永遠に終わらない副作用があった。裁定を「Deserialize の try と組み立ての try を分け、文言と停止理由（FluidNetworkInitialSnapshotFailed / FluidNetworkInitialApplyFailed）で原因を区別する」に改める。棄却案: 元の 1 つの try に戻す（原因を区別できない）／今のまま（起動が止まるのを許容）。出所: ユーザー裁定「try を 2 つに分ける」。
リンク: ブランチ fix/train-client-tick-slack-pacing（コミット 5cbc8d547・f666eeeb0 と録画シナリオ修正）、`.decisions/2026-10-22-パイプの流体状態は境界の出入りだけを差分で送りクライアントが同じ計算を回す.md`、memory `invariant-guards-belong-in-tests`
