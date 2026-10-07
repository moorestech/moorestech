決定: create-infographic-light の moorestech 同梱コピーを削除し、`~/.agents/skills/` のグローバル版だけにする。codex-audit と postmortem の repo コピーは残し、グローバル版の現行内容（監査=astra・read-only・`====` 出力契約）へ全体同期する。
棄却案: create-infographic-light の2実体の緩い同期を続ける案（同期漏れで差分が再発していた。Mac mini の全 worktree からグローバル版が見えるため同梱の利点が小さい）。codex-audit の repo 側を open-questions だけの最小スキルにする案は次回検討。
理由: 2026-09-28〜29 の ~/.agents/skills 全体剪定で、repo 側 codex-audit が旧モデル gpt-5.6-sol のまま「監査=astra」裁定に反して動き、postmortem の出力契約も両版で食い違っていた。ユーザー裁定（2026-09-29）。
リンク: [[2026-08-08-create-infographic-lightは2実体を相互参照で緩く同期する]]
