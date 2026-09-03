import {
  forwardRef,
  type HTMLAttributes,
  type ReactNode,
} from "react";

import { cx } from "./class-names";

export type FeedbackTone = "neutral" | "success" | "warning" | "error";

export interface SpinnerProps extends HTMLAttributes<HTMLSpanElement> {
  label?: string;
  size?: "sm" | "md";
}

export const Spinner = forwardRef<HTMLSpanElement, SpinnerProps>(
  function Spinner(
    { className, label = "Loading", size = "md", ...props },
    ref,
  ) {
    return (
      <span
        ref={ref}
        role="status"
        className={cx("inline-flex items-center justify-center", className)}
        {...props}
      >
        <span
          aria-hidden="true"
          className={cx(
            "animate-spin rounded-full border-2 border-accent border-r-transparent motion-reduce:animate-none",
            size === "sm" ? "size-4" : "size-5",
          )}
        />
        <span className="sr-only">{label}</span>
      </span>
    );
  },
);

export const Skeleton = forwardRef<HTMLDivElement, HTMLAttributes<HTMLDivElement>>(
  function Skeleton({ className, ...props }, ref) {
    return (
      <div
        ref={ref}
        aria-hidden="true"
        className={cx(
          "animate-pulse rounded-control bg-surface-2 motion-reduce:animate-none",
          className,
        )}
        {...props}
      />
    );
  },
);

export interface InlineNoticeProps
  extends Omit<HTMLAttributes<HTMLDivElement>, "title"> {
  title?: ReactNode;
  tone?: FeedbackTone;
}

const noticeClasses: Record<FeedbackTone, string> = {
  neutral: "border-line bg-surface text-ink-2",
  success: "border-accent/25 bg-accent-soft text-accent",
  warning: "border-warn/25 bg-warn-bg text-warn",
  error: "border-warn/25 bg-warn-bg text-warn",
};

const noticeDotClasses: Record<FeedbackTone, string> = {
  neutral: "bg-ink-3",
  success: "bg-accent",
  warning: "bg-warn",
  error: "bg-warn",
};

export const InlineNotice = forwardRef<HTMLDivElement, InlineNoticeProps>(
  function InlineNotice(
    { children, className, role, title, tone = "neutral", ...props },
    ref,
  ) {
    return (
      <div
        ref={ref}
        role={role ?? (tone === "error" ? "alert" : "status")}
        className={cx(
          "flex items-start gap-3 rounded-control border px-4 py-3 text-body",
          noticeClasses[tone],
          className,
        )}
        {...props}
      >
        <span
          aria-hidden="true"
          className={cx(
            "mt-2 size-1 shrink-0 rounded-full",
            noticeDotClasses[tone],
          )}
        />
        <div className="min-w-0">
          {title ? <p className="font-semibold text-current">{title}</p> : null}
          <div className={cx(title ? "mt-1" : undefined)}>{children}</div>
        </div>
      </div>
    );
  },
);

export interface EmptyStateProps
  extends Omit<HTMLAttributes<HTMLElement>, "title"> {
  action?: ReactNode;
  description?: ReactNode;
  title: ReactNode;
}

export const EmptyState = forwardRef<HTMLElement, EmptyStateProps>(
  function EmptyState(
    { action, className, description, title, ...props },
    ref,
  ) {
    return (
      <section
        ref={ref}
        className={cx(
          "flex min-h-48 flex-col items-center justify-center rounded-modal border border-dashed border-line bg-surface/45 px-6 py-8 text-center",
          className,
        )}
        {...props}
      >
        <span
          aria-hidden="true"
          className="mb-4 grid size-8 place-items-center rounded-full border border-line bg-ground font-mono text-sm text-ink-3"
        >
          —
        </span>
        <h2 className="text-body font-semibold text-ink">{title}</h2>
        {description ? (
          <p className="mt-2 max-w-md text-body text-ink-3">{description}</p>
        ) : null}
        {action ? <div className="mt-4">{action}</div> : null}
      </section>
    );
  },
);
