# generateHeightmap は内側のステージ有効化フラグとして生かす

- 日付: 2026-08-23（moores-code-review C1/D1 の裁定）
- 決定: `TileVisualBaker` 内で `generateHeightmap` を見て、false なら平坦配列を `BakedTerrainTile.DisplayHeights` に入れる。クライアント側の分岐は復活させない（R2 維持）。回帰テストも復元する
- 棄却案: `TerrainGenerationConfig.generateHeightmap`・`GenerationRuntimeConfigFactory` の代入・generation スキーマの当該フィールドを全JSON一括削除する
- 理由: `generateTexture`/`generateDetail` を「ステージ有効化フラグとして内側に残す」とした R9 と同型に揃う。平坦デバッグ手段も残る。削除案は「誰も読まないマスタフィールドを残さない」原則には素直だが、デバッグ手段の喪失に見合わない
- 背景: 見た目生成の移設で `TerrainDataAssembler` の `if (!config.generateHeightmap) return;` が消え、このフラグの読み手が0件になっていた（マスタで false を指定しても例外もログも出ずに起伏付きの地形が建つ）
- リンク: [[docs/adr/0025-generation-system-exposes-results-only.md]] / plan docs/superpowers/plans/2026-08-21-terrain-generation-boundary.md
