# 前向き測定ログ

マージ済みPRごとに1行（手動運用。手順: README.md「指摘の反映手順」）。
- **人間指摘**: sakastudioのレビューコメント数（LGTM・肯定は除く）
- **分類**: F0=設計段階確定 / F1=前例不参照 / F2=既存ルール違反 / その他
- **事前検出**: うちハーネス（moores-code-review / spec-architecture-review / 決定論）が人間より先に検出していた件数
- **却下**: ハーネス指摘のうちユーザーが却下した件数（ノイズ）

| 日付 | PR | 人間指摘 | 分類内訳 | 事前検出 | 却下 | メモ |
|---|---|---|---|---|---|---|
| 2026-07-07 | #978 | 13 | F1×8, F2×4, 質問×1 | 0（ハーネス導入前） | - | ベースライン。ドメイン越境・optional・ポーリング |
| 2026-07-07 | #988 | 4 | F0×3, F1×1 | 0（ハーネス導入前） | - | ベースライン。spec段階で誤方針が確定していた |
| 2026-07-07 | #997 | 2 | F1×2 | 0（ハーネス導入前） | - | ベースライン。複合interface・ディレクトリ |
| 2026-07-07 | #987 | 1 | F1×1 | 0（ハーネス導入前） | - | ベースライン。共用体struct（メモ扱い） |
| 2026-07-09 | #996 | 3 | F1×2, F2×1 | 0（ハーネス導入前） | - | ベースライン。god-context・DTO配置 |
| 2026-07-09 | #1000 | 1 | F1×1 | 0（ハーネス導入前） | - | ベースライン。委譲漏れ重複 |
| 2026-07-22 | #1045 | 7 | 裁定翻意×2(多態化/DI分割), 設計趣向×3(呼出元最小・CreateFrom対称), 派生×2 | 1（多態化はarchitecture-lifecycleが検出→誤推奨で現状維持裁定→翻意） | 1（region-internal） | [記録](../records/2026-07-22-build-undo-ctrl-z.md)。事後にレンズ改修＋リプレイで②〜⑦全件検知化 |
| 2026-07-23 | replace-family(セッション指摘) | 1 | F1×1(同dirマスタ駆動前例からの無言乖離) | 二値契約時代のリプレイでopus/sonnet9系統+Codex素通し・fable precedent-alignmentの設計判断出口のみ検知 | - | 対策: 3段階セベリティ化＋hardcoded-content-enumeration(opus)新設。3段階検証合格(selector発火/由来diffサニティCritical/ブラインド陽陰2026-07-23 opus) |
| 2026-07-25 | #1061(P2 map-autogen) | —(pre-merge自己ゲート) | 設計判断×3(per-item失敗/初期化ゲート/helper構造)・Critical: deadcode1+データ系3(v8 addressablePath/stale guid/EditMode fixture) | 6系統一致でtruncation検出(R-archlife/bugfix/resultstate/userintent/L-precedent/Fable)・Fableがstale guid新発見 | 0 | [記録](../records/2026-07-25-map-autogen-p2.md)。23sub+Codex+post-guard2。裁定=skip+continue/gate/localize適用(db89d3c32,b76b941db)。データ系CriticalはT8実行 |
| 2026-07-26 | 電線interface化＋選定コア共通化 | 6 | Critical 6件（比較演算子/容量ガード二重化×2系統/helper局所化×2系統/rationale-guard）＋設計判断4件を裁定 | 決定論1+verifier1+2系統一致×2+rationale1。Codex Critical 0/High 0で他系統と整合 | - | [記録](../records/2026-07-26-electric-wire-param-interface-and-shared-selector.md)。test-mutationが「yaml 1行が唯一の接続経路」の無防備を単独検出→データ駆動不変条件テストで解消。裁定3件適用・1件却下 |
| 2026-07-26 | 免責ロンダリング封鎖(スキル改修) | - | ガード文言19本suppressed化・checks_context新設・ledger_gate新設に伴いsynthetic 4contextへ出所ラベル付与＋README Layer1に--context追加＋期待#34/#35追記 | 5系統レビューでCritical 3件(ADR無検証/伝送路未定義/自動適用未除外)を検出→v2で全対応 | - | 由来: docs/superpowers/specs/2026-07-26-review-exemption-laundering-fix-design.md |
| 2026-07-26 | 免責ロンダリング封鎖(最終ブランチレビュー) | 4 | ADR無検証(3系統一致)/suppressed伝送路未定義/自動適用未除外/checks_context片翼fail-open | Codex実測突破2件(LABEL_RE/TARGET_RE)・レビュー中の追加fail-open発見1件(**Modify:**) | - | [記録](../records/2026-07-26-review-exemption-laundering-fix.md)。fable枠上限で2観点opus代替 |
| 2026-07-27 | 画面外ワールドピン矢印の視認性改善 | —（pre-merge自己ゲート） | 汎用reviewer Critical 2（許容値リテラル・見た目mutation耐性）＋Codex High 1 / Medium 4を全照合 | 4隅収まり・computed stroke/filter・fresh capture manifestを追加し、1280x720の3背景を目視合格 | 0 | [記録](../records/2026-07-27-world-pin-arrow-visual.md)。最終決定論confirmed 0、post-checkは自明コメント2組だけを削除 |
| 2026-07-27 | スキットWeb UI再実装 | 8 | intent型消失(4系統一致)/背景空行バグ(2系統)/capture直叩き(3系統)/svg序数依存(3系統)/マーカー条件重複/死にガード/SKILL不整合2＋設計判断4件を全て裁定・適用 | Codex High(z層逆転)→HUDゲート裁定。ai-recurringが撮影QA全数未接続を実測検出→notification snapshot恒久修正 | - | [記録](../records/2026-07-27-skit-web-ui-redesign.md)。TS/CSSのみでレンズ1+reviewer8構成。修正適用はユーザー指示でOpus subagent実施 |
| 2026-07-27 | チュートリアル画面暗転の撤去 | —（pre-merge自己ゲート） | Critical 2件（E2E配置上限・旧暗転CSS mutation生存）＋コメント規約2件 | 決定論/Codexが配置、reviewer/Codex/Fableが回帰テスト不足を独立検出 | 1（Codex: wire DTOのenum化は今回のpublic producer API契約外） | [記録](../records/2026-07-27-tutorial-screen-dimming-removal.md)。E2Eをsystemへ移し、旧9999px selector復元でREDになるprobeを追加。最終Unity/Web検証は全GREEN |
| 2026-07-27 | クラフト進捗矢印のゲージ化 | 6 | Critical 0（決定論0）。Codex High 1(value=1が画素完全一致せず)＋設計判断3件を裁定。適用: 幾何の出所統一・3層/クリップ矩形のテスト固定・clamp再実装除去・寸法トークン化・rationale復元 | 3系統一致(ARROW_TOP/BOTTOM命名: ssot/ai-mistakes/implicit-value)＋Codex Low。test-mutationが「層の取り違えが全green」を単独検出 | 1（rationale-guard: 自分のStep6短縮で根拠消失→escalateせず機械的復元） | [記録](../records/2026-07-27-craft-arrow-progress-gauge.md)。Codex Highは実画素で再検証し「塗り内部は差0・AA境界1221px/Δ31」と確定→記述訂正で決着。**centralization reviewerが4回催促に無応答で未回収**（オーケストレータが自力代替）。全系統でSendMessage明示が必要だった |
