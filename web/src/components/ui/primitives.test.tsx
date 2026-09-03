import "@testing-library/jest-dom/vitest";

import {
  cleanup,
  render,
  screen,
  waitFor,
  within,
} from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { createRef, useState } from "react";
import { afterEach, describe, expect, it, vi } from "vitest";

import { Badge } from "./badge";
import { Button } from "./button";
import { Dialog } from "./dialog";
import { Drawer } from "./drawer";
import { EmptyState, InlineNotice, Skeleton, Spinner } from "./feedback";
import { Field, Input } from "./input";
import {
  Table,
  TableBody,
  TableCaption,
  TableCell,
  TableContainer,
  TableHead,
  TableHeader,
  TableRow,
} from "./table";

afterEach(() => {
  cleanup();
});

describe("Button", () => {
  it("forwards native props and defaults to a non-submitting button", async () => {
    const user = userEvent.setup();
    const onClick = vi.fn();
    const ref = createRef<HTMLButtonElement>();

    render(
      <Button
        ref={ref}
        aria-label="Add fill"
        data-testid="add-fill"
        onClick={onClick}
        startIcon="+"
      >
        Add fill
      </Button>,
    );

    const button = screen.getByTestId("add-fill");
    expect(button).toHaveAttribute("type", "button");
    expect(ref.current).toBe(button);
    expect(button.querySelector("[aria-hidden='true']")).toHaveTextContent("+");

    await user.click(button);
    expect(onClick).toHaveBeenCalledOnce();
  });

  it("exposes a disabled busy state with a CSS loading indicator", () => {
    const { container } = render(
      <Button isLoading loadingLabel="Saving…" variant="secondary">
        Save
      </Button>,
    );

    const button = screen.getByRole("button", { name: "Saving…" });
    expect(button).toBeDisabled();
    expect(button).toHaveAttribute("aria-busy", "true");
    expect(container.querySelector("svg")).not.toBeInTheDocument();
  });
});

describe("form fields", () => {
  it("associates a label and hint while preserving native input props", () => {
    const ref = createRef<HTMLInputElement>();

    render(
      <Field
        ref={ref}
        autoComplete="username"
        hint="Use the bootstrapped email."
        id="email"
        label="Email"
        name="email"
        optional
      />,
    );

    const input = screen.getByRole("textbox", { name: /Email/ });
    expect(input).toHaveAttribute("autocomplete", "username");
    expect(input).toHaveAccessibleDescription("Use the bootstrapped email.");
    expect(ref.current).toBe(input);
    expect(screen.getByText("(optional)")).toBeInTheDocument();
  });

  it("announces an error and marks required input invalid", () => {
    render(
      <Field
        error="Enter a quantity greater than zero."
        id="quantity"
        label="Quantity"
        required
      />,
    );

    const input = screen.getByRole("textbox", { name: /Quantity/ });
    const error = screen.getByRole("alert");
    expect(input).toBeRequired();
    expect(input).toHaveAttribute("aria-invalid", "true");
    expect(input).toHaveAttribute("aria-errormessage", error.id);
  });

  it("supports a standalone invalid input", () => {
    render(<Input aria-label="Price" invalid type="number" step="any" />);
    expect(screen.getByRole("spinbutton", { name: "Price" })).toHaveAttribute(
      "aria-invalid",
      "true",
    );
  });
});

describe("data and feedback primitives", () => {
  it("renders semantic table elements and forwards their native attributes", () => {
    render(
      <TableContainer data-testid="table-container">
        <Table aria-label="Positions">
          <TableCaption>Current positions</TableCaption>
          <TableHeader>
            <TableRow>
              <TableHead abbr="Instrument">Symbol</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            <TableRow>
              <TableCell>AAPL</TableCell>
            </TableRow>
          </TableBody>
        </Table>
      </TableContainer>,
    );

    expect(screen.getByRole("table", { name: "Positions" })).toBeInTheDocument();
    expect(screen.getByText("Current positions").tagName).toBe("CAPTION");
    expect(screen.getByRole("columnheader")).toHaveAttribute("scope", "col");
    expect(screen.getByRole("cell")).toHaveTextContent("AAPL");
    expect(screen.getByTestId("table-container")).toHaveClass("overflow-x-auto");
  });

  it("renders visible, text-labelled statuses and loading placeholders", () => {
    render(
      <>
        <Badge dot tone="positive">
          Accepted
        </Badge>
        <Badge tone="warning" variant="outline">
          Delayed
        </Badge>
        <Spinner label="Loading lots" size="sm" />
        <Skeleton className="h-4" data-testid="skeleton" />
      </>,
    );

    expect(screen.getByText("Accepted")).toBeVisible();
    expect(screen.getByText("Delayed")).toBeVisible();
    expect(screen.getByRole("status")).toHaveTextContent("Loading lots");
    expect(screen.getByTestId("skeleton")).toHaveAttribute("aria-hidden", "true");
  });

  it("gives notices and empty states useful semantics", () => {
    render(
      <>
        <InlineNotice title="Queued" tone="success">
          The processor will apply this fill asynchronously.
        </InlineNotice>
        <InlineNotice tone="error">Could not load positions.</InlineNotice>
        <EmptyState
          action={<Button>Refresh</Button>}
          description="Add a fill to begin."
          title="No positions"
        />
      </>,
    );

    expect(screen.getByRole("status")).toHaveTextContent("Queued");
    expect(screen.getByRole("alert")).toHaveTextContent("Could not load positions.");
    expect(screen.getByRole("heading", { name: "No positions" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Refresh" })).toBeInTheDocument();
  });
});

describe("overlays", () => {
  function ControlledDialogHarness() {
    const [open, setOpen] = useState(false);

    return (
      <>
        <Button onClick={() => setOpen(true)}>External trigger</Button>
        <Dialog
          description="Controlled dialog description."
          onOpenChange={setOpen}
          open={open}
          title="Controlled dialog"
        >
          <Input aria-label="Controlled field" />
        </Dialog>
      </>
    );
  }

  it("labels the dialog, traps keyboard focus, closes with Escape, and restores focus", async () => {
    const user = userEvent.setup();

    render(
      <Dialog
        description="Enter an executed fill."
        footer={<Button>Queue fill</Button>}
        title="Add fill"
        trigger={<Button>Open fill form</Button>}
      >
        <Input aria-label="Symbol" />
      </Dialog>,
    );

    const trigger = screen.getByRole("button", { name: "Open fill form" });
    await user.click(trigger);

    const dialog = screen.getByRole("dialog", { name: "Add fill" });
    expect(dialog).toHaveAccessibleDescription("Enter an executed fill.");
    expect(dialog).toContainElement(document.activeElement as HTMLElement);

    await user.tab();
    expect(dialog).toContainElement(document.activeElement as HTMLElement);

    await user.keyboard("{Escape}");
    await waitFor(() => expect(dialog).not.toBeInTheDocument());
    expect(trigger).toHaveFocus();
  });

  it("provides an accessible close control and restores focus for the drawer", async () => {
    const user = userEvent.setup();

    render(
      <Drawer
        contentProps={{ id: "lots-drawer" }}
        description="FIFO order — oldest first"
        title="AAPL — Open lots"
        trigger={<Button>View lots</Button>}
      >
        <p>One open lot</p>
      </Drawer>,
    );

    const trigger = screen.getByRole("button", { name: "View lots" });
    await user.click(trigger);

    const drawer = screen.getByRole("dialog", { name: "AAPL — Open lots" });
    expect(drawer).toHaveAttribute("id", "lots-drawer");
    expect(drawer).toHaveAccessibleDescription("FIFO order — oldest first");

    await user.click(within(drawer).getByRole("button", { name: "Close drawer" }));
    await waitFor(() => expect(drawer).not.toBeInTheDocument());
    expect(trigger).toHaveFocus();
  });

  it("restores focus when a controlled dialog uses an external trigger", async () => {
    const user = userEvent.setup();
    render(<ControlledDialogHarness />);

    const trigger = screen.getByRole("button", { name: "External trigger" });
    await user.click(trigger);
    expect(screen.getByRole("dialog", { name: "Controlled dialog" })).toBeVisible();

    await user.keyboard("{Escape}");
    await waitFor(() =>
      expect(
        screen.queryByRole("dialog", { name: "Controlled dialog" }),
      ).not.toBeInTheDocument(),
    );
    expect(trigger).toHaveFocus();
  });
});
