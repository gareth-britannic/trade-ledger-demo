import {
  forwardRef,
  type InputHTMLAttributes,
  type ReactNode,
} from "react";

import { cx } from "./class-names";

export interface InputProps extends InputHTMLAttributes<HTMLInputElement> {
  invalid?: boolean;
}

export const Input = forwardRef<HTMLInputElement, InputProps>(function Input(
  { className, invalid = false, ...props },
  ref,
) {
  return (
    <input
      ref={ref}
      aria-invalid={invalid || props["aria-invalid"] || undefined}
      className={cx(
        "block min-h-10 w-full rounded-control border border-line bg-ground px-3 py-2 text-body text-ink shadow-control",
        "placeholder:text-ink-3 transition-[border-color,box-shadow,background-color] duration-150",
        "hover:border-ink-3 focus:border-accent focus:outline-none focus:ring-2 focus:ring-accent/20",
        "disabled:cursor-not-allowed disabled:bg-surface disabled:text-ink-3 disabled:opacity-70",
        "aria-invalid:border-warn aria-invalid:focus:border-warn aria-invalid:focus:ring-warn/20",
        className,
      )}
      {...props}
    />
  );
});

export interface FieldProps extends Omit<InputProps, "id"> {
  containerClassName?: string;
  error?: ReactNode;
  hint?: ReactNode;
  id: string;
  label: ReactNode;
  labelClassName?: string;
  optional?: boolean;
}

export const Field = forwardRef<HTMLInputElement, FieldProps>(function Field(
  {
    className,
    containerClassName,
    error,
    hint,
    id,
    label,
    labelClassName,
    optional = false,
    required,
    ...inputProps
  },
  ref,
) {
  const hintId = hint ? `${id}-hint` : undefined;
  const errorId = error ? `${id}-error` : undefined;
  const describedBy = [inputProps["aria-describedby"], hintId, errorId]
    .filter(Boolean)
    .join(" ");

  return (
    <div className={cx("grid gap-2", containerClassName)}>
      <label
        htmlFor={id}
        className={cx(
          "font-mono text-label font-medium uppercase text-ink-2",
          labelClassName,
        )}
      >
        {label}
        {required ? (
          <span aria-hidden="true" className="ml-1 text-warn">
            *
          </span>
        ) : null}
        {optional && !required ? (
          <span className="ml-2 normal-case tracking-normal text-ink-3">
            (optional)
          </span>
        ) : null}
      </label>
      <Input
        ref={ref}
        id={id}
        required={required}
        invalid={Boolean(error)}
        aria-describedby={describedBy || undefined}
        aria-errormessage={errorId}
        className={className}
        {...inputProps}
      />
      {hint ? (
        <p id={hintId} className="text-[12px] leading-4 text-ink-3">
          {hint}
        </p>
      ) : null}
      {error ? (
        <p
          id={errorId}
          role="alert"
          className="text-[12px] font-medium leading-4 text-warn"
        >
          {error}
        </p>
      ) : null}
    </div>
  );
});
