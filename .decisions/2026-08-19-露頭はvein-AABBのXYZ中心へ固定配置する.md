決定: 露頭は地表Raycastを行わず、vein inclusive AABBのXYZ中心へ常に配置する
棄却案: TerrainColliderがRaycast可能になるまで待機してAABB中心XZの地表へ配置する
理由: 露頭を鉱脈中心の可視マーカーとして扱い、TerrainCollider反映タイミングへの依存をなくすため
リンク: [[2026-08-04-露頭の地表未解決はAABB高さフォールバックで設置する]] / [[2026-08-04-露頭はvein-AABBごとに1個生成する]]
