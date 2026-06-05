import { FigureFrame } from '../components/comment-feature';

export function PurityModel() {
  return (
    <section className="section">
      <div className="eyebrow">03 — 純度の数理モデル</div>
      <h2>濃度は「入る速度 ÷ 出る能力」で落ち着く</h2>
      <p className="lead">
        各部屋は不純物総数 N と体積 V を持ち、濃度 <b>C = N / V（個/m³）</b>で清浄度を測る。
        汚染源が毎秒 A 個を足し、空気清浄機が<b>毎秒一定体積 q を処理して濃度に比例除去</b>する。
        この設計だと濃度は安定した平衡値に収束し、クラスが連続スペクトルになる。
      </p>

      <FigureFrame label="図03 部屋(体積V・不純物N)に対し、左から汚染源が毎秒A個を加算し、右で清浄機n台が毎秒q体積を処理して n·q·C 個を除去する収支。dN/dt = A − n·q·C がゼロになる平衡で濃度が C_eq = A/(n·q) に収束する関係">
        <div className="figure-frame">
        <svg viewBox="0 0 660 250" width="660" role="img" aria-label="部屋の不純物収支と平衡">
          {/* room box */}
          <rect x="210" y="55" width="240" height="120" rx="14" fill="#F0F9FF" stroke="#0EA5E9" strokeWidth="2.5" />
          <text x="330" y="92" textAnchor="middle" fontSize="14" fontWeight="700" fill="#0369A1">クリーンルーム</text>
          <text x="330" y="118" textAnchor="middle" fontSize="16" fontWeight="700" fill="#0F172A">不純物 N</text>
          <text x="330" y="140" textAnchor="middle" fontSize="13" fill="#475569">体積 V（m³）</text>
          <text x="330" y="160" textAnchor="middle" fontSize="13" fill="#0EA5E9">濃度 C = N / V</text>

          {/* in arrow */}
          <path d="M 60 115 L 205 115" stroke="#DC2626" strokeWidth="3" markerEnd="url(#aIn)" fill="none" />
          <text x="130" y="100" textAnchor="middle" fontSize="13" fontWeight="700" fill="#B91C1C">汚染 +A</text>
          <text x="130" y="135" textAnchor="middle" fontSize="11" fill="#B91C1C">個/秒</text>

          {/* out arrow */}
          <path d="M 455 115 L 600 115" stroke="#059669" strokeWidth="3" markerEnd="url(#aOut)" fill="none" />
          <text x="528" y="100" textAnchor="middle" fontSize="13" fontWeight="700" fill="#047857">除去 n·q·C</text>
          <text x="528" y="135" textAnchor="middle" fontSize="11" fill="#047857">清浄機 n台 × 処理体積 q</text>

          {/* equilibrium */}
          <text x="330" y="210" textAnchor="middle" fontSize="14" fill="#0F172A">
            平衡（dN/dt = 0）：<tspan fontWeight="700" fill="#0369A1">C_eq = A / (n·q)</tspan>
          </text>
          <text x="330" y="232" textAnchor="middle" fontSize="11.5" fill="#64748B">入る速度と出る能力が釣り合う濃度に収束する</text>

          <defs>
            <marker id="aIn" markerWidth="9" markerHeight="9" refX="6" refY="4.5" orient="auto"><path d="M0,0 L9,4.5 L0,9 Z" fill="#DC2626" /></marker>
            <marker id="aOut" markerWidth="9" markerHeight="9" refX="6" refY="4.5" orient="auto"><path d="M0,0 L9,4.5 L0,9 Z" fill="#059669" /></marker>
          </defs>
        </svg>
        </div>
      </FigureFrame>

      <div className="formula">
        <div>dN/dt = <em>A</em> − <em>n·q·C</em></div>
        <div className="big" style={{ marginTop: 10 }}>C_eq = A / (n·q)</div>
        <span className="cap">平衡濃度は体積 V に依存しない。これがクラスを「0か∞」でなく連続値にする鍵。</span>
      </div>

      <h3>なぜ「固定個数除去」ではダメか</h3>
      <p>
        清浄機を「濃度に関係なく毎秒 R 個除去」にすると、<code>dN/dt = A − n·R</code> が定数になり、
        N は <b>0 にクランプ（常に最良）</b>か <b>無限に発散（常に最悪）</b>の二択に崩壊する。
        安定した中間値が存在せず、せっかくの多段クラスがコイントスになる。体積処理式なら必ず平衡に収束する。
      </p>

      <h3>体積 V は「定常コスト」と「バースト緩衝」の両方に効く</h3>
      <div className="table-wrap">
        <table className="tbl">
          <thead><tr><th>側面</th><th>体積 V の効き方</th></tr></thead>
          <tbody>
            <tr><td><b>定常</b></td><td>汚染 A に体積・表面積比例項 <code>a_volume·V + a_surface·S</code> を含め、さらにクラス成立に換気回数 <b>ACH = n·q/V ≥ 必要値</b> を要求する。大部屋ほど清浄機・電力・フィルターが重い。</td></tr>
            <tr><td><b>過渡</b></td><td>ドア開閉やハッチ搬入出の固定個数スパイクを、大部屋は薄めて吸収（許容的だが回復は遅い <code>V/(n·q)</code>）、小部屋は濃度が大きく跳ねる。</td></tr>
          </tbody>
        </table>
      </div>
      <div className="callout info">
        <b>集約の緊張：</b> 「大部屋＝バースト耐性は高いが維持コストと必要換気量が重い／小部屋＝安いがスパイクに弱い」。
        これで3D体積判定という重い実装に意味が生まれ、汚い工程と超清浄工程を分ける判断が成立する。
      </div>
    </section>
  );
}
