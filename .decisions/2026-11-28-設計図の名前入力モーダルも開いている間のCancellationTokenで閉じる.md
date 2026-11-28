# 設計図の名前入力モーダルも開いている間のCancellationTokenで閉じる

決定: 設計図の名前入力のモーダルも、開いた側の `CancellationToken` で閉じる。名前入力が開くたびに `CancellationTokenSource` を作ってそのトークンを要求に渡し、名前入力が閉じるときに `Cancel` する。`CancellationToken.None` を渡して戻り値の持ち手を `Dispose` する形はやめる。
棄却案: 今のまま（`CancellationToken.None` を渡し、閉じるときに持ち手を `Dispose` する）。
理由: ユーザーは理由を述べていない。問いに添えた説明は「今は None を渡して持ち手の Dispose で閉じており、通知の途中で閉じられるとモーダルが残る穴があります」（Claude の説明）。
明示引用: 原文「モーダル使うところ、ちゃんとctは適切に管理してる？」（2026-11-28）
明示引用: 問い「PR #1704 で、設計図の名前入力モーダルも CancellationToken で閉じる形（開くたびに CTS を作り、閉じるとき Cancel）に直すかを決めてください。推奨は「直す」です」→ 原文「はい」（2026-11-28）
補足: [[2026-11-28-モーダルは開いた側のCancellationTokenで閉じ持ち手はIDisposableにする]] の持ち手（`IDisposable`）と基盤の形は変えない。この変更の後、持ち手の `Dispose` を呼ぶ本番の呼び手は無くなる。
リンク: docs/adr/0105-clean-room-screens-show-final-odds-and-units-to-next-class.md §4 / bd moorestech-kgfsp.3 / PR #1704
