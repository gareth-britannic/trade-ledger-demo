import { forwardRef, type HTMLAttributes } from "react";

import { cx } from "./class-names";

export type BadgeTone =
  | "neutral"
  | "accent"
  | "positive"
  | "warning"
  | "danger";
export type BadgeVariant = "soft" | "outline";

export interface BadgeProps extends HTMLAttributes<HTMLSpanElement> {
  dot?: boolean;
  tone?: BadgeTone;
  variant?: BadgeVariant;
}

const toneClasses: Record<BadgeTone, Record<BadgeVariant, string>> = {
  neutral: {
    soft: "border-surface-2 bg-surface text-ink-2",
    outline: "border-line bg-transparent text-ink-2",
  },
  accent: {
    soft: "border-accent-soft bg-accent-soft text-accent",
    outline: "border-accent/35 bg-transparent text-accent",
  },
  positive: {
    soft: "border-accent-soft bg-accent-soft text-accent",
    outline: "border-accent/35 bg-transparent text-accent",
  },
  warning: {
    soft: "border-warn-bg bg-warn-bg text-warn",
    outline: "border-warn/35 bg-transparent text-warn",
  },
  danger: {
    soft: "border-warn-bg bg-warn-bg text-warn",
    outline: "border-warn/35 bg-transparent text-warn",
  },
};

const dotClasses: Record<BadgeTone, string> = {
  neutral: "bg-ink-3",
  accent: "bg-accent",
  positive: "bg-accent",
  warning: "bg-warn",
  danger: "bg-warn",
};

export const Badge = forwardRef<HTMLSpanElement, BadgeProps>(function Badge(
  {
    children,
    className,
    dot = false,
    tone = "neutral",
    variant = "soft",
    ...props
  },
  ref,
) {
  return (
    <span
      ref={ref}
      className={cx(
        "inline-flex min-h-6 items-center justify-center gap-1 rounded-full border px-2 py-1",
        "font-mono text-[11px] font-medium leading-[14px] tracking-[0.04em]",
        toneClasses[tone][variant],
        className,
      )}
      {...props}
    >
      {dot ? (
        <span
          aria-hidden="true"
          className={cx("size-1 rounded-full", dotClasses[tone])}
        />
      ) : null}
      {children}
    </span>
  );
});
