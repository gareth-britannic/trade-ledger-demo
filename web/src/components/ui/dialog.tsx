import * as DialogPrimitive from "@radix-ui/react-dialog";
import {
  forwardRef,
  type ComponentPropsWithoutRef,
  type HTMLAttributes,
  type ReactElement,
  type ReactNode,
  useState,
} from "react";

import { cx } from "./class-names";
import { useRestoreFocus } from "./use-restore-focus";

export const DialogHeader = forwardRef<
  HTMLDivElement,
  HTMLAttributes<HTMLDivElement>
>(function DialogHeader({ className, ...props }, ref) {
  return (
    <div ref={ref} className={cx("grid gap-2 pr-8", className)} {...props} />
  );
});

export const DialogFooter = forwardRef<
  HTMLDivElement,
  HTMLAttributes<HTMLDivElement>
>(function DialogFooter({ className, ...props }, ref) {
  return (
    <div
      ref={ref}
      className={cx(
        "mt-6 flex flex-col-reverse gap-2 sm:flex-row sm:justify-end",
        className,
      )}
      {...props}
    />
  );
});

type DialogContentProps = ComponentPropsWithoutRef<
  typeof DialogPrimitive.Content
>;

export type DialogProps = Omit<
  ComponentPropsWithoutRef<typeof DialogPrimitive.Root>,
  "children"
> & {
  children: ReactNode;
  className?: string;
  closeLabel?: string;
  contentProps?: Omit<DialogContentProps, "children" | "className">;
  description?: ReactNode;
  footer?: ReactNode;
  title: ReactNode;
  trigger?: ReactElement;
};

export function Dialog({
  children,
  className,
  closeLabel = "Close dialog",
  contentProps,
  defaultOpen,
  description,
  footer,
  onOpenChange,
  open,
  title,
  trigger,
  ...rootProps
}: DialogProps) {
  const [internalOpen, setInternalOpen] = useState(defaultOpen ?? false);
  const restoreFocus = useRestoreFocus(open ?? internalOpen);
  const { onCloseAutoFocus, ...remainingContentProps } = contentProps ?? {};
  const openProps =
    open !== undefined
      ? { open }
      : defaultOpen !== undefined
        ? { defaultOpen }
        : {};
  const descriptionProps = description
    ? {}
    : ({ "aria-describedby": undefined } satisfies Pick<
        DialogContentProps,
        "aria-describedby"
      >);

  return (
    <DialogPrimitive.Root
      {...rootProps}
      {...openProps}
      onOpenChange={(nextOpen) => {
        setInternalOpen(nextOpen);
        onOpenChange?.(nextOpen);
      }}
    >
      {trigger ? (
        <DialogPrimitive.Trigger asChild>{trigger}</DialogPrimitive.Trigger>
      ) : null}
      <DialogPrimitive.Portal>
        <DialogPrimitive.Overlay className="fixed inset-0 z-50 bg-scrim/16 backdrop-blur-[1px] data-[state=closed]:animate-overlay-out data-[state=open]:animate-overlay-in" />
        <DialogPrimitive.Content
          {...descriptionProps}
          {...remainingContentProps}
          onCloseAutoFocus={(event) => {
            onCloseAutoFocus?.(event);
            restoreFocus(event);
          }}
          className={cx(
            "fixed left-1/2 top-1/2 z-50 w-[calc(100%-2rem)] max-w-[34rem] -translate-x-1/2 -translate-y-1/2",
            "max-h-[calc(100dvh-2rem)] overflow-y-auto rounded-modal border border-line bg-ground p-6 text-ink shadow-panel outline-none",
            "data-[state=closed]:animate-dialog-out data-[state=open]:animate-dialog-in",
            className,
          )}
        >
          <DialogHeader>
            <DialogPrimitive.Title className="text-[18px] font-semibold leading-6 text-ink">
              {title}
            </DialogPrimitive.Title>
            {description ? (
              <DialogPrimitive.Description className="text-body text-ink-3">
                {description}
              </DialogPrimitive.Description>
            ) : null}
          </DialogHeader>
          <DialogPrimitive.Close
            type="button"
            aria-label={closeLabel}
            className="absolute right-4 top-4 grid size-8 place-items-center rounded-control text-ink-3 transition-colors hover:bg-surface hover:text-ink focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
          >
            <span aria-hidden="true" className="text-[20px] leading-none">
              ×
            </span>
          </DialogPrimitive.Close>
          <div className="mt-6">{children}</div>
          {footer ? <DialogFooter>{footer}</DialogFooter> : null}
        </DialogPrimitive.Content>
      </DialogPrimitive.Portal>
    </DialogPrimitive.Root>
  );
}
