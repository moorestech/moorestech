type Row = {
  axis: string; color: string; main: string; tradeoff: string;
};

const ROWS: Row[] = [
  { axis: '速度', color: 'var(--speed)', main: '処理時間を短縮', tradeoff: '電力 / トルク消費が増える' },
  { axis: '生産性', color: 'var(--productivity)', main: '確率で追加産出（完了時に出力1セットを上乗せ）', tradeoff: '処理速度が落ちる（Factorio 準拠）' },
  { axis: '省エネ', color: 'var(--efficiency)', main: '消費を下げる', tradeoff: '単独では弱め。速度の相殺用' },
  { axis: '品質', color: 'var(--quality)', main: '高レベル産物の出現率を上げる', tradeoff: '処理速度が落ちる' },
];

export function EffectAxes() {
  return (
    <section className="section" id="axes">
      <div className="eyebrow">03 — 効果軸</div>
      <h2>4 つの効果軸とトレードオフ</h2>
      <p className="lead">
        採用した効果軸は 4 つ。同一機械内で複数モジュールの効果は<strong>加算</strong>される。
        重要なのは、この 4 軸が「性質の違う 2 グループ」に分かれること（次セクション）。
      </p>

      <div className="table-wrap">
        <table className="tbl">
          <thead>
            <tr>
              <th style={{ width: '92px' }}>モジュール</th>
              <th>主効果</th>
              <th>トレードオフ</th>
            </tr>
          </thead>
          <tbody>
            {ROWS.map((r) => (
              <tr key={r.axis}>
                <td><span className="axis-tag" style={{ background: r.color }}>{r.axis}</span></td>
                <td>{r.main}</td>
                <td>{r.tradeoff}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      <div className="card good">
        <h4>tier（Mk1〜）で長期成長軸にする</h4>
        <p style={{ margin: 0 }}>
          各軸に Mk1〜Mk3 等の段階を設け、上位ほど主効果が高い。製造コストと研究で解放をゲートし、
          終盤までモジュールを作り続けるモチベーションにする。
        </p>
        <p style={{ margin: '10px 0 0', fontSize: '13.5px', color: 'var(--text-muted)' }}>
          ※ モジュールの段階（Mk）は、品質モジュールが生む<strong>産物のレベル（Lv1 / Lv2…）</strong>とは別概念。
          Mk は「挿すモジュールの強さ」、Lv は「機械が作り出すアイテムの品質」を指す。
        </p>
      </div>
    </section>
  );
}
