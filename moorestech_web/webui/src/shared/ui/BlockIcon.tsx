import { blockIconUrl } from "@/bridge";
import GameIcon from "./GameIcon";

type Props = {
  blockId: number;
  alt?: string;
  className?: string;
};

export default function BlockIcon({ blockId, alt, className }: Props) {
  return <GameIcon id={blockId} src={blockIconUrl(blockId)} alt={alt ?? `block ${blockId}`} className={className} />;
}
