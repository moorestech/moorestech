# MapVeinAabbはOfItemとOfFluidのfactoryで生成する

- 日付: 2026-09-06 / 出所: AskUserQuestion（PR #1323 レビュー D11）
- 決定: `MapVeinAabb` のコンストラクタを private にし `OfItem(...)` / `OfFluid(...)` の static factory からのみ生成する。読み出し側は変更なし
- 棄却案: (A) 現状維持 / (C) Kind を nullable からの導出プロパティにする / (D) ItemVeinContent / FluidVeinContent のペイロード型へ分割
- 理由: Kind・VeinItemId?・VeinFluidId? の不整合な組を生成箇所1つの書き換えで構文的に作れなくする最小手
