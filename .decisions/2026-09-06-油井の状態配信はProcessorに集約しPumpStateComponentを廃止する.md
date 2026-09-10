# 油井の状態配信はProcessorに集約しPumpStateComponentを廃止する

- 日付: 2026-09-06 / 出所: AskUserQuestion（PR #1323 レビュー D1・6系統一致）
- 決定: `ElectricPumpProcessorComponent` 自身が `IBlockStateObservable, IBlockStateDetail` を実装し、供給電力・要求電力・稼働ラベル・発火判定を同一時点の値から導く。`PumpStateComponent` と `IPumpGenerationState` は廃止し、`ElectricPumpComponent` は薄いアダプタへ戻す（miner/machine/cleanroom の3前例と同形）
- 棄却案: (A) 電力ポスト処理で分子分母を同時ラッチ（0落としフラグが別途要り、ラベルのずれは残る） / (B) 生成可否をラッチ済みフィールド化して現構成を維持（生成判定が1tick遅れる）
- 理由: 満杯tickの「待機中＋満額要求」固着・飽和時の「稼働中なのに40%」・発電機撤去後の100%固着の3症状が全て別時点評価に由来し、前例に揃えれば構造的に消える
