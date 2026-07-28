# MVP design

## Element references

UIA `AutomationElement` and runtime IDs are not sent to clients or retained as stable global identifiers. Each inspection creates a new generation of opaque references. Internally, a reference maps to an immutable route beneath the attached top-level window:

1. Control type plus `AutomationId`, when present.
2. Otherwise control type plus accessible name.
3. Otherwise control type plus provider class.
4. An ordinal distinguishes equal siblings.

Actions re-open the attached HWND, verify its PID, and re-resolve every selector segment. Missing routes return `stale_element`. A successful action clears the map and advances the generation.

The returned `stablePath` is explanatory. Clients must use the opaque `elementReference` for actions.

## Tree shape

Snapshots are flattened in breadth-first order with parent references. Each node reports:

- Control type, accessible name, AutomationId, provider class, and framework ID.
- Enabled/offscreen/password state.
- Device-independent bounding rectangle.
- Supported Invoke, Value, SelectionItem, ExpandCollapse, Toggle, and Window patterns.

The MVP deliberately does not return ValuePattern values, help text, arbitrary provider properties, DataContext, bindings, or dependency properties.

## Threading and lifetime

Each attached UIA session owns one `UIA3Automation` instance and serializes inspection/actions through a session gate. Live COM-backed automation elements never cross the session boundary. Provider property reads use narrow safe fallbacks for unsupported, invalid, or unavailable properties.

## Test fixtures

The WPF and WinForms fixtures expose matching automation IDs:

- `InvokeButton`
- `DisabledButton`
- `ValueTextBox`
- `PasswordBox`
- `ColorComboBox`
- `EnabledCheckBox`
- `NavigationTree`, `RootNode`, `ChildNode`, `LeafNode`
- `KeyboardTextBox`, `KeyStatusText`
- `ResultText`

Each operation updates `ResultText` synchronously, giving integration tests a deterministic observable state.

## Milestones

- MVP: the current UIA3 server, consent, semantic snapshots/actions, audit, rate limits, and desktop fixtures.
- V1: binding trace collection where available, validation/layout diagnostics, screenshots with semantic overlays, virtualization helpers, and UIA2 compatibility fallback.
- V2: opt-in authenticated in-process agent for XAML/visual trees, DataContext/dependency properties, commands, recording/replay, and hot reload events.
- Compatibility: dedicated packaged WinUI, Avalonia, and MAUI Windows fixtures plus enterprise CI security policy.
