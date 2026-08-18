# HUDの面はGamePanelのhud variantで供給する

決定: 目標HUDの背景は `GamePanel` に新variant `"hud"` を追加して供給する。面と境界フェードだけを持ち、タイトル罫線・下向き三角・右下グリップ・正本合わせの実測オフセットは持たない。

棄却案: HUD側CSS（`CurrentChallengeHud.module.css` の `::before`）へトークン化した面を直接描く（NotificationHost・world-pinラベル面の前例に倣う形だが、§2の「GamePanel外で独自CSSのパネル面を作るのは禁止」へHUD例外を切る必要がある）／既存 `variant="default"` をそのまま流用する（インベントリ面と完全一致するが、下向き三角3個・左28pxの非対称padding・上端の実測オフセット -3.9px までHUDに付いてくる）。

理由: §2 が「新しい見た目は GamePanel に variant を追加してから使う」と規定しており、それに忠実。面表現の供給元を1箇所に保ったまま、将来の他HUD（装備HUD等）へも同じ面を展開できる。

リンク: [[2026-08-17-目標HUDの面色は既存の半透明ネイビーを流用する.md]]、`.claude/skills/webui-design/SKILL.md` §2 / §8.14
