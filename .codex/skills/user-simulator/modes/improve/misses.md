# 採点台帳（スコアボード）

review / preanswer / shadow の全実行を記録する。損失関数の帳簿はユーザーの頭ではなくこのファイルが持つ。
記録はメインセッションが行う（判事ではない）。追記型・行の書き換え禁止（事後確定・反転・永続化リンクの追補のみ可）。

**このファイルはインデックス兼スコアボード**。1行要約は考古学に耐えないため、実体（盲検タスク・gold・予測・
採点・匿名化transcript・HEAD）は `../../datasets/<日付>-<slug>/` に機械学習的に再チェック可能な形で封入する
（shadowモードprotocol手順5参照）。データセット化以前（2026-07-24〜07-26）の行は学び抽出のうえ
`misses-archive-2026-07.md` へ退避済み（素材散逸・要約のみ。抽出先は同ファイル冒頭に明記）。

- **寄与知識**: 的中した予測が根拠にした知識/裁定の実名（判事レポートの根拠欄から転記）。
  improveセッションが「改善の事後有効性」（死蔵知識の検出・剪定）を判定する材料になる。

| 日付 | モード | 対象 | 的中 | FP | FN | 寄与知識 | 備考（外しはハンドオフID） |
|---|---|---|---|---|---|---|---|
| 2026-07-27 | review | specs/2026-07-27-mining-progress-hud-design.md | 適用推奨1件適用（削除対象 `ui.mining_hud` だけを駆動するmock採掘シナリオを統合先 `ui.progress` へ移す要件を追加） | 0 | 0 | 前提裏取り・検証カバレッジ、`topicControls.ts`・`topicFixtures.ts`・`fixtures.ts` 実コード | ユーザー「ok、playwrgihtで問題ないと言えるまで」・台帳提示後も追加指摘なし。Fable指定不可のためgpt-5.6-terra判事へ縮退、scout/refuterで反証済み |
| 2026-07-27 | review | plans/2026-07-27-mining-progress-hud.md | 適用推奨1件適用（採掘mockの `label: null` をwireキー省略へ修正し、既存 `label?: string` 契約を維持） | 0 | 0 | `ProgressDataSchema`・`WebUiJson.NullValueHandling.Ignore`・`progress_no_label.json` 実コード | 継続目標により追加裁定なしで実装続行。Fable指定不可のためgpt-5.6-terra判事へ縮退、scout/refuterで反証済み |
| 2026-07-27 | preanswer | 装備スロット構成とツール選択方式（1スロット/複数+自動マッチ/複数+手動切替） | 質問時点は的中扱い（AskUserQuestionで予測通り「複数+自動マッチ」選択）だが、並行grillセッションの最終ADR-0003は手動ホイール循環＋素手で確定し**最終外れ**。同一ユーザーが枠組み依存で逆回答（トレードオフ提示→自動、対話深掘り→手動）。B型前提「装備制限なし」もitems.yml tools列挙制（マスタ一級化）で棄却された | 1 | 0 | adjudications 2026-07-24露頭裁定（外れ側の根拠） | 判事agentId: af11c9024f89c40cd。学び: 「概念のマスタ一級市民化」はYAGNIでB宣言せずC残留。以後user-simはwriting-plans段階に限定運用（ユーザー裁定 2026-07-28） |
| 2026-07-28 | shadow | grillセッション（装備スロット設計・transcript b8102540）の28問を盲検再予測（opus・逐次文脈・実回答遮断・初回シャドー採点） | exact 18/26=69%。**「常に推奨案を選ぶ」ベースライン19/26=73%を下回る**。ユーザーが推奨から逸脱した7問（割当操作=両方・装備複数+循環・スロット数はマスタ・無効割当は削除・HUD右端・枠破り(ID統一の逆提案)・plan全部今書く）の的中0。確信校正は有効: 高83%(15/18)・中37%(3/8) | 8 | 0 | adjudications・moorestech-principles（推奨追従の再現のみに寄与） | 学び: 現知識ベースは「推奨に従う」の再現しかできず、価値源泉であるべき逸脱予測がゼロ。逸脱のうち装備構成系はpreanswer外れ(07-27)と同域。Phase2昇格（確信高の自動回答）は高でも装備構成・枠破りを外すため見送り、逸脱パターンの知識蓄積が先。全素材は `../../datasets/2026-07-27-hotbar-equipment-grill/` に永続化（凍結tasks+gold・run別pred/score・匿名化transcript・HEAD） |
| 2026-07-28 | shadow | 同28問ゴールデン再演 r2（decision #9 逸脱知識化後・opus・in-sample） | exact 23/26=88%（自由回答の目視照合込み25/26=96%）。**ベースライン73%を超過**。逸脱7問中6的中（機械4: 割当両方・複数+循環・無効削除・plan全部今書く／目視2: マスタ定数・枠破りID統一逆提案は趣旨一致）。r1の過剰逸脱FP(Q22向き)消滅・既存18問全維持。確信高14/15=93%・中11/12=92% | 1 | 0 | deviation-cases.md・adjudications「推奨追従と逸脱の境界」 | 再演ゲート合格（基準: 逸脱3+・既存維持・FP消滅）。残る外れはHUD右端（好み系・確信中降格済み）のみ。in-sampleにつき汎化は次の新規セッションshadowで測る（decision #9） |
| 2026-07-28 | review | plans/2026-07-28-placement-target-id-unification.md ＋ equipment-slot-and-server-authoritative-mining.md ＋ hotbar-build-shortcut.md（plan A/B/C同時レビュー） | 適用推奨2件適用（数字キー二重経路のUnity一本化・plan B Task6のVanillaApiSendOnly同梱によるコンパイル単位修正）＋Warning3件適用（クライアント側カタログDI登録・クールダウン×0.9ジッタ余裕・ホットバーAPI削除の波及テスト列挙とフィルタ拡張）。要裁定0件 | 0 | 0 | ユーザー反応待ち | 反証破棄2件（slot範囲未検証はcatch既存常態・usePlaceItems波及疑いはGrep実測0件）はFP防止として機能。裁定済みADRの蒸し返しなし。判事はサブエージェント（Fable）で実行 |
| 2026-07-30 | review | specs/2026-07-30-craft-tab-corner-parity-design.md | Warning 1件を適用（共有craft variantを使うPlacementModeHudとResearchDetailPaneの非破壊QAを追加）。Critical 0件、ユーザー「ok」で承認 | 0 | 0 | premise-verification・scope-resolution、GamePanel・PlacementModeHud・ResearchDetailPaneの実コード | Fable指定不可のためgpt-5.6-terra判事へ縮退し、scoutの裏取り結果も反映 |
