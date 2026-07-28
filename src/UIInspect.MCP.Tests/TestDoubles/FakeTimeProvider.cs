// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace UIInspect.MCP.Tests;

/// <summary>Controllable clock for deterministic tests.</summary>
internal sealed class FakeTimeProvider : TimeProvider
{
    /// <summary>Initializes a new instance of the <see cref="FakeTimeProvider"/> class.</summary>
    /// <param name="utcNow">Initial UTC time.</param>
    public FakeTimeProvider(DateTimeOffset utcNow) => UtcNow = utcNow;

    /// <summary>Gets the simulated current UTC time.</summary>
    internal DateTimeOffset UtcNow { get; private set; }

    /// <inheritdoc/>
    public override DateTimeOffset GetUtcNow() => UtcNow;

    /// <summary>Moves the simulated clock forward.</summary>
    /// <param name="duration">Elapsed duration to add.</param>
    internal void Advance(TimeSpan duration) => UtcNow = UtcNow.Add(duration);
}
