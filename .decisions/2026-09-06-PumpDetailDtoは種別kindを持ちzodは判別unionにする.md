# PumpDetailDtoは種別kindを持ちzodは判別unionにする

- 日付: 2026-09-06 / 出所: AskUserQuestion（PR #1323 レビュー D10）
- 決定: `PumpDetailDto.Kind`（"electric" | "gear"）を追加し、Web の `PumpDetailDataSchema` は `z.discriminatedUnion("kind", …)` で electric の有無を型で narrowing する。C#・zod・fixture・e2e を同時に変更
- 棄却案: `Apply` に param を渡し `ElectricPumpBlockParam` で型ガードする最小修正（CommonMachine 不在の代用判定は Web 側に残る）
- 理由: 「CommonMachine の有無」を種別の代用にする判定を3層から消す
