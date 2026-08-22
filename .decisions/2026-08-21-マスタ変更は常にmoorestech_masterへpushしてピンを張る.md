# マスタ変更は常にmoorestech_masterへpushしてピンを張る

2026-08-21 ユーザー裁定（液体色マスタ化 D8 の進め方）

## 決定

マスタデータの変更を伴う作業は、`moorestech_master` へコミット＋**push** し、その push 済み SHA を本repoの
`.moorestech-external-revisions.json` のピンに据える運用とする。ローカルコミット止まりでピンを張らない。

## 棄却案

- **本repo分だけ実装してmaster更新とピン更新をマージ直前に手動でやる** — ピンが実体と食い違う期間が生まれ、
  現に P0 `moorestech-hvwb`（ピンが存在しないコミットを指しCI全PR停止）と同じ事故の温床になる
- **D8を別PRへ分ける** — 同日の「本PRに含める」裁定を巻き戻すことになる

## 理由

ピンが指す先が push 済みであることが CI の前提。ローカルにしか無い SHA を指した瞬間に全PRのCIが落ちる。
「常に push する」を運用の既定にすれば、この破れ方が構造的に起きなくなる。

## 未解決（実装時に確認が要る）

`moorestech-worktrees/moorestech_master` は**全worktree共有のシンボリックリンク**で、現在は別セッションの
worktree（`vein-hand-mining` / ブランチ `feature/blocks-placements-per-cost` @ 990298f）を指し未コミット変更を抱えている。
色を足した SHA をピンに据えると、共有シンボリックリンクの指す実体にはその色が無いため、ローカルの
PlayMode/実データ検証だけがマスタロードに失敗する。押し込み先ブランチとローカル検証の段取りは別途詰める。
