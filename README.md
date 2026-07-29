![UIInspect.MCP semantic Windows UI Automation](images/ReadmeHero.png)

<!-- mcp-name: io.github.chrispulman/uiinspect-mcp -->

# UIInspect MCP Server

`UIInspect.MCP.Server` is a consent-gated NuGet MCP server that gives AI agents semantic access to Windows applications through UI Automation 3 (UIA3). It discovers accessible application windows, returns bounded control trees, and performs deterministic actions against opaque element references instead of relying on screenshots or pixel coordinates.

The package runs as a local stdio server on .NET 10. WPF and WinForms are directly tested. Standard controls in WinUI, Avalonia, and .NET MAUI Windows applications may be reachable when they expose UI Automation providers, but those frameworks are not yet compatibility-certified.

## Quick Install

Once the package is available on NuGet.org, click to install it in your preferred environment:

[![VS Code - Install UIInspect MCP](https://img.shields.io/badge/VS_Code-Install_UIInspect_MCP-0098FF?style=flat-square&logo=visualstudiocode&logoColor=white)](https://vscode.dev/redirect/mcp/install?name=uiinspect-mcp&config=%7B%22type%22%3A%22stdio%22%2C%22command%22%3A%22dnx%22%2C%22args%22%3A%5B%22UIInspect.MCP.Server%400.%2A%22%2C%22--prerelease%22%2C%22--yes%22%5D%7D)
[![VS Code Insiders - Install UIInspect MCP](https://img.shields.io/badge/VS_Code_Insiders-Install_UIInspect_MCP-24bfa5?style=flat-square&logo=visualstudiocode&logoColor=white)](https://insiders.vscode.dev/redirect/mcp/install?name=uiinspect-mcp&config=%7B%22type%22%3A%22stdio%22%2C%22command%22%3A%22dnx%22%2C%22args%22%3A%5B%22UIInspect.MCP.Server%400.%2A%22%2C%22--prerelease%22%2C%22--yes%22%5D%7D&quality=insiders)
[![Visual Studio - Install UIInspect MCP](https://img.shields.io/badge/Visual_Studio-Install_UIInspect_MCP-5C2D91?style=flat-square&logo=visualstudio&logoColor=white)](https://vs-open.link/mcp-install?%7B%22name%22%3A%22UIInspect.MCP.Server%22%2C%22type%22%3A%22stdio%22%2C%22command%22%3A%22dnx%22%2C%22args%22%3A%5B%22UIInspect.MCP.Server%400.%2A%22%2C%22--prerelease%22%2C%22--yes%22%5D%7D)

Note:

- These install links use the NuGet package identity `UIInspect.MCP.Server` and select the latest `0.*` prerelease.
- The selected package must be available on NuGet.org. For an unpublished local build, expose the package directory as a NuGet feed and add `--source <feed-path>` to the `dnx` arguments.
- UIInspect is Windows-only and requires an interactive desktop.

Manual MCP configuration using NuGet:

```json
{
  "mcpServers": {
    "uiinspect-mcp": {
      "type": "stdio",
      "command": "dnx",
      "args": [
        "UIInspect.MCP.Server@0.2.1-alpha.0.1",
        "--prerelease",
        "--yes"
      ]
    }
  }
}
```

Some clients use `servers` instead of `mcpServers`; only the outer property name changes.

The NUKE build stamps every literal `UIInspect.MCP.Server` package coordinate in this README with the MinVer package version before packaging:

```powershell
dnx UIInspect.MCP.Server@0.2.1-alpha.0.1 --yes
```

## Codex Skill

The package includes the `uiinspect` Codex skill and its MCP dependency metadata. When the packaged server starts, it installs missing skill files into an existing Codex home: `CODEX_HOME` when it is set and exists, otherwise `%USERPROFILE%\.codex` when that directory exists. Automatic installation never overwrites existing skill files.

Install the skill explicitly and create the Codex home when needed. This command installs the skill and exits without starting the MCP server:

```powershell
dnx UIInspect.MCP.Server@0.2.1-alpha.0.1 --yes -- --install-codex-skill
```

To deliberately replace an existing installed copy with the packaged version:

```powershell
dnx UIInspect.MCP.Server@0.2.1-alpha.0.1 --yes -- --install-codex-skill --force
```

Set `CODEX_HOME` before either command when Codex uses a non-default location. The skill is installed at `<Codex home>\skills\uiinspect`.

Invoke the installed skill with a request such as:

```text
Use $uiinspect to inspect and safely operate the target Windows application.
```

## Requirements

- Windows 10 or Windows 11 with an interactive desktop.
- A .NET 10 SDK that provides `dnx`.
- UIInspect must run in the same Windows logon session as the target application.
- UIInspect must run at a sufficient integrity level for the target. A non-elevated server cannot automate an elevated application.

The MCP protocol owns stdout; server diagnostics are written to stderr.

## What the package provides

- Top-level window discovery with process-instance identity and native window handles.
- A trusted, server-owned Windows approval dialog shown at most once per client and exact target process instance during one server session.
- Attach by process ID with an optional native window handle.
- Bounded, flattened UI Automation control-tree snapshots.
- Opaque, generation-scoped element references with explanatory semantic paths.
- Invoke, resolved click, ValuePattern set, SelectionItemPattern select, expand/collapse, and allowlisted logical-key actions.
- Password-element redaction and no control-value collection during inspection.
- Consent bound to the local stdio server principal, expiry and PID-reuse checks, rate limits, application-level append-only redacted JSONL auditing, and deterministic session cleanup.

The current package deliberately does not provide XAML source or visual trees, dependency properties, bindings, validation, DataContext or command diagnostics, hot reload, screenshots, OCR, overlays, recording/replay, arbitrary reflection, shell execution, generic property writes, or TCP transport.

## Consent and security

UIInspect never treats an MCP request as user approval. The local user must approve the exact process instance and requested capabilities in a trusted Windows dialog before the server attaches. Repeated and concurrent requests reuse that server-session decision without opening another dialog. A denial is terminal for that client and process instance until the server restarts, and later capability expansion is denied rather than silently approved or prompted again.

Inspection, interaction, and keyboard access are separate capabilities. Request only the minimum needed for the task. Grants are short-lived and bound to the local stdio server principal, exact process identity, Windows session, and approved capabilities. Tool parameters cannot supply or override that principal.

For unattended tests, keep one MCP server process alive for the complete run, request every required capability during setup, approve the single trusted dialog, and reuse its grants and attached sessions.

Successful actions invalidate all current element references. Re-inspect before the next action so the server can semantically resolve the current UI rather than act on stale coordinates.

The default audit file is:

```text
%LOCALAPPDATA%\UIInspect.MCP\audit\actions.jsonl
```

Set `UIINSPECT_AUDIT_PATH` for the MCP server process to use another location. Audit records exclude entered values, password content, clipboard data, screenshots, raw provider exceptions, and all keystrokes except the allowlisted logical key name. Appends are enforced at the application level; protect the audit directory with an ACL that grants access only to the server account and administrators.

See the complete [security model](https://github.com/ChrisPulman/UIInspect.MCP/blob/main/docs/security.md) for consent scopes, rate limits, audit contents, integrity boundaries, and the threat model.

## MCP tools

| Tool | Purpose | Required access |
|---|---|---|
| `uiinspect_discover_windows` | List top-level UI Automation windows | Discovery |
| `uiinspect_request_consent` | Show the trusted local approval dialog once per exact target and server session | Local user decision |
| `uiinspect_attach` | Open an opaque session for a PID and optional HWND | Inspect |
| `uiinspect_inspect_tree` | Return a bounded semantic snapshot | Inspect |
| `uiinspect_invoke` | Use InvokePattern | Interact |
| `uiinspect_click` | Click a semantically resolved element | Interact |
| `uiinspect_set_value` | Set a value through ValuePattern | Interact |
| `uiinspect_set_text` | Set text through ValuePattern | Interact |
| `uiinspect_select_item` | Use SelectionItemPattern | Interact |
| `uiinspect_expand_collapse` | Use ExpandCollapsePattern | Interact |
| `uiinspect_send_key` | Send one allowlisted logical key after focus | Keyboard |
| `uiinspect_close_session` | Dispose an attached session | Session owner |

## Recommended workflow

1. Call `uiinspect_discover_windows`.
2. Identify the intended process and window using independently known application context.
3. Call `uiinspect_request_consent` once with every capability required for the session. The local user must approve the exact process instance; repeats reuse the decision and cannot expand it.
4. Call `uiinspect_attach`.
5. Call `uiinspect_inspect_tree` with the smallest useful depth and node budget.
6. Select an element using its automation ID, control type, accessible name, patterns, and semantic path.
7. Prefer `uiinspect_invoke`, `uiinspect_set_value` (or its `uiinspect_set_text` alias), `uiinspect_select_item`, or `uiinspect_expand_collapse`. Use `uiinspect_click` only when the provider does not expose InvokePattern, and use `uiinspect_send_key` only when keyboard consent was granted.
8. Re-inspect after every successful action to obtain fresh element references.
9. Call `uiinspect_close_session`.

## Recovery behavior

- On `stale_element`, inspect again and use a newly returned element reference.
- On `consent_expired`, call `uiinspect_request_consent`, then `uiinspect_attach`, then `uiinspect_inspect_tree`. The retained server-session approval renews the grant without showing another dialog.
- On `session_not_found`, attach again when consent remains active; otherwise request consent, attach, and inspect.
- On `target_changed` or `target_unavailable`, rediscover the target; a new exact process identity requires its own consent decision.
- On `rate_limited`, wait for `retryAfterMilliseconds` before retrying.
- Treat `consent_denied` as terminal for that client, target, and server process. Do not retry in a loop.
- Treat `pattern_not_supported` as a semantic limitation; select another supported operation rather than falling back to blind input.

## Package contents

The NuGet package contains:

- The `uiinspect-mcp` .NET tool and its runtime dependencies.
- `.mcp/server.json` MCP registry metadata.
- The recursively packaged `skills/uiinspect` skill, including `SKILL.md` and `agents/openai.yaml`.
- This README and the detailed [MVP behavior and boundaries](https://github.com/ChrisPulman/UIInspect.MCP/blob/main/docs/mvp-design.md).

The package uses `ModelContextProtocol` 2.0.0 and FlaUI 5.0 with UIA3.
