# uiObjectIdの正本はCore.Masterに置く

**[上書き済み]** 同日の裁定「チュートリアルUI指定はWebアンカーIDを直書きし変換も検証も持たない」により、uiObjectId語彙自体が廃止された。本裁定は歴史的記録として残す。

決定: チュートリアルの uiObjectId（静的キー集合と `buildMenuBlock:` / `researchNode:` prefix）の正本を Core.Master 側に置き、クライアントの TutorialAnchorIdMapper はそれを参照するだけにする。マスタ検証は起動時フェイルファストとし、静的キー一致のテストで機械保証する。

棄却案:
- challenges.yml を判別子付きcase（staticUi enum / buildMenuBlock uuid+foreignKey / researchNode uuid+foreignKey）へ分割しSourceGeneratorとforeignKeyに検証を任せる（本筋だが全マスタJSON・生成スクリプト・Web側の一括移行が必要）
- 現状維持（サーバ検証とクライアント変換で二重手書き）

理由: ユーザー裁定 2026-08-19（AskUserQuestion）。本PR内で閉じられ、3rd-party modにも効く最小の一本化を選ぶ。UI語彙がCore.Masterに載る点は challenges.json の値検証として許容する。
