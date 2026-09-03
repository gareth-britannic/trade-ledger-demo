import { forwardRef, type ButtonHTMLAttributes, type ReactNode } from "react";

import { cx } from "./class-names";

export type ButtonVariant = "primary" | "secondary" | "ghost";
export type ButtonSize = "sm" | "md" | "icon";

export interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  endIcon?: ReactNode;
  isLoading?: boolean;
  loadingLabel?: ReactNode;
  size?: ButtonSize;
  startIcon?: ReactNode;
  variant?: ButtonVariant;
}

const variantClasses: Record<ButtonVariant, string> = {
  primary:
    "border-accent bg-accent text-ground shadow-control hover:border-ink-2 hover:bg-ink-2",
  secondary:
    "border-line bg-ground text-ink shadow-control hover:bg-surface",
  ghost:
    "border-transparent bg-transparent text-ink-2 hover:bg-surface hover:text-ink",
};

const sizeClasses: Record<ButtonSize, string> = {
  sm: "min-h-8 px-3 py-1 text-[13px] leading-[18px]",
  md: "min-h-10 px-4 py-2 text-body",
  icon: "size-9 p-0",
};

export const Button = forwardRef<HTMLButtonElement, ButtonProps>(
  function Button(
    {
      children,
      className,
      disabled,
      endIcon,
      isLoading = false,
      loadingLabel,
      size = "md",
      startIcon,
      type = "button",
      variant = "primary",
      ...props
    },
    ref,
  ) {
    const isDisabled = disabled || isLoading;

    return (
      <button
        ref={ref}
        type={type}
        disabled={isDisabled}
        aria-busy={isLoading || undefined}
        className={cx(
          "inline-flex shrink-0 items-center justify-center gap-2 rounded-control border font-medium transition-colors duration-150",
          "focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent",
          "disabled:cursor-not-allowed disabled:opacity-55",
          variantClasses[variant],
          sizeClasses[size],
          className,
        )}
        {...props}
      >
        {isLoading ? (
          <span
            aria-hidden="true"
            className="size-4 animate-spin rounded-full border-2 border-current border-r-transparent motion-reduce:animate-none"
          />
        ) : startIcon ? (
          <span aria-hidden="true" className="flex shrink-0 items-center">
            {startIcon}
          </span>
        ) : null}
        <span>{isLoading && loadingLabel ? loadingLabel : children}</span>
        {!isLoading && endIcon ? (
          <span aria-hidden="true" className="flex shrink-0 items-center">
            {endIcon}
          </span>
        ) : null}
      </button>
    );
  },
);
