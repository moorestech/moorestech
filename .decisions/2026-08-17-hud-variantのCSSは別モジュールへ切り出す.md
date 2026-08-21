# hud variantのCSSは別モジュールへ切り出す

決定: `GamePanel/style.module.css` から `.hud` / `.hud::before` を `GamePanel/hudVariant.module.css` へ分離する。`hudVariantDesign.test.ts` の読み先も新ファイルへ変える。

棄却案: 221行のまま許容（plan通りの単一ファイル維持）／既存 `.panel`・`.craft`・`.skit` ごと圧縮して200行以下へ収める。
あわせて棄却: hud variant で `decoLine` を variant ガードで構造的に排除する案。

理由: 前者は「1ファイル200行以下」規約超過を残しレビューで再指摘される。後者は既存スタイルへの回帰リスクが高い。罫線ガードは hud に `title` を渡す利用者が存在せずYAGNIのため、plan通り放置する。

リンク: docs/superpowers/plans/2026-08-17-webui-challenge-hud-face-and-notification-animation.md
