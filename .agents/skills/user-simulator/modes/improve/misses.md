# 採点台帳（スコアボード）

review / preanswer / shadow の全実行を記録する。損失関数の帳簿はユーザーの頭ではなくこのファイルが持つ。
記録はメインセッションが行う（判事ではない）。追記型・行の書き換え禁止（事後確定・反転・永続化リンクの追補のみ可）。

**このファイルはインデックス兼スコアボード**。1行要約は考古学に耐えないため、実体（盲検タスク・gold・予測・
採点・匿名化transcript・HEAD）は `../../datasets/<日付>-<slug>/` に機械学習的に再チェック可能な形で封入する
（shadowモードprotocol手順5参照）。ここに無いリンクの行（2026-07-27以前）は素材が散逸済みで要約しか残っていない。

- **寄与知識**: 的中した予測が根拠にした知識/裁定の実名（判事レポートの根拠欄から転記）。
  improveセッションが「改善の事後有効性」（死蔵知識の検出・剪定）を判定する材料になる。

| 日付 | モード | 対象 | 的中 | FP | FN | 寄与知識 | 備考（外しはハンドオフID） |
|---|---|---|---|---|---|---|---|
| 2026-07-24 | review | specs/2026-07-24-electric-wire-param-interface-and-shared-collector-design.md | Warning2件適用（電柱同名キー並存明記・テスト配置先） | 0 | ユーザー反応待ち | moorestech-principles.md（先行パターン・YAGNI・SSOT）、裁定「前例はファイル配置の粒度まで調べる」、IMachineParam前例 | Critical/要裁定なし・opus反証未起動 |
| 2026-07-25 | review | specs/2026-07-24-electric-wire-param-interface-and-shared-collector-design.md（採点確定） | 的中（ユーザー「ok」・追加指摘なし） | 0 | 0 | 前行と同じ（moorestech-principles.md・配置粒度裁定・IMachineParam前例） | 前行「ユーザー反応待ち」の確定行 |
| 2026-07-25 | review | plans/2026-07-25-electric-wire-param-interface-and-shared-selector.md | C1適用（interfaceプロパティ注入前提が偽・生成器実コードで反証確定→plan/spec両修正）＋Warning適用（挙動差なしへ記述修正） | 0 | ユーザー反応待ち | premise-verification lens・mooresmaster/DefinitionGenerator.cs実コード・IMachineParam実装ケースの自前宣言 | メイン筆者のspec前提誤りを判事反証が捕捉（simulator価値実証例）。要裁定1件はpreanswer予測(a)本命でAskUserQuestionへ |
| 2026-07-25 | preanswer | yaml重複の扱い（C#共通化のみ/生成器拡張別タスク/生成器拡張込み） | 予測=(a)C#共通化のみ・(b)は先送り（確信中: 過去裁定「最小構成へ畳む・将来拡張は先送り」傾向。ユーザーが生成器作者のため(b)昇格の余地あり）→予測注記付きで質問 | - | - | reviewモード判事の裁定予測欄を流用（別判事は未起動） | 初回質問はユーザーから「どういうこと？」の明確化要求→背景説明後に再質問。採点は回答後に確定 |
| 2026-07-25 | preanswer | yaml重複の扱い（採点確定） | 方向的中（(a)選択）。ニュアンス外れ: 予測は重複を「容認するコスト・(b)先送り」と枠付けたが、ユーザー裁定は「各ブロック宣言があるべき姿・重複は問題ではない」（生成器拡張の需要自体が不存在） | 0 | 0 | 過去裁定「最小構成へ畳む」傾向 | 学び: スキーマの明示的宣言はコピペではなく意図された形とみなす価値観。C#側の分岐重複だけが解消対象 |
| 2026-07-25 | review | plans/2026-07-25-electric-wire-param-interface-and-shared-selector.md（レビュー実施記録） | C1（注入前提の反証）・Warning（挙動差なし）適用済み、要裁定はユーザー裁定済み（yaml重複=あるべき姿）。plan本文のユーザーレビュー反応は実行方法選択待ち | 0 | 反応待ち | premise-verification lens・DefinitionGenerator.cs実コード | 本セッションのplan review実行記録（sim-gate通過用の明示行） |
| 2026-07-26 | preanswer | agent前提の提示方式（台帳承認/都度質問/事後可視化のみ） | 予測=台帳承認方式（確信中: 同型裁定は#4の1件のみ・逆向き前例#3あり）→予測注記付きで質問。判事警告: 選択肢1と3は排他でなく「両方やる」の枠組み指摘の可能性あり→質問文で3原則②は共通前提と明記して緩和 | - | - | decisions.md #4（読む量・答える量の純増却下）・ユーザー自己診断「全文章を読んでいない」 | レビュー免責ロンダリング事故の再発防止設計。採点は回答後に確定 |
| 2026-07-26 | preanswer | agent前提の提示方式（採点確定） | 的中（台帳承認方式を選択・枠組みへの異議なし） | 0 | 0 | decisions.md #4（読む量・答える量の純増却下）・ユーザー自己診断「全文章を読んでいない」 | 前行の確定行。判事警告「1と3両方では」は質問文で原則②を共通前提と明記したため発生せず |
| 2026-07-26 | review | specs/2026-07-26-review-exemption-laundering-fix-design.md | 適用推奨1件適用（all-code-review側reviewers25本の消音経路残存→変更2対象へ明記・ADR限定し直し）＋Warning3件適用（pathsマッチ=機械的下限の優先順位・plan frontmatterにspecパス必須・ledger-gateカバー範囲明記） | 0 | ユーザー反応待ち | ユーザーゴール発言引用（両機構名指し）・sim-gate.sh実装・all-code-review自前detchecks実在・select_lenses.py機構 | 判事agentId: a0db8dc8a7a8ea3e8。予測C（変更4不十分説）は反証役が破棄しFP防止で除外。要裁定0件 |
| 2026-07-26 | review | plans/2026-07-26-review-exemption-laundering-fix.md | 適用推奨2件適用（fixture期待値off-by-one `[9]`→`[8]`・checks_contextの見出し前提沈黙故障→fail-closed＋##見出し明記）＋Warning5件適用（reviewer数11→13訂正・相対spec解決をplan位置由来に・YAML例にPreToolUse維持明記・hardcoded-content-enumeration判定注意・Files節相対パス必須） | 0 | ユーザー反応待ち（「最後まで進めて」指示により実装続行） | 機械的検証（ロジック実走）・verification-coverage「無言フォールバック」実例・skill-frontmatter-hooks-quirksメモリ・実grep照合 | 判事agentId: a7b67de4d9433c708。要裁定0件 |
| 2026-07-27 | preanswer | 装備スロット構成とツール選択方式（1スロット/複数+自動マッチ/複数+手動切替） | 質問時点は的中扱い（AskUserQuestionで予測通り「複数+自動マッチ」選択）だが、並行grillセッションの最終ADR-0003は手動ホイール循環＋素手で確定し**最終外れ**。同一ユーザーが枠組み依存で逆回答（トレードオフ提示→自動、対話深掘り→手動）。B型前提「装備制限なし」もitems.yml tools列挙制（マスタ一級化）で棄却された | 1 | 0 | adjudications 2026-07-24露頭裁定（外れ側の根拠） | 判事agentId: af11c9024f89c40cd。学び: 「概念のマスタ一級市民化」はYAGNIでB宣言せずC残留。以後user-simはwriting-plans段階に限定運用（ユーザー裁定 2026-07-28） |
| 2026-07-28 | shadow | grillセッション（装備スロット設計・transcript b8102540）の28問を盲検再予測（opus・逐次文脈・実回答遮断・初回シャドー採点） | exact 18/26=69%。**「常に推奨案を選ぶ」ベースライン19/26=73%を下回る**。ユーザーが推奨から逸脱した7問（割当操作=両方・装備複数+循環・スロット数はマスタ・無効割当は削除・HUD右端・枠破り(ID統一の逆提案)・plan全部今書く）の的中0。確信校正は有効: 高83%(15/18)・中37%(3/8) | 8 | 0 | adjudications・moorestech-principles（推奨追従の再現のみに寄与） | 学び: 現知識ベースは「推奨に従う」の再現しかできず、価値源泉であるべき逸脱予測がゼロ。逸脱のうち装備構成系はpreanswer外れ(07-27)と同域。Phase2昇格（確信高の自動回答）は高でも装備構成・枠破りを外すため見送り、逸脱パターンの知識蓄積が先。全素材は `../../datasets/2026-07-27-hotbar-equipment-grill/` に永続化（凍結tasks+gold・run別pred/score・匿名化transcript・HEAD） |
