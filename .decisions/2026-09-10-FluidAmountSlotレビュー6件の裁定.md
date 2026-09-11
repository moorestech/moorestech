# FluidAmountSlot 最終ブランチレビュー 設計判断6件の裁定（2026-09-10）

対象: PR feature/machine-recipe-fluid-amount-slot / run `../moorestech_logs/harness/moores-code-review/runs/2026-09-10-1459/`

| # | 症状 | 決定 | 棄却した生きた案 |
|---|---|---|---|
| D1 | 本ブランチが master 既収の `b7824d92b v4 超軽量設定` を打ち消す revert を抱え、マージ時にUnity側5ファイルが巻き戻る | **ブランチ内で `git revert 63abedff1` を積んで相殺** | 案A rebaseでdrop（履歴が消え、巻き込み元の追跡が効かなくなる）／案C 意図的として残す（未レビューのUnity変更がmasterへ入る） |
| D2 | 液体量4桁(1,000)の量バッジが32px枠から20.9px左へはみ出し隣接列に重なる | **`.amount` は composes を保ったまま寸法系（left/text-align/font-size/letter-spacing）だけ上書き** | 案B 1k丸め（必要量の正確な数値が読めない・丸め規則が新裁定になる）／案C 枠を広げる（ADR 0054の「アイテムと同じ枠」を崩す） |
| D3 | アイコン取得失敗時に白い空枠となり、成功と区別できず無音で縮退する | **`FluidIcon` に `fallback` を必須prop化（デフォルト引数なし）・既存3箇所は `none` 明示・`SlotFrame.filled` を実描画可否から導出** | 案B 液体色のベタ面（ADR 0054「容量の無いスロットはフィルを持たない」に但し書きが要る）／案C 現状維持（AGENTS.md「無音の縮退は禁止」と衝突） |
| D4 | `amount===0` が無言でバッジ非表示になり「量なし」と区別できない | **`amount: number` + `showAmount: boolean` の明示prop化（判断は具体側が持つ）** | 案B 判別union（呼び出し2系統には過剰）／案C 現状維持（ItemSlot.count? の多義性を複製） |
| D5 | 同一行でアイテム`1000`と液体`1,000`の表記が割れる | **`shared/ui/slotAmountFormat.ts` に `formatSlotAmount` を集約し ItemSlot・液体の双方が呼ぶ** | 案B 液体を生値へ（FluidSlotのタンク表示と割れる）／案C 割れを規則として固定（表記の不統一が残る） |
| D6 | `ItemSlot` の私有CSSを composes で借用しており、ItemSlot側の調整が液体バッジへ無言で連動する | **`shared/ui/slotContent.module.css` を新設し `.icon`/`.count` を移して ItemSlot/BlockSlot/FluidAmountSlot の3者が composes** | 案B 現状維持（私有CSSが事実上の共有APIになり、ItemSlot側テストで検出できない連動事故を抱え続ける） |

リンク: `docs/adr/0054-recipe-row-fluid-slot-matches-item-slot.md` /
`docs/superpowers/plans/2026-09-10-machine-recipe-fluid-amount-slot.md` /
`.decisions/2026-09-10-e2eの液体レシピはcccccccc作り替えで足す.md` /
統合レビュー: `../moorestech_logs/harness/moores-code-review/runs/2026-09-10-1459/integrated.md`
