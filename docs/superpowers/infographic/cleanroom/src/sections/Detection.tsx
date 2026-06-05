import { FigureFrame } from '../components/comment-feature';

// 5x5 断面のセルを生成する。perimeter=壁、内部=空気。holeAt で壁を1つ欠けさせる。
// Build a 5x5 cross-section: perimeter = wall, interior = air; holeAt removes one wall.
function RoomGrid({ ox, oy, hole }: { ox: number; oy: number; hole?: [number, number] }) {
  const S = 30;
  const cells = [];
  for (let r = 0; r < 5; r++) {
    for (let c = 0; c < 5; c++) {
      const isWall = r === 0 || r === 4 || c === 0 || c === 4;
      const isHole = hole && hole[0] === r && hole[1] === c;
      const x = ox + c * S;
      const y = oy + r * S;
      if (isWall && !isHole) {
        cells.push(<rect key={`${r}-${c}`} x={x} y={y} width={S} height={S} rx={3} fill="#2563EB" stroke="#1E40AF" strokeWidth={1} />);
      } else {
        const air = !isWall;
        cells.push(<rect key={`${r}-${c}`} x={x} y={y} width={S} height={S} rx={3} fill={air ? '#E0F2FE' : '#FFFFFF'} stroke="#CBD5E1" strokeWidth={1} strokeDasharray={isHole ? '4 3' : undefined} />);
      }
    }
  }
  return <g>{cells}</g>;
}

export function Detection() {
  return (
    <section className="section">
      <div className="eyebrow">02 — 部屋検出（3D密閉）</div>
      <h2>「囲えているか」を flood-fill で判定する</h2>
      <p className="lead">
        密閉境界になれるのは <b>ICleanRoomBoundaryComponent を持つブロック</b>（壁・ドア・ハッチ・パイプコネクタ）だけ。
        内部の空セルから6近傍で flood-fill し、外へ漏れずに閉じれば1部屋。機械など非境界ブロックは<b>通過セル</b>として扱い、体積に数える。
      </p>

      <FigureFrame label="図02 左は壁(青)で完全に囲まれた断面で、内部の空気セル(水色)から6近傍flood-fillしても壁の内側で閉じるため1部屋として成立する。右は壁が1つ欠けた断面で、flood-fillが穴から境界AABB(破線)の外へ抜け、AABB外縁に到達した瞬間にリークとして不成立になる様子">
        <div className="figure-frame">
        <svg viewBox="0 0 660 290" width="660" role="img" aria-label="密閉とリークの断面比較">
          {/* 左: 密閉 */}
          <text x="95" y="24" textAnchor="middle" fontSize="14" fontWeight="700" fill="#065F46">密閉 → 1部屋</text>
          <RoomGrid ox={20} oy={40} />
          <text x="95" y="218" textAnchor="middle" fontSize="12" fill="#475569">体積 V = 9セル ／ 表面積 S = 12面</text>
          <text x="95" y="236" textAnchor="middle" fontSize="11" fill="#64748B">(図は2D断面。実際は3D)</text>

          {/* 右: リーク */}
          <text x="470" y="24" textAnchor="middle" fontSize="14" fontWeight="700" fill="#B91C1C">穴あき → 不成立</text>
          {/* 境界AABB（破線の外枠 = 壁の外接箱）。穴の外側に1セル広げて描く */}
          <rect x={393} y={37} width={156} height={156} fill="none" stroke="#DC2626" strokeWidth={1.5} strokeDasharray="5 4" rx={4} />
          <text x="470" y="34" textAnchor="middle" fontSize="10" fill="#DC2626">境界AABB（この外＝外部）</text>
          <RoomGrid ox={395} oy={40} hole={[4, 2]} />
          {/* 漏れの矢印: 内部(2,2)→穴(4,2)→AABB外へ下方向 */}
          <path d="M 470 100 L 470 205" stroke="#DC2626" strokeWidth={2.4} markerEnd="url(#arrowR)" fill="none" />
          <text x="470" y="225" textAnchor="middle" fontSize="12" fill="#B91C1C" fontWeight="700">AABB外縁に到達＝リーク</text>
          <defs>
            <marker id="arrowR" markerWidth="9" markerHeight="9" refX="6" refY="4.5" orient="auto">
              <path d="M0,0 L9,4.5 L0,9 Z" fill="#DC2626" />
            </marker>
          </defs>
        </svg>
        </div>
      </FigureFrame>

      <h3>リーク判定は「AABB外縁到達」で即決まる</h3>
      <p>
        無限の空間で「囲えているか」を判定するには境界が要る。素朴に「一定セル歩いたら外部」とすると、穴あき構造を毎回大量に探索して重い。
        そこで<b>全境界ブロックの外接箱（AABB）</b>を上限に使い、flood-fill がその外に出た瞬間にリークと確定する。
        体積上限 <code>MaxRoomVolume = 4096</code> は異常巨大ケースの安全網。
      </p>

      <div className="code-head"><span className="pill">C#</span> CleanRoomDetector — flood-fill の核</div>
      <div className="code" dangerouslySetInnerHTML={{ __html:
`<span class="c">// 種から通過セルを flood-fill。境界AABB外に出たら（=漏れ）false。</span>
<span class="k">while</span> (stack.Count &gt; <span class="n">0</span>)
{
    <span class="k">var</span> cur = stack.Pop();

    <span class="k">if</span> (cells.Count &gt; MaxRoomVolume || <span class="fn">IsOutsideAabb</span>(cur, aabbMin, aabbMax))
        <span class="k">return</span> <span class="k">false</span>;        <span class="c">// 漏れ / 暴走 → 不成立</span>

    <span class="k">foreach</span> (<span class="k">var</span> n <span class="k">in</span> <span class="fn">SixNeighbors</span>(cur))
    {
        <span class="k">if</span> (boundary.Contains(n)) { surfaceArea++; <span class="k">continue</span>; } <span class="c">// 境界面に接触 → S+1</span>
        <span class="k">if</span> (cells.Add(n)) stack.Push(n);                       <span class="c">// 通過セル → V に加える</span>
    }
}` }} />

      <div className="callout info">
        <b>派生状態として持つ：</b> 部屋はブロックから常に再計算できるのでフェーズ1では<b>セーブしない</b>（ロード後に再検出で再構築）。
        ただし <code>Cells</code> を公開し、フェーズ2で純度状態 N を引き継ぐときに再検出前後の部屋を<b>セル重なりで対応付け</b>られるようにしてある。<code>Id</code> は永続キーにしない。
      </div>
    </section>
  );
}
