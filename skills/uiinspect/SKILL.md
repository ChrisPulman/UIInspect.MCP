---
name: uiinspect
description: Use UIInspect MCP for consent-gated semantic inspection and operation of Windows WPF, WinForms, WinUI, Avalonia, or MAUI applications through UI Automation, including window discovery, secure session attachment, control-tree inspection, semantic actions, and unattended test workflows.
---

# UIInspect MCP

Prefer semantic UI Automation over pixel coordinates. Keep one MCP server process alive for the complete automation run so its consent decision and attached sessions remain available.

## Workflow

1. Call `uiinspect_discover_windows`.
2. Match the intended PID and optional HWND using independently known application context. Treat a restarted process as a different target even when its PID is reused.
3. Decide the complete capability set before requesting consent:
   - Inspection only: set `allowActions` and `allowKeyboard` to `false`.
   - Semantic actions: set `allowActions` to `true`.
   - Logical keys: also set `allowKeyboard` to `true`.
4. Call `uiinspect_request_consent` once for that exact process with the complete capability set. Wait for the trusted Windows dialog to resolve.
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

For unattended tests, start one server process, request every required capability during setup, approve the single dialog, and reuse that server process for the complete test run.

## Result handling

- On `stale_element`, inspect again and use the new reference.
- On `consent_expired`, request consent again, attach a new UIA session, and inspect again. The retained server-session approval renews the grant without another dialog, but the expired attached session has been removed.
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
