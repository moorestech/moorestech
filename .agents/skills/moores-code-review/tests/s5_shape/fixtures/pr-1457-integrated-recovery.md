## 系統別回収状況

起動計画42系統すべて `agents/<name>.md` が実在・非空。欠員なし。

| 系統 | 判定 |
|---|---|
| rev-core-any-callsite-tracer | 回収（Critical なし／Warning 5・設計判断あり→判断4へ） |
| rev-core-any-efficiency | 回収（Critical なし／Warning 2→C3 へ併合） |
| rev-core-any-implicit-value-meaning | 回収（Critical 1→C3／Warning 4） |
| rev-core-any-removed-invariant | 回収（Critical なし／Warning 4・設計判断あり→判断6へ） |
| rev-core-any-test-mutation-effectiveness | 回収（Critical なし／Warning 3） |
| rev-core-any-user-intent-fulfillment | 回収（§5 裁定引用の含意チェック節あり・決定10件判定済み／Critical なし） |
| rev-core-cs-architecture-lifecycle | 回収（Critical なし／Warning 6・設計判断あり→判断1・5へ） |
| rev-core-cs-async-cancellation | 回収（Critical なし／Warning 3） |
| rev-core-cs-bug-fix-intent | 回収（Critical なし／Warning 3・設計判断あり→遮蔽 Warning へ） |
| rev-core-cs-caller-orchestration-minimization | 回収（Critical なし／Warning 2・設計判断あり→判断1へ） |
| rev-core-cs-centralization-duplication | 回収（Critical なし／Warning 3・suppressed 1件は契約違反として通常節へ復帰・設計判断あり→判断6へ） |
| rev-core-cs-dead-code-and-scope | 回収（Critical なし／Warning 2→うち1件破棄） |
| rev-core-cs-region-internal | 回収（Critical 2→C8） |
| rev-core-cs-result-state-propagation | 回収（Critical なし／Warning 2→C1・C2 へ・設計判断あり→判断1へ） |
| rev-core-cs-schema-design | 回収（Critical 1→C1・C3／Warning 4・設計判断あり→判断1・2・5へ） |
| rev-core-cs-unidirectional-flow | 回収（Critical なし／Warning 1） |
| rev-core-cs-unity-convention | 回収（該当なし） |
| rev-core-ts_tsx-ai-recurring-mistakes | 回収（Critical なし／Warning 7→C6 等へ・設計判断あり→判断4へ） |
| rev-core-ts_tsx-centralization-duplication | 回収（Critical 1→C4／Warning 3） |
| rev-core-ts_tsx-default-resolution-ownership | 回収（Critical なし／Warning 1→C7 へ） |
| rev-core-ts_tsx-hardcoded-content-enumeration | 回収（Critical なし／Warning 2→C4・C6 へ） |
| rev-core-ts_tsx-implicit-cardinality-assumption | 回収（該当なし） |
| rev-core-ts_tsx-result-state-propagation | 回収（Critical なし／Warning 2・設計判断あり→判断4へ） |
| rev-core-ts_tsx-single-source-of-truth | 回収（Critical なし／Warning 2→C4 へ） |
| rev-core-ts_tsx-speculative-abstraction | 回収（Critical なし／Warning 3→1件破棄・設計判断あり→判断4へ） |
| rev-core-ts_tsx-type-driven-structure | 回収（Critical なし／Warning 3・設計判断あり→判断4へ） |
| rev-moores-any-precedent-alignment | 回収（Critical なし／Warning 3・設計判断あり→判断1・4へ） |
| rev-moores-any-server-state-sync | 回収（対象外・該当なし） |
| rev-moores-cs-datastore-access-separation | 回収（Critical なし／Warning 1→判断5 へ） |
| rev-moores-cs-default-resolution-ownership | 回収（Critical なし／Warning 1） |
| rev-moores-cs-domain-boundary | 回収（Critical 1→C3／Warning 3・設計判断あり→判断2・5へ） |
| rev-moores-cs-redundant-member-duplication | 回収（Critical なし／Warning 1→破棄） |
| rev-moores-cs-set-once-dependency-injection | 回収（該当なし） |
| rev-moores-cs-speculative-abstraction | 回収（Critical なし／Warning 1→判断5 へ） |
| rev-moores-cs-type-driven-structure | 回収（Critical なし／Warning 3・設計判断あり→判断2・5へ） |
| fable-holistic-review | 回収（Critical 1→C5／Warning 3・設計判断あり→判断3へ） |
| investigator-chunk-1-context-consistency | 回収（Critical なし／Warning 2） |
| investigator-chunk-1-deep-correctness | 回収（Critical なし／Warning 4） |
| investigator-chunk-1-seam-integration | 回収（Critical なし／Warning 2） |
| investigator-chunk-2-context-consistency | 回収（Critical なし／Warning 2） |
| investigator-chunk-2-deep-correctness | 回収（Critical なし／Warning 3） |
| investigator-chunk-2-seam-integration | 回収（Critical なし／Warning 2） |
| 決定論チェック（checks.json） | 回収（confirmed 0件／候補 comment_length 49・passthrough_property 1） |
| Codex 外部監査（codex-audit） | **完走し `.final.md` に結論あり（4.8KB・非空）→ 通常の1系統として統合済み**（Medium 1件＝C2）。`codex_recover.py` の実行は不要だった。欠員ではない。bughunt／design の2本は本実行の起動計画に含まれておらず、起動されていない（prompt ファイルも無い）＝縮退申告の対象外 |
