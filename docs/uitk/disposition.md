# 写さない Web テストの処遇

段階ごとの削除 PR で、消えた Web のテスト名（Playwright・vitest）と増えた NUnit のテスト名をスクリプトで突き合わせる（計画 `docs/uitk-migration/plan.md` §4.1）。写さないテストはここに 1 件 1 行で理由を書く。目視で済ませるものは証跡のパス、捨てるものは理由。見た目の意図は `docs/uitk/TODO.md` の参照キャプチャで辿る。

| 束 | Web のテスト（ファイル・テスト名） | 処遇（目視・捨てる） | 証跡のパスか理由 | PR |
|---|---|---|---|---|
| 4 | `src/shared/itemMove/planDirectMoves.test.ts` | maxStack 不明時（マスタ未ロード）は同種探索をスキップし空スロットのみ使う | 捨てる | UITK はマスタを同期で持ち、スタック上限は `IItemStackLevelLookup.GetMaxStack` で常に引ける。Web の「マスタ未ロード（HTTP 取得前）」の状態が UITK に無い（設計書 [A-13] の ConnectingPlaceholder と同じ理由）。L0-s4a 判断記録 D-5 | L0-s4a |
