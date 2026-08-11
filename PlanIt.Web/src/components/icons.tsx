// Journey-metaphor icon set for visually distinguishing the three hierarchy
// levels: Project = the whole map, Feature = a signpost/waypoint along the
// way, Task = a single footprint/step.

interface IconProps {
  size?: number;
  className?: string;
}

export function MapIcon({ size = 16, className }: IconProps) {
  return (
    <svg
      width={size}
      height={size}
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="2"
      strokeLinecap="round"
      strokeLinejoin="round"
      className={className}
      aria-hidden="true"
    >
      <polygon points="3 6 9 3 15 6 21 3 21 18 15 21 9 18 3 21" />
      <line x1="9" y1="3" x2="9" y2="18" />
      <line x1="15" y1="6" x2="15" y2="21" />
    </svg>
  );
}

export function SignpostIcon({ size = 16, className }: IconProps) {
  return (
    <svg
      width={size}
      height={size}
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="2"
      strokeLinecap="round"
      strokeLinejoin="round"
      className={className}
      aria-hidden="true"
    >
      <line x1="12" y1="20" x2="12" y2="4" />
      <path d="M12 5h7l-2 2.5L19 10h-7" />
      <path d="M12 10h-6l2 2.5L6 15h6" />
    </svg>
  );
}

export function HikingShoeIcon({ size = 16, className }: IconProps) {
  return (
    <svg
      width={size}
      height={size}
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="2"
      strokeLinecap="round"
      strokeLinejoin="round"
      className={className}
      aria-hidden="true"
    >
      <path d="M4 17c0-2 .5-3 2-4l3-2V7a2 2 0 0 1 2-2h1.5c.5 1.2 1.5 2 3 2.3l4 .9a2 2 0 0 1 1.5 2v3.3c0 1.4-.9 2.6-2.3 2.9L15 17H4z" />
      <path d="M4 17v1.5A1.5 1.5 0 0 0 5.5 20h13a1.5 1.5 0 0 0 1.5-1.5V17" />
      <path d="M9 11h4" />
    </svg>
  );
}

export function WorkItemTypeIcon({ type, size, className }: { type: "Feature" | "Task"; size?: number; className?: string }) {
  return type === "Feature" ? <SignpostIcon size={size} className={className} /> : <HikingShoeIcon size={size} className={className} />;
}
