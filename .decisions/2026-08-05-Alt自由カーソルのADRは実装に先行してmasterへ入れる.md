決定: PR #1129 が追加するAlt自由カーソル関連の `.decisions/` 9件は、対応する実装コードが1行も無い状態のままmasterへ入れる（Alt実装は後続で行う）
棄却案: ①Alt関連ADR 9件をPRから外し、Alt実装PRと同時にマージする ②本PRでAlt実装まで含める
理由: 後で実装する前提が立っているため。決定記録が先行しても実装予定が生きていれば問題にしない
補足: 独立レビューが挙げた懸念は「`.decisions/` はAGENTS.md上ユーザー裁定の蒸留であり、実装なしで記録だけがmasterに入ると後続セッションが『決定済み＝実装済み』と読んでAlt機能が落ちる」というもの。この誤読リスクは受け入れる。
未実装のsurface（独立レビューが `git grep` で確認・いずれもHEADに0ヒット）: `Control/ViewMode/GameScreenCameraInteractionService.cs` の新設とGameScreenStateからの委譲・DI登録／`HybridInput` のKeyCode→Keyマップへの `LeftAlt` 追加（現在マップに無く `GetKey(KeyCode.LeftAlt)` が常にfalse）／Alt押下中のみのホールド判定とTPS限定／解放時の `Mouse.WarpCursorPosition` による画面中央ワープ／`AimPointProvider` を視点モード自動導出（`PlayerViewApplier.cs:28` 経由）からUIステート側プッシュ契約へ変更／非Alt時のTPS照準を明示的に `ScreenCenter` にする（既存テスト `Client.Tests/ViewMode/AimPointProviderTest.cs` が旧仕様TPS=Mouseを固定しているため要更新）。
実装漏れの防止は現状このファイルだけが担っている（本repoはbd未初期化のためタスク化できていない）
リンク: 出所=ユーザー裁定 2026-08-05（PR #1129独立レビューのダイジェスト裁定「後で実装するから別にこれでいい」）
