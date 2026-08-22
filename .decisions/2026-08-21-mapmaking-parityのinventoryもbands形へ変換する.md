決定: `scripts/mapmaking-parity/species-inventory.json` の objectConfig entries を、v8マスタで実施した機械変換と同型で bands 形へ変換し、`gen_generation_objectconfig.py` の再生成経路を復旧する。

棄却案: 案B スクリプトが bands 移行後は動作しないことを plan/ADR に明記して再生成経路を放棄する（planの非目標記述と整合するが、壊れたツールが残る）／案C スクリプトごと削除する。

理由: bands 移行後、当該スクリプトは grassland の1件目で `ValueError` を投げて確実に停止する。CI では実行されないため手動実行時にのみ露見する。planは「Mooresmasterがロードしない資料だから」を理由に非目標としていたが、それは更新しない理由にはなっても壊してよい理由にはならない。

リンク: [[2026-08-21-散布バンドの量指定はdensityへ統一しclusterCountを廃止する]]
