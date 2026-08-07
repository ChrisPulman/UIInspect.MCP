---
name: uiinspect
description: Use UIInspect MCP for consent-gated semantic inspection and operation of Windows WPF, WinForms, WinUI, Avalonia, or MAUI applications through UI Automation, including window discovery, secure session attachment, control-tree inspection, semantic actions, and unattended test workflows.
---

# UIInspect MCP

Prefer semantic UI Automation over pixel coordinates. Keep each attached session with the MCP server process that created it. A user-activated unattended approval can be shared across multiple local MCP server and agent processes in the same Windows sign-in session.

## Workflow

1. When unattended approval is expected, call `uiinspect_get_unattended_approval`, then call `uiinspect_discover_windows`.
2. Match the intended PID and optional HWND using independently known application context. Treat a restarted process as a different target even when its PID is reused.
3. Decide the complete capability set before requesting consent:
   - Inspection only: set `allowActions` and `allowKeyboard` to `false`.
   - Semantic actions: set `allowActions` to `true`.
   - Logical keys: also set `allowKeyboard` to `true`.
4. Call `uiinspect_request_consent` once for that exact process with the complete capability set. An active unattended lease is exchanged automatically without a per-target dialog; otherwise wait for the trusted Windows dialog to resolve.
5. Call `uiinspect_attach`.
6. Call `uiinspect_inspect_tree` with the smallest useful `maxDepth` and `maxNodes`.
7. Select an element by automation ID, control type, name, supported patterns, and semantic path. Use its opaque `elementReference`.
8. Prefer `uiinspect_invoke`, `uiinspect_set_value` (or its `uiinspect_set_text` alias), `uiinspect_select_item`, or `uiinspect_expand_collapse`. Use `uiinspect_click` only when the semantic provider lacks InvokePattern. Use `uiinspect_send_key` only for an allowlisted logical key and only when keyboard consent was granted.
9. After every successful action, inspect again; references are intentionally invalidated.
10. Call `uiinspect_close_session` when finished.

## Consent lifecycle

- The trusted dialog appears at most once per local MCP client and exact process identity during one MCP server process lifetime.
- Concurrent and repeated consent calls share the same in-flight or completed decision. Cancelling one request only stops that caller's wait; it does not close the native dialog. A retry joins the same decision and cannot open another dialog.
- Approval and denial are retained until the MCP server exits. Closing an attached UIA session or allowing a short-lived grant to expire does not cause another prompt; an approved decision can issue a fresh grant.
- The first request establishes the maximum capability set for that target. Same-scope and subset requests reuse it. Capability expansion returns `consent_denied` without another prompt; restart the MCP server only when a larger capability set is genuinely required.
- A new exact process identity or a different MCP client receives an independent decision.
- New-dialog rate limits are charged only when a new prompt would be shown. Cached decisions do not consume another permit.
- The native prompt closes after its bounded timeout or when the exact target exits. A later request may open a clean prompt after a timeout.

## Unattended and multi-agent tests

- The MCP transport cannot authenticate a caller merely because it claims to be Codex, Claude Code, or another agent. Never treat an agent name as approval.
- Ask the user to run `uiinspect-mcp --authorize-unattended <hours>` in a trusted interactive terminal and approve the Windows dialog. Valid values are exactly `1`, `2`, `5`, `8`, `12`, and `24`.
- Confirm activation with `uiinspect_get_unattended_approval`. This status tool cannot create, extend, or revoke a lease.
- Multiple agent sessions can use the same lease. Each must independently discover, request consent for the exact target and full capability set, attach, inspect, and later close its own session.
- The user can inspect or revoke the window with `uiinspect-mcp --unattended-status` and `uiinspect-mcp --revoke-unattended`. Do not attempt to invoke private broker mode directly.
- A lease exists only in broker memory and ends on expiry, revocation, broker exit, logoff, or reboot. Every protected operation is revalidated, so an ended lease fails closed.

## Result handling

- On `stale_element`, inspect again and use the new reference.
- On `unattended_approval_inactive`, ask the user to activate a window from a trusted terminal or continue with the normal per-target prompt. Do not loop.
- On `consent_expired`, check `uiinspect_get_unattended_approval` when unattended operation is expected, then request consent again, attach a new UIA session, and inspect again. An active broker lease or retained explicit approval renews the grant without another dialog, but the expired attached session has been removed.
- On `session_not_found`, attach again when consent is still active; otherwise request consent, attach, and inspect.
- On `target_changed` or `target_unavailable`, rediscover the application and treat the new exact process as a new target.
- On `rate_limited`, wait for `retryAfterMilliseconds` before retrying. No dialog was shown for that attempt.
- Treat `consent_denied` as terminal for that client, exact target, and server process. Do not loop.
- Treat `pattern_not_supported` as a semantic limitation. Choose another supported semantic operation; do not fall back to blind input.

## Safety

- Never automate a different process or window than the user approved.
- Never request interaction or keyboard capability for a read-only task.
- Do not infer hidden values. UIInspect intentionally omits control values and redacts password nodes.
- UIA does not expose XAML source, DataContext, bindings, dependency properties, or validation details.
