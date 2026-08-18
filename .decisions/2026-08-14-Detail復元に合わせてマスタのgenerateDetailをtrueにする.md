決定: Task 8/9（Detailのwinnerマスク復元・SDF距離マップ供給）を実装し、加えてv8マスタ server_v8/mods/moorestechAlphaMod_8/master/generation.json の generateDetail を true へ変更する。planのGlobal Constraints「マスタデータ値の変更なし」はこの1点のみ緩める。
棄却案: 実装するがマスタは false のままで機構だけ復元する案。Task 8/9 を本planから外しDetail有効化の別planへ送る案。
理由: generation.json:460 が generateDetail: false のためTask 13のゲート実装後はTask 8/9の成果がproductionで一度も実行されず死コードになる。実際に草・下草が描画される状態にする方が復元の目的に適う。
リンク: [[2026-08-14-移植漏れは全て実装復元する]] / bd moorestech-edd
