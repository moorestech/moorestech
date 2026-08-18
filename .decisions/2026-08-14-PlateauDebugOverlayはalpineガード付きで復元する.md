決定: Task 12 の PlateauDebugOverlay を復元するが、そのために必要なクライアント側 HeightmapStage.Run の追加実行を alpineEnabled && alpine.enablePlateau && alpine.debugPlateauOverlay（移植元 MM/Pipeline/TerrainGenerator.cs:825 と同条件）でガードしproductionコストをゼロにする。Task 14（placementNoiseテクスチャ源）は効果ゼロの機構復元だがR10どおり実装する。
棄却案: Task 12 を本planから削除しAlpineを使うマスタが出た時点で別途復元する案。Task 14 をスコープ外にしテクスチャノイズ使用時に再裁定する案。
理由: v8マスタは alpineEnabled: false のため overlay は1画素も塗らない。無条件に HeightmapStage.Run を足すと25タイル分（2049の2乗×25）の純粋な無駄計算になる。ガードすれば復元と実コストゼロを両立できる。
リンク: [[2026-08-14-移植漏れは全て実装復元する]] / bd moorestech-edd
