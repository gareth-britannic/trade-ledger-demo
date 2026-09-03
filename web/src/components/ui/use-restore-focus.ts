import { useEffect, useRef } from "react";

export function useRestoreFocus(open: boolean) {
  const openRef = useRef(open);
  const lastFocusedOutside = useRef<HTMLElement | null>(null);

  useEffect(() => {
    openRef.current = open;
  }, [open]);

  useEffect(() => {
    const rememberFocus = (event: FocusEvent) => {
      if (
        !openRef.current &&
        event.target instanceof HTMLElement &&
        event.target !== document.body
      ) {
        lastFocusedOutside.current = event.target;
      }
    };

    document.addEventListener("focusin", rememberFocus);
    return () => document.removeEventListener("focusin", rememberFocus);
  }, []);

  return (event: Event) => {
    const target = lastFocusedOutside.current;
    if (!event.defaultPrevented && target?.isConnected) {
      event.preventDefault();
      target.focus();
    }
  };
}
