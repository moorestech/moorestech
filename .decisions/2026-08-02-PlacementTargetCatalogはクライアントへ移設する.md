決定: Game.PlacementTarget（サーバ側新アセンブリ）の PlacementTargetCatalog を Client.Game 配下へ移設し、IBlueprintCatalogSource と新アセンブリを廃止する。設置対象Guid統一の語彙（Kind・カタログ）自体は承認。
棄却案:
- サーバ側アセンブリのまま interface で依存反転を維持する
理由: カタログのproduction参照はクライアント8ファイル・3アセンブリでサーバ0。interfaceは配置ミスを取り繕う接着剤になっていた。サーバに具体的な消費者が現れた時に改めて切り出す。
リンク: PR #1095 独立レビューC4/D2/新形N1
