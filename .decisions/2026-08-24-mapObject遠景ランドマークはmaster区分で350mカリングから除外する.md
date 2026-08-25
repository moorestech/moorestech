# mapObject遠景ランドマークはmaster区分で350mカリングから除外する

決定:
- ユーザー裁定 2026-08-24: 通常mapObjectは現在描画中のカメラから350mを超えると非表示にする
- ユーザー裁定 2026-08-24: `BigMesa_*`・`ThinMesa_*`・`StratMesaSharp_*`・`Boulders_*`・`BigBoulders_*` は遠景ランドマークとして距離カリングから除外する
- ユーザー裁定 2026-08-24: 遠景ランドマークの判定は全mapObject master必須の表示区分で明示する
- ユーザー裁定 2026-08-24: 本repoの1PRへSSAO/シャドウ設定・350m距離カリング・mapObjectのLight/Reflection Probe無効化をまとめる
- ユーザー裁定 2026-08-24: Renderer統合・MeshCollider簡略化・mesa imposter化は今回のPRへ含めない
- agent前提: 距離カリングは描画だけを休止し、GameObject・Collider・状態同期・最寄り探索登録は維持する
- agent前提: 340mで再表示、350mで非表示にするヒステリシスを設ける
- agent前提: カメラ切替は変化通知で受け、大量の表示切替は時間予算でフレーム分散する
- agent前提: master変更は`moorestech_master`のcompanion PRを作り、本repoのpinをそのpush済みコミットへ更新する

棄却案:
- mapObject名の前方一致をクライアントコードにハードコードする

理由:
- 新しいランドマーク追加時にコード変更が必要で、データ上の役割が名前へ隠れるため

リンク: docs/adr/0032-mapobject-distance-culling.md / bd moorestech-ara
