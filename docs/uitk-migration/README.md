# UI Toolkit 全面移行の計画

裁定 `.decisions/2026-10-18-CEFを撤去しUI-Toolkitへ全面移行する.md` を受けた移行計画と、その根拠一式。計画は裁定ではなく、§0 の裁定 4 件を聞いてから段階 0 に入る。

| パス | 中身 |
|---|---|
| `plan.md` | 統合計画（レビュー反映版）。読むのはここから |
| `plan-notes.md` | 草案の採点、調査・草案・レビューでの事実の訂正 |
| `poc/hard-cases/` | 第1弾 PoC 報告（Web で易しく UI Toolkit で難しい UI） |
| `poc/editor/` | 第2弾 PoC 報告（ノードエディタ・ベクターエディタ） |
| `research/` | 計画の前に取った調査 5 本（画面・ホスト・入力・テスト・配布） |
| `drafts/` | 3 方針の草案と、それを統合した初版 `plan-v1.md` |
| `review/` | `plan-v1.md` へのレビュー（Fable 4 観点・Codex 2 観点）と、採用した指摘・v1 からの対応表 |

PoC のコードはブランチ `poc/uitk-hard-cases`（master には入れない）。
