決定: `GearDetailDto` / `GearDetailDataSchema` を `role` を判別子とする判別union（zod `discriminatedUnion`、C# は `float? BaseRpm`）にする。発電機の枝には `baseRpm` を持たせない。

棄却案:
- nullable フラット（`float? BaseRpm` + zod `.optional()`）。番兵0は消えるが「発電機には無い」ことが型に出ず、読み手側に `?? 0` の吸収が残る
- 現状維持（番兵0を仕様として ADR 0056 に明記）。変更量ゼロだが機械的に止める仕組みが無い

理由: 発電機に `baseRpm: 0` を平置きしていると、翻訳CSVの1列を「RPM {current} / {base}」へ語順統一しただけで ADR 0056 が名指しで潰した「RPM 20.0 / 0.0」が風車に復活する。型検査もzodもvitestも止めない。同ファイル4行上の `PumpDetailDataSchema` が既に判別union化されており前例一致。

リンク: docs/adr/0056-gear-section-reports-current-values-without-satisfaction-comparison.md / PR #1347
