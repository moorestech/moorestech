interface IconProps {
  open?: boolean;
}

export function Chevron({ open }: IconProps) {
  return (
    <svg width="10" height="10" viewBox="0 0 10 10" style={{ transform: open ? 'rotate(90deg)' : 'none', transition: 'transform .1s' }}>
      <path d="M3 1l4 4-4 4" fill="none" stroke="currentColor" strokeWidth="1.4" strokeLinecap="round" strokeLinejoin="round" />
    </svg>
  );
}

export function ArrowDown() {
  return (
    <svg width="10" height="10" viewBox="0 0 10 10">
      <path d="M5 1v7m0 0L2 5m3 3l3-3" fill="none" stroke="currentColor" strokeWidth="1.4" strokeLinecap="round" strokeLinejoin="round" />
    </svg>
  );
}

export function ArrowUp() {
  return (
    <svg width="10" height="10" viewBox="0 0 10 10">
      <path d="M5 9V2m0 0L2 5m3-3l3 3" fill="none" stroke="currentColor" strokeWidth="1.4" strokeLinecap="round" strokeLinejoin="round" />
    </svg>
  );
}

export function Collapse() {
  return (
    <svg width="10" height="10" viewBox="0 0 10 10">
      <path d="M1 3h8M1 5h8M1 7h8" fill="none" stroke="currentColor" strokeWidth="1.2" strokeLinecap="round" />
    </svg>
  );
}

export function Check() {
  return (
    <svg width="10" height="10" viewBox="0 0 10 10">
      <path d="M1.5 5.2l2.4 2.4L8.5 2.4" fill="none" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" strokeLinejoin="round" />
    </svg>
  );
}
