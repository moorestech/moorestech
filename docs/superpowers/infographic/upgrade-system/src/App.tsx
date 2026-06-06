import { CommentLayer } from './components/comment-feature';
import { FigureZoomLayer } from './components/FigureZoomLayer';
import { Hero } from './sections/Hero';
import { Origin } from './sections/Origin';
import { CoreMechanic } from './sections/CoreMechanic';
import { EffectAxes } from './sections/EffectAxes';
import { LevelRep } from './sections/LevelRep';
import { TwoLayer } from './sections/TwoLayer';
import { ProcessFlow } from './sections/ProcessFlow';
import { Process } from './sections/Process';
import { References } from './sections/References';

export function App() {
  return (
    <div className="page">
      <CommentLayer />
      <FigureZoomLayer />
      <Hero />
      <Origin />
      <CoreMechanic />
      <EffectAxes />
      <LevelRep />
      <TwoLayer />
      <ProcessFlow />
      <Process />
      <References />
    </div>
  );
}
