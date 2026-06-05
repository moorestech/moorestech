import { CommentLayer } from './components/comment-feature';
import { FigureZoomLayer } from './components/FigureZoomLayer';
import { Hero } from './sections/Hero';
import { Overview } from './sections/Overview';
import { Detection } from './sections/Detection';
import { PurityModel } from './sections/PurityModel';
import { ClassBinning } from './sections/ClassBinning';
import { PollutionLoop } from './sections/PollutionLoop';
import { Architecture } from './sections/Architecture';
import { References } from './sections/References';

export function App() {
  return (
    <div className="page">
      <CommentLayer />
      <FigureZoomLayer />
      <Hero />
      <Overview />
      <Detection />
      <PurityModel />
      <ClassBinning />
      <PollutionLoop />
      <Architecture />
      <References />
    </div>
  );
}
