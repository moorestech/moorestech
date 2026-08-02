# 採点台帳（スコアボード）

review / preanswer の全実行を記録する。損失関数の帳簿はユーザーの頭ではなくこのファイルが持つ。
記録はメインセッションが行う（判事ではない）。追記型・行の書き換え禁止。

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
| 2026-07-27 | review | specs/2026-07-27-pr-independent-review-design.md | 確信あり4件適用（原則①②master到達済みで上書き前提失効・ハーネス正典tree絶対パス固定・report-onlyモード明記・checkout --detach+reset）＋Warning2件適用（出所ラベル正式文法・suppressed Critical格上げの将来検討1行） | 0 | ユーザー反応待ち | 判事のgit実走査（laundering-fix改修のmaster到達確認）・moores-code-review SKILL.md実文（Step6/6.5/7.3）・worktreeブランチロックの実測再現 | 要裁定なし（ハーネス取得元は目的から一択と判断）。採点はユーザーspecレビュー後に確定 |
| 2026-07-27 | review | specs/2026-07-27-pr-independent-review-design.md（採点確定） | 的中（ユーザー「ok」・追加指摘なし・適用済み指摘への否定なし） | 0 | 0 | 前行と同じ（判事git実走査・moores-code-review SKILL.md実文・worktreeロック実測） | 前行の確定行 |
| 2026-07-27 | review | plans/2026-07-27-pr-independent-review.md | 確信あり4件適用（テストfixtureのbase分岐欠落で3/4必敗・git grep POSIX EREの\s無効で全FP化・pytest未導入→uv run・vendor元をリポジトリ内パスへ）＋Warning2件適用（asmdef key行スキップ+テスト形式修正・スモークPR選定手順化） | 0 | ユーザー反応待ち | 判事の実行検証（一時repo実走・git 2.53実測・uv実在確認・テンプレバイト同一diff）・verification-coverage観点4・premise-verification観点2 | 判事が論証でなく実行ログ一次証拠で反証した好例。要裁定なし |
| 2026-07-27 | preanswer | 最終レビュー設計判断2件（PR内新設ADRの免責可否・台帳の見逃し記録粒度） | Q1予測=(a)自動降格+フラグ（確信高: 3原則①直接適用+台帳承認方式の同型2件）→前提宣言に降格し実装適用。Q2予測=(c)不一致PRのみrecords内訳（確信中: decisions.md #4で(b)棄却+粗いスコアボード裁定#3類似）→予測注記付きで質問 | - | - | 3原則①（spec ADR 2026-07-26承認）・台帳承認方式裁定・decisions.md #4・「認知コストを最低に」発言2026-07-27 | 採点は回答後に確定 |
| 2026-07-27 | preanswer | 最終レビュー設計判断2件（採点確定） | Q1: 前提宣言が受理（異議なし）。Q2: 的中（(c)不一致PRのみ詳細を選択） | 0 | 0 | 前行と同じ | 前行の確定行。Q2の「内訳はセッションが記入・人間は確認のみ」は判事補足の条件付き採択予測どおり選択肢文面に織込み済みで異議出ず |
| 2026-07-26 | review | specs/2026-07-26-review-exemption-laundering-fix-design.md | 適用推奨1件適用（all-code-review側reviewers25本の消音経路残存→変更2対象へ明記・ADR限定し直し）＋Warning3件適用（pathsマッチ=機械的下限の優先順位・plan frontmatterにspecパス必須・ledger-gateカバー範囲明記） | 0 | ユーザー反応待ち | ユーザーゴール発言引用（両機構名指し）・sim-gate.sh実装・all-code-review自前detchecks実在・select_lenses.py機構 | 判事agentId: a0db8dc8a7a8ea3e8。予測C（変更4不十分説）は反証役が破棄しFP防止で除外。要裁定0件 |
| 2026-07-26 | review | plans/2026-07-26-review-exemption-laundering-fix.md | 適用推奨2件適用（fixture期待値off-by-one `[9]`→`[8]`・checks_contextの見出し前提沈黙故障→fail-closed＋##見出し明記）＋Warning5件適用（reviewer数11→13訂正・相対spec解決をplan位置由来に・YAML例にPreToolUse維持明記・hardcoded-content-enumeration判定注意・Files節相対パス必須） | 0 | ユーザー反応待ち（「最後まで進めて」指示により実装続行） | 機械的検証（ロジック実走）・verification-coverage「無言フォールバック」実例・skill-frontmatter-hooks-quirksメモリ・実grep照合 | 判事agentId: a7b67de4d9433c708。要裁定0件 |
| 2026-08-02 | review | plans/2026-08-02-pr1104-review-ruling-fixes.md（レビュー実施記録） | 判事起動済み・レポート待ち。設計判断4件は全てユーザー裁定（PR#1104独立レビューダイジェストへの実コメント）由来でspec ADR#11〜#14へ掲載済み | - | 反応待ち | spec ADR#11〜#14（ユーザー裁定 2026-08-02） | 本セッションのplan review実行記録（sim-gate通過用の明示行）。採点は判事レポート＋ユーザー反応後に確定 |
| 2026-08-02 | preanswer | D1初期化待機機構の裁定＋plan実行方法 | 判事起動済み・予測待ち | - | - | - | 採点は予測受領＋ユーザー回答後に確定 |
| 2026-08-02 | preanswer | D1初期化待機機構＋plan実行方法（予測受領・提示形式確定） | Q1予測=A+B併用（確信高: 同セッション裁定「たたむ」「集約」＋AGENTS.md「変更の波及を恐れない」直接適用）→前提宣言（拒否権つき）へ降格しplan Task 5として適用済み。Q2予測=Subagent-Driven（確信中: writing-plans推奨追従＋SDD常用実績・逆例1件）→予測注記付きでAskUserQuestion提示 | - | - | adjudications.md同セッション裁定2件・AGENTS.md変更波及原則・deviation-cases§5§6・SDD運用メモリ | 採点はユーザー回答/拒否権行使後に確定行を追記 |
| 2026-08-02 | preanswer | D1初期化待機機構＋plan実行方法（採点確定） | Q1: 前提宣言（A+B併用）が受理（拒否権行使なし）。Q2: 方向的中だが一手ずれ — AskUserQuestion回答は「まだ実行しない」、直後にユーザーが「opus subagent drivenで実行」と明示指示。予測Subagent-Drivenは最終的に的中、選択肢提示時点の回答とは不一致 | 0 | 0 | adjudications.md同セッション裁定2件・AGENTS.md変更波及原則・deviation-cases§5§6・SDD運用メモリ | 学び: 「まだ実行しない」は否定でなく提示形式への保留（planを読んでから決める）だった。実行方法質問はplan提示と同一ターンに置くと保留を招く可能性 |
