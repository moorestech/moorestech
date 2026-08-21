決定: mapObjectの配置データ（PlacedMapObject→map.json→va:mapData→クライアントInstantiate）はTransformと同じ3要素（位置Vector3・回転・スケールVector3）を全区間で持つ。sinkは生成時にY座標へ畳み込み、bendFactorは破棄する。

棄却案: TreeInstance相当の最小形（rotationY / scaleWidth / scaleHeight の3値のみ追加）。

理由: ユーザー裁定「transformと同じものを返す。つまり、位置、回転、大きさの3値」。汎用形にして将来の斜面整列等にも対応できる形を取る。

リンク: grillセッション 2026-08-16（map object 3Dモデル・草配置のMapMaking同一化）
