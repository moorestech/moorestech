# ゲーム内 UI（UI Toolkit）の文書の入口

ゲーム内 UI は UI Toolkit（`moorestech_client/Assets/Scripts/Client.Ui`・`Client.Ui.State`）で作る。CEF・Web UI（`moorestech_web`）は切替で撤去した。

| 知りたいこと | 読むもの |
|---|---|
| 作り方・層・部品・USS・トークン・画面の型・入力 | [ref-uitk-design](../../.agents/skills/ref-uitk-design/SKILL.md) |
| 部品の一覧と公開 API | [component-catalog.md](../../.agents/skills/ref-uitk-design/references/component-catalog.md) |
| 見た目のホワイトリスト | [visual-vocabulary.md](../../.agents/skills/ref-uitk-design/references/visual-vocabulary.md) |
| 見た目の比べ方・例外の記帳 | [compare-procedure.md](../../.agents/skills/ref-uitk-design/references/compare-procedure.md)・[look-exceptions.md](look-exceptions.md) |
| UITK の挙動の実測の結論 | [measurements.md](measurements.md) |
| 録画のプレイテスト（DSL） | [unity-playmode-recorded-playtest](../../.agents/skills/unity-playmode-recorded-playtest/SKILL.md)・[playtest-dsl.md](../playtest-dsl.md) |
| 移行の設計と経緯 | [docs/uitk-migration/](../uitk-migration/)（設計書・実行計画）・[docs/adr/](../adr/)・`.decisions/` |
| 撤去前の Web UI の文書 | [docs/archive/webui/](../archive/webui/)・[docs/archive/webui-parity/](../archive/webui-parity/)・[docs/archive/webui-feature-videos-2026-06-14/](../archive/webui-feature-videos-2026-06-14/) |
| 撤去前の最後の参照キャプチャ | `moorestech_logs/uitk/reference-captures/`（撮影元は [reference-captures.md](reference-captures.md)） |

移行中の追従台帳と写さない Web テストの処遇は消した。基準は切替 PR #1684 のマージコミットの木にある。`git show <マージ SHA>:docs/uitk/TODO.md`／`:docs/uitk/disposition.md` で読む（マージ SHA は `git log --merges --grep='^Merge pull request #1684 ' -1 origin/master` で引ける。SHA: 254f942fb56a83f71db064f7178127140f39b8a2）。
