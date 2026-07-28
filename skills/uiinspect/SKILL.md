---
name: uiinspect
description: Use UIInspect MCP to discover, consent to, inspect, and semantically operate Windows WPF, WinForms, WinUI, Avalonia, or MAUI Windows applications through UI Automation.
---

# UIInspect MCP

Use this skill when an agent needs semantic Windows UI Automation instead of pixel-coordinate interaction.

## Workflow

1. Call `uiinspect_discover_windows`.
2. Match the intended PID and HWND using independently known application context.
3. Call `uiinspect_request_consent` with the minimum capabilities. The local user must approve the trusted Windows prompt.
4. Call `uiinspect_attach`.
5. Call `uiinspect_inspect_tree` with the smallest useful `maxDepth` and `maxNodes`.
6. Select an element by automation ID, control type, name, patterns, and semantic path. Use its opaque `elementReference`.
7. Prefer `uiinspect_invoke`, `uiinspect_set_value` (or its `uiinspect_set_text` alias), `uiinspect_select_item`, or `uiinspect_expand_collapse`. Use `uiinspect_click` only when the semantic provider lacks InvokePattern. Use `uiinspect_send_key` only for an allowlisted logical key and only when keyboard consent was granted.
8. After every successful action, inspect again; references are intentionally invalidated.
9. Call `uiinspect_close_session`.

## Safety

- Never automate a different process or window than the user approved.
- Never request interaction or keyboard capability for a read-only task.
- Do not infer hidden values. UIInspect intentionally omits control values and redacts password nodes.
- Treat `pattern_not_supported`, `stale_element`, `target_changed`, and `consent_expired` as safe terminal results for that attempt. Re-inspect or request fresh consent as indicated; do not fall back to blind input.
- UIA does not expose XAML source, DataContext, bindings, dependency properties, or validation details in the MVP.
