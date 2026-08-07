# Security

UIInspect.MCP treats Windows UI Automation as a privileged semantic interface. Accessibility trees can expose application structure and actions can change application state, so access is denied by default.

## Consent and identity

- Consent is requested through a trusted server-owned Windows dialog, never through target-provided text.
- The dialog is single-flight and shown at most once per client and exact process instance during one server session. Concurrent or repeated calls share the first terminal decision, and cancellation detaches only that caller's wait.
- The dialog closes and fails closed after a bounded timeout or when the exact target process exits. A timeout releases the decision key so a later request can open a clean prompt instead of inheriting a stale one.
- Denial and the initially approved capability ceiling are retained until the server session ends. Later capability expansion is rejected without displaying another dialog.
- Grants are scoped to the MCP client hash, exact PID, process creation time, executable identity, Windows session, capability set, and a short expiry.
- PID alone is never an authorization identity. The process is resolved before consent, after the prompt, after UIA attachment, and before every inspection or action.
- Grants are in memory and disappear on server restart. They are revoked when a process instance changes.
- Inspect, interact, and keyboard are separate capabilities. Read consent does not grant actions.

The MCP stdio transport is a subprocess pipe, not an authenticated agent identity. Client names, initialization metadata, and request metadata are self-asserted, so a caller claiming to be Codex, Claude Code, or another agent is never allowed to bypass user consent. A future remote agent-identity mode must be opt-in, use authenticated HTTP with OAuth or mTLS as appropriate, validate scoped short-lived credentials, retain only verifiers/public credentials, support revocation, and never accept bearer credentials as MCP tool arguments.

## Unattended approval broker

- A local user can activate exactly a 1, 2, 5, 8, 12, or 24 hour lease from the trusted `uiinspect-mcp` manager command. Activation and lease extension are not exposed as MCP tools.
- The approval dialog states that every local UIInspect client in the current Windows sign-in session receives Inspect, Interact, and Keyboard capability until the displayed expiry. Denial is the default button.
- The lease is held only in broker memory. There is no reusable token file and no restoration after broker exit, logoff, or reboot.
- Broker object names are scoped by a SHA-256 derivative of the current Windows SID plus the interactive session ID. The pipe uses `PipeOptions.CurrentUserOnly`, and a process-lifetime kernel marker prevents multiple authorities from owning the same scope without holding a thread-affine lock across asynchronous work.
- Multiple listeners allow independent Codex, Claude Code, and other local MCP server processes to validate concurrently. Every grant, attach, inspection, and action contacts the broker again; revocation or broker loss therefore fails closed and disposes affected automation sessions.
- The broker independently resolves the exact PID, creation time, executable identity, and Windows session on every validation. A process restart or PID reuse cannot inherit approval.
- Anyone able to run code as the same Windows user is inside this approval boundary and can query or revoke the lease. The broker does not defend against a fully compromised same-user account; use a dedicated test account when that boundary is too broad.
- Broker lifecycle events are written separately to `%LOCALAPPDATA%\UIInspect.MCP\audit\unattended-approval.jsonl` without target UI content.

## Least privilege

- The only automation backend is out-of-process UIA3.
- No injection, remote assembly load, arbitrary reflection, process launch, shell execution, OCR, clipboard read, screenshots, or arbitrary Win32 messages are exposed.
- Value changes use `ValuePattern`; selection, expansion, and invocation use their semantic UIA patterns.
- Logical keys are allowlisted. Windows/system modifier chords are not exposed.
- Click operates only on a semantic reference resolved under the consented top-level window.
- Tree depth and node count are bounded.

## Sensitive data

- Inspection never reads ValuePattern values.
- Password nodes are flagged and their accessible names are redacted.
- Audit records contain hashed client identity, PID, process start time, operation, outcome, and safe reason code.
- Audit records never contain entered values, UI text, password content, keystrokes beyond the logical key name, bearer tokens, screenshots, clipboard data, or raw provider exceptions.
- The JSONL audit sink is append-only at the application level. Deployments should additionally apply an ACL allowing only the server account and administrators.

## Rate limits

Defaults:

- Discovery: 30/minute.
- New consent dialogs: 3/minute per client. Cached decisions do not consume permits, and the native prompt itself appears at most once per client and exact process instance in a server session.
- Inspection: 60/minute.
- Actions: 10/minute.

Rate-limit responses include a retry delay. Inspection and action limits are separate so large read loops cannot hide action bursts.

## Windows boundaries

UIA normally requires the server and target to share an interactive Windows session. A non-elevated server cannot automate higher-integrity/elevated targets. Secure desktop and protected processes remain unavailable. Do not run the server elevated merely to bypass this boundary; start it at the least privilege required for the intended target.

Custom-drawn controls, virtualization, XAML islands, popups, and framework accessibility-provider differences can produce incomplete or changing trees. An absent semantic pattern is returned as `pattern_not_supported`; the server does not silently downgrade to unrelated input.

## Future in-process mode

Deep XAML, dependency-property, binding, validation, DataContext, command, and hot-reload diagnostics require a separately versioned in-process agent. That mode must remain disabled by default, require developer mode and a distinct UAC/user approval, authenticate its gRPC channel, restrict reflection to explicit allowlists, and keep a separate audit/capability boundary from UIA-only access.
