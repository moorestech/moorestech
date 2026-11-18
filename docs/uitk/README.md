# ゲーム内 UI（UI Toolkit）の文書の入口

ゲーム内 UI は UI Toolkit（`moorestech_client/Assets/Scripts/Client.Ui`・`Client.Ui.State`）で作る。CEF・Web UI（`moorestech_web`）は切替で撤去した。

| 知りたいこと | 読むもの |
|---|---|
| 作り方・層・部品・USS・トークン・画面の型・入力 | [ref-uitk-design](../../.agents/skills/ref-uitk-design/SKILL.md) |
| 部品の一覧と公開 API | [component-catalog.md](../../.agents/skills/ref-uitk-design/references/component-catalog.md) |
| 見た目のホワイトリスト | [visual-vocabulary.md](../../.agents/skills/ref-uitk-design/references/visual-vocabulary.md) |
| 見た目の比べ方・例外の記帳 | [compare-procedure.md](../../.agents/skills/ref-uitk-design/references/compare-procedure.md)・[TODO.md](TODO.md) の「## 見た目の例外」 |
| 録画のプレイテスト（DSL） | [unity-playmode-recorded-playtest](../../.agents/skills/unity-playmode-recorded-playtest/SKILL.md)・[playtest-dsl.md](../playtest-dsl.md) |
| 移行の設計と経緯 | [docs/uitk-migration/](../uitk-migration/)（設計書・実行計画）・[docs/adr/](../adr/)・`.decisions/` |
| 撤去前の Web UI の文書 | [docs/archive/webui/](../archive/webui/)・[docs/archive/webui-parity/](../archive/webui-parity/)・[docs/archive/webui-feature-videos-2026-06-14/](../archive/webui-feature-videos-2026-06-14/) |
| 撤去前の最後の参照キャプチャ | `moorestech_logs/uitk/reference-captures/`（撮影元は [TODO.md](TODO.md) の「## 参照キャプチャ」） |

[TODO.md](TODO.md)（追従台帳）と [disposition.md](disposition.md)（写さない Web テストの処遇）は切替 PR のマージの後に別の PR で消える。
