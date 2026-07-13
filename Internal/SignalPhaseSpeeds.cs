using System;
using System.Collections.Generic;

namespace Tanuden.Rudolf.Adapters.TrainCrew.Internal;

/// <summary>
///     Route-wide per-phase speed table for TRAIN CREW plus per-instance runtime overrides.
///     Populates <see cref="Tanuden.Rudolf.Profile.Vocabularies.SignalPhaseSpeed" /> at
///     profile-emit time and lets the per-frame build apply the Tatehama-home signal
///     override before computing existing <c>speedLimit.current</c> /
///     <c>speedLimit.next[*].limit</c> field values.
/// </summary>
internal static class SignalPhaseSpeeds
{
    /// <summary>
    ///     Route-wide per-phase km/h. Keys are Rudolf phase indices. Phase 5 (YGF) is
    ///     absent because TC does not model YGF; the most permissive speed-capped aspect
    ///     slots at phase 6 with the line-specific capped-permissive km/h value.
    /// </summary>
    public static readonly IReadOnlyDictionary<int, double> PhaseSpeedKmh
        = new Dictionary<int, double>
        {
            [1] = 0,
            [2] = 25,
            [3] = 55,
            [4] = 80,
            [6] = 110
        };

    /// <summary>
    ///     Per-instance override: Tatehama-down home signals at phase Y emit 40 km/h
    ///     instead of the route-wide 55 km/h. Prefix-matches the signal name against
    ///     the standard Tatehama down-home prefix; returns <c>null</c> when no override
    ///     applies. Applied at runtime by the frame builder; never appears on the wire
    ///     as a new schema field.
    /// </summary>
    public static double? TryOverrideSpeed(string? name, string? phase)
    {
        if (name != null && phase == "Y" && name.StartsWith("館浜下り場内", StringComparison.Ordinal))
            return 40.0;
        return null;
    }
}
