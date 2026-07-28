// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
namespace UIInspect.MCP.Core.Models;

/// <summary>Identifies one process instance and prevents PID reuse from inheriting consent.</summary>
/// <param name="ProcessId">Operating-system process identifier.</param>
/// <param name="StartedAtUtc">Process creation time.</param>
/// <param name="ProcessName">Executable process name.</param>
/// <param name="ExecutablePath">Best-effort executable path.</param>
/// <param name="SessionId">Windows logon session identifier.</param>
public sealed record ProcessIdentity(int ProcessId, DateTimeOffset StartedAtUtc, string ProcessName, string ExecutablePath, int SessionId);
