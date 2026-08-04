決定: mapObjectに無限採掘を正式導入し、スキーマはdurabilityType enum(finite/infinite)+durabilityParam switchで表現する。finiteは現行のhp+earnItemHpInterval、infiniteはearnItemHpIntervalのみ（Nダメージ蓄積ごとにドロップ抽選・永久に壊れない）。既存の全mapObject JSONは一括更新し、巨大HP(100000等)による無限代用を廃止する。
棄却案:
- isInfinite boolを足してinfinite時はhpを無視する（死にフィールドが残る）
- hp: -1等の番兵値で無限扱い（マジックナンバー・フォールバック吸収の禁止原則に反する）
理由: 無限時に意味を持たないhpをスキーマ上から消せる形が最も正しく、変更の波及（全JSON一括更新）を恐れない原則に従う。
リンク: .decisions/2026-08-04-露頭をサーバー管理mapObjectとして手掘り対象にする.md
