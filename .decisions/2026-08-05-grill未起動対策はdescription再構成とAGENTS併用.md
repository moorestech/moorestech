決定: moores-grill-with-docsの起動漏れ（挙動変更依頼をバグ修正と誤分類）対策は、descriptionの全面再構成（説明先行を排しトリガー先頭言い切り・「バグ修正に見える〜にしたい」罠パターン明示・対象外1文）と、AGENTS.md設計原則へのgrill-first 1行の併用とする
棄却案: ①冒頭「ブレスト用」文維持の最小修正 ②UserPromptSubmit hookでの機械注入 ③SKILL.md本文へのルール追記（未起動では読まれず原理的に無効）
理由: 診断が注意の希釈のため処方は追記でなく再構成（update-skillの分析フレームワーク）。hookは毎ターンのコンテキスト費用が過大。AGENTS.mdは常時ロードでdescriptionの確率的手当てを決定論寄りに補強する
リンク: 出所=ユーザー裁定 AskUserQuestion 2026-08-05（update-skillセッション・「なんで最初にgrillを起動しなかったの」指摘の再発防止）
