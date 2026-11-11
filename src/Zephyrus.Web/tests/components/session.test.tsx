import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { AuthGate } from "@/components/AuthGate";
import { SessionBar } from "@/components/SessionBar";
import { api, ApiError } from "@/lib/api";
import { getTeamToken, setTeamToken } from "@/lib/auth";
import { pm } from "../fixtures";

vi.mock("@/lib/api", async (importOriginal) => ({
  ApiError: (await importOriginal<typeof import("@/lib/api")>()).ApiError,
  api: { getCurrentUser: vi.fn() },
}));

const getCurrentUser = vi.mocked(api.getCurrentUser);
const reload = vi.fn();

beforeEach(() => {
  getCurrentUser.mockReset();
  reload.mockReset();
  vi.stubGlobal("location", { ...window.location, reload });
});

describe("AuthGate", () => {
  it("shows the sign-in prompt when no token is stored", async () => {
    render(<AuthGate>secret page</AuthGate>);

    expect(await screen.findByText("Sign in to Zephyrus")).toBeInTheDocument();
    expect(screen.queryByText("secret page")).not.toBeInTheDocument();
  });

  it("shows the page when a token is stored", async () => {
    setTeamToken("demo-pm");
    render(<AuthGate>secret page</AuthGate>);

    expect(await screen.findByText("secret page")).toBeInTheDocument();
  });
});

describe("SessionBar", () => {
  it("shows who is signed in", async () => {
    setTeamToken("demo-pm");
    getCurrentUser.mockResolvedValue(pm);

    render(<SessionBar />);

    expect(await screen.findByText("Pat")).toBeInTheDocument();
  });

  it("drops a stored token the API no longer accepts", async () => {
    setTeamToken("revoked");
    getCurrentUser.mockRejectedValue(new Error("401"));

    render(<SessionBar />);

    expect(await screen.findByPlaceholderText("Team token")).toBeInTheDocument();
    expect(getTeamToken()).toBeNull();
  });

  it("asks for a token before signing in", async () => {
    render(<SessionBar />);

    await userEvent.click(await screen.findByRole("button", { name: "Sign in" }));

    expect(screen.getByText("Token required.")).toBeInTheDocument();
    expect(getCurrentUser).not.toHaveBeenCalled();
  });

  it("keeps a recognised token and reloads the page", async () => {
    getCurrentUser.mockResolvedValue(pm);
    render(<SessionBar />);

    await userEvent.type(await screen.findByLabelText("Team token"), "  demo-pm  ");
    await userEvent.click(screen.getByRole("button", { name: "Sign in" }));

    expect(getTeamToken()).toBe("demo-pm");
    expect(reload).toHaveBeenCalled();
  });

  it("rejects an unrecognised token without keeping it", async () => {
    getCurrentUser.mockRejectedValue(new Error("401"));
    render(<SessionBar />);

    await userEvent.type(await screen.findByLabelText("Team token"), "wrong");
    await userEvent.click(screen.getByRole("button", { name: "Sign in" }));

    expect(await screen.findByText("Token not recognised.")).toBeInTheDocument();
    expect(getTeamToken()).toBeNull();
    expect(reload).not.toHaveBeenCalled();
  });

  it("tells a client blocked for too many wrong tokens to wait", async () => {
    getCurrentUser.mockRejectedValue(new ApiError(429, "Too many failed sign-ins. Try again later."));
    render(<SessionBar />);

    await userEvent.type(await screen.findByLabelText("Team token"), "guess");
    await userEvent.click(screen.getByRole("button", { name: "Sign in" }));

    expect(await screen.findByText("Too many failed sign-ins. Try again later.")).toBeInTheDocument();
  });

  it("signs out by clearing the token", async () => {
    setTeamToken("demo-pm");
    getCurrentUser.mockResolvedValue(pm);
    render(<SessionBar />);

    await userEvent.click(await screen.findByRole("button", { name: "Sign out" }));

    expect(getTeamToken()).toBeNull();
    expect(reload).toHaveBeenCalled();
  });
});
