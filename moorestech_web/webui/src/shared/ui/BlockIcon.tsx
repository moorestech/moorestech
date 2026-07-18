import GameIcon from "./GameIcon";

type Props = {
  blockId: number;
  alt?: string;
  className?: string;
};

export default function BlockIcon({ blockId, alt, className }: Props) {
  return <GameIcon id={blockId} src={`/api/block-icons/${blockId}.png`} alt={alt ?? `block ${blockId}`} className={className} />;
}
