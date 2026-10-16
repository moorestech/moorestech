決定: 地形めり込み判定（底から1セルより深い沈みを不可）は、設置Yの決定と同じ GroundHeightProbe（占有セル4隅への下向きレイ）でフットプリント最高点を取り「底+1」と比較して測る。プレビューはColliderを持たず、判定箱・kinematic Rigidbody・GroundCollisionDetector・PlacementGroundContactQuery は不要になる。
棄却案: 占有サイズから計算した箱で Physics.OverlapBox により TerrainCollider へ直接問い合わせる（4隅間の尖りも拾えるが、高さ1は上面より上の箱が別に要り、完全地中の検出は未実測、Y決定と別データになる）。
理由: ユーザー裁定 2026-10-15 提示「高さサンプル（推奨）／計算した箱で物理問い合わせ」に対し選択「高さサンプル (推奨)」。帰結として4隅の間の尖った盛り上がりは拾わず置ける。
リンク: [[2026-10-15-地形めり込み判定は全設置系で底から1セルより深い沈みを不可にする]] / GroundHeightProbe.cs
