決定: PlayerBuildRequest のバリアント表現は3つのboolのまま維持する。ExitOnFinish の削除（PlayerBuildOutcome導入）までで十分とする。
棄却案:
- enum BundlingPolicy { FailFast, WarnOnly } を導入して isStrict のboolスレッドを置換する
- 入口バリアントを型に分ける（LocalDistributionBuildRequest / CiBatchBuildRequest、Execute は switch 網羅）
理由: 結果型化で最も危険だった暗黙分岐（ExitOnFinish）は消えた。残る3boolは名前から意味が明確で、今の入口2つに対して型を増やす利得が小さい。
リンク: docs/plans/pr-1116-independent-review-fix-plan.md B-5
