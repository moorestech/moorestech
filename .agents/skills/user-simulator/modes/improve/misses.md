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
