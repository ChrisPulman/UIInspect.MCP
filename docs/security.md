# Security

UIInspect.MCP treats Windows UI Automation as a privileged semantic interface. Accessibility trees can expose application structure and actions can change application state, so access is denied by default.

## Consent and identity

- Consent is requested through a trusted server-owned Windows dialog, never through target-provided text.
- The dialog is single-flight and shown at most once per client and exact process instance during one server session. Concurrent or repeated calls share the first terminal decision, and cancellation detaches only that caller's wait.
- Denial and the initially approved capability ceiling are retained until the server session ends. Later capability expansion is rejected without displaying another dialog.
- Grants are scoped to the MCP client hash, exact PID, process creation time, executable identity, Windows session, capability set, and a short expiry.
- PID alone is never an authorization identity. The process is resolved before consent, after the prompt, after UIA attachment, and before every inspection or action.
- Grants are in memory and disappear on server restart. They are revoked when a process instance changes.
- Inspect, interact, and keyboard are separate capabilities. Read consent does not grant actions.

The MVP uses a local stdio principal. It does not listen on TCP. A future TCP/CI mode must be opt-in, use TLS/mTLS as appropriate, validate scoped short-lived signed tokens, retain only verifiers/public credentials, support revocation, and never accept tokens as MCP tool arguments.

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
