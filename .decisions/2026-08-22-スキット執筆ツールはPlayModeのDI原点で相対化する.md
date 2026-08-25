# スキット執筆ツールはPlayModeのDI原点で相対化する

## 決定（moores-code-review run 2026-08-22-2052 の設計判断3件・AskUserQuestion 2026-08-22）
- **D1**: SkitCamera/SkitCharacterEditorUtilInspector のコマンドコピーは、PlayMode中のDIコンテナから実行時と同一の `SkitOrigin` を解決し `ToRelative` で相対化して出力する。解決できない時はコピーを拒否しダイアログで知らせる（窓口は `SkitAuthoringOriginResolver` に一本化）
- **D2**: 位置コマンドの `origin.ToWorld` 加算漏れは、commands.yaml の位置プロパティとコマンドソースを突き合わせるガードテスト1本（`SkitPositionalCommandOriginGuardTest`）で守る。実装は現行の各コマンド内加算のまま
- **D3**: BackgroundSkitManager の StoryContext には `new SkitOrigin(Vector3.zero)` を明示登録し「背景スキットは位置コマンドを使わない（原点なし）」の意図をコードに残す

## 棄却した案
- D1: シーン原点マーカー（人手合わせのずれ再発）／執筆用定数 AuthoringOrigin（実スポーンとの二重管理）／貼付後一括変換ツール（変換忘れ検出不能）／別タスク送り
- D2: SkitRelativePosition 型で強制（生成器契約へ波及し裁定範囲超え）／SkitCamera等の実装側へ原点注入（加算が3クラスへ分散・レンズ間で推奨が割れた）／無策
- D3: 現状維持（暗黙規律）／StoryContext ctor で必須化（裁定範囲超え。将来移行は可）

リンク: ADR 0029 / [[2026-08-22-スキット座標はスポーン基準の相対座標へ相対化する]] / bd moorestech-gbkd
