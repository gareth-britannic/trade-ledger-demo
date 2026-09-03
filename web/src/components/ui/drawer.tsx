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

export const DrawerHeader = forwardRef<
  HTMLDivElement,
  HTMLAttributes<HTMLDivElement>
>(function DrawerHeader({ className, ...props }, ref) {
  return (
    <div
      ref={ref}
      className={cx("border-b border-line px-6 py-4 pr-14", className)}
      {...props}
    />
  );
});

export const DrawerFooter = forwardRef<
  HTMLDivElement,
  HTMLAttributes<HTMLDivElement>
>(function DrawerFooter({ className, ...props }, ref) {
  return (
    <div
      ref={ref}
      className={cx(
        "flex flex-col-reverse gap-2 border-t border-line px-6 py-4 sm:flex-row sm:justify-end",
        className,
      )}
      {...props}
    />
  );
});

type DrawerContentProps = ComponentPropsWithoutRef<
  typeof DialogPrimitive.Content
>;

export type DrawerProps = Omit<
  ComponentPropsWithoutRef<typeof DialogPrimitive.Root>,
  "children"
> & {
  children: ReactNode;
  className?: string;
  closeLabel?: string;
  contentProps?: Omit<DrawerContentProps, "children" | "className">;
  description?: ReactNode;
  footer?: ReactNode;
  title: ReactNode;
  trigger?: ReactElement;
};

export function Drawer({
  children,
  className,
  closeLabel = "Close drawer",
  contentProps,
  defaultOpen,
  description,
  footer,
  onOpenChange,
  open,
  title,
  trigger,
  ...rootProps
}: DrawerProps) {
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
        DrawerContentProps,
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
            "fixed inset-y-0 right-0 z-50 flex h-dvh w-full max-w-[30rem] flex-col border-l border-line bg-ground text-ink shadow-panel outline-none",
            "data-[state=closed]:animate-drawer-out data-[state=open]:animate-drawer-in",
            className,
          )}
        >
          <DrawerHeader>
            <DialogPrimitive.Title className="text-[18px] font-semibold leading-6 text-ink">
              {title}
            </DialogPrimitive.Title>
            {description ? (
              <DialogPrimitive.Description className="mt-2 text-body text-ink-3">
                {description}
              </DialogPrimitive.Description>
            ) : null}
          </DrawerHeader>
          <DialogPrimitive.Close
            type="button"
            aria-label={closeLabel}
            className="absolute right-4 top-3 grid size-8 place-items-center rounded-control text-ink-3 transition-colors hover:bg-surface hover:text-ink focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
          >
            <span aria-hidden="true" className="text-[20px] leading-none">
              ×
            </span>
          </DialogPrimitive.Close>
          <div className="min-h-0 flex-1 overflow-y-auto px-6 py-6">
            {children}
          </div>
          {footer ? <DrawerFooter>{footer}</DrawerFooter> : null}
        </DialogPrimitive.Content>
      </DialogPrimitive.Portal>
    </DialogPrimitive.Root>
  );
}
