# Unattended operation review

Reviewed on 8 October 2026 using ReactiveMemory project history, the local redacted UIInspect audit log, and the implementation.

## Observed usage

The available local audit log contained 235 completed inspections, 96 completed actions, 19 completed discoveries, 13 completed attaches, and 17 pending consent requests. It also recorded three consent denials, three attaches denied because consent was required, and two operations denied because consent expired. These are historical event counts, not a controlled success-rate benchmark; pending requests can later complete.

ReactiveMemory records the previous design as a user-approved broker lease restricted to 1, 2, 5, 8, 12, or 24 hours. That design reduces repeated dialogs but still requires initial approval and renewal. Historical framework verification covered WPF, WinForms, WinUI, Avalonia, and MAUI.

## Changes

- Explicit unrestricted startup configuration removes server consent dialogs, broker round trips, lease renewal, and per-minute throttling for that server process. Process identity validation and session ownership remain active.
- Sibling selectors read each included sibling identity once and compute ordinals in linear time. Previously each child reread the complete sibling set, producing quadratic cross-process property traffic.
- Inspection queues only nodes that can fit in the requested snapshot.
- Clicks use supported semantic patterns before physical mouse input. Logical keyboard input checks the focused target before sending a key.
- Disposal shares the session operation gate so it cannot dispose the UIA client during an active operation.

## Practical limits

Client-side MCP permission prompts are controlled by the hosting agent application. Server startup configuration cannot change those settings. Windows integrity levels and inaccessible desktops also remain operating-system constraints.

Semantic actions are preferable while a person uses the same PC. Controls without suitable UIA patterns require physical input, which shares the pointer or keyboard with the user. Successful actions invalidate snapshot references so a changed tree cannot silently redirect a subsequent action.

The algorithmic improvement does not establish an end-to-end latency or success percentage. Measure those against the actual target applications and providers before making numerical performance claims.
