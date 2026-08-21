# inGameObjectControlのフラグをworldObjectEnableへ改名する

決定: `commands.yaml` の `mapObjectEnable` を `worldObjectEnable` へリネームし、`100_start_game.json`(2コマンド)・i18n(japanese/english)・commandListLabelFormat を一括更新する。

棄却案: `mapObjectEnable` の名前を据え置き、C#側だけ載せ替える（改修は最小）

理由: 束ねた後の実処理は mapObject＋露頭。AGENTS.md「名前は実処理と一致させる」「変更の波及を恐れない」。名前が実態とズレると次の改修者が同じ取りこぼしを繰り返す。

リンク: [[2026-08-18-スキットの世界非表示は共通interfaceへ載せ替える.md]] / docs/adr/0016-skit-hides-world-objects-through-shared-interface.md
