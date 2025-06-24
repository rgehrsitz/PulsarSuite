// Pulsar Runtime Common Helpers
// -----------------------------------------------------------------------------
// This file is generated once by the compiler and referenced by every generated
// rule class.  It contains:
//   • EvalResult – 3‑valued boolean logic enum
//   • WindowTracker – temporal threshold evaluator (per sensor)
//   • EmitGuard – helper for on_enter / on_change emission control
// -----------------------------------------------------------------------------

using System;
using System.Collections.Concurrent;

namespace Pulsar.Runtime.Helpers
{
    // 1️⃣  Three‑valued logic --------------------------------------------------
    public enum EvalResult
    {
        True,
        False,
        Indeterminate
    }

    public static class EvalOps
    {
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static EvalResult And(EvalResult a, EvalResult b)
            => (a, b) switch
            {
                (EvalResult.False, _)              => EvalResult.False,
                (_, EvalResult.False)              => EvalResult.False,
                (EvalResult.True, EvalResult.True) => EvalResult.True,
                _                                  => EvalResult.Indeterminate
            };

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static EvalResult Or(EvalResult a, EvalResult b)
            => (a, b) switch
            {
                (EvalResult.True, _)               => EvalResult.True,
                (_, EvalResult.True)               => EvalResult.True,
                (EvalResult.False, EvalResult.False) => EvalResult.False,
                _                                   => EvalResult.Indeterminate
            };

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static EvalResult Not(EvalResult v)
            => v == EvalResult.Indeterminate ? EvalResult.Indeterminate
                                             : (v == EvalResult.True ? EvalResult.False : EvalResult.True);
    }

    // 2️⃣  Temporal threshold tracker ----------------------------------------
    public sealed class WindowTracker
    {
        private readonly double _threshold;
        private readonly Func<double, double, bool> _cmp;
        private readonly TimeSpan _duration;
        private DateTime _start;   // when condition first satisfied
        private bool _inWindow;

        public WindowTracker(double threshold, string op, TimeSpan duration)
        {
            _threshold = threshold;
            _duration = duration;
            _cmp = op switch
            {
                ">"  => (v, t) => v > t,
                ">=" => (v, t) => v >= t,
                "<"  => (v, t) => v < t,
                "<=" => (v, t) => v <= t,
                _ => throw new ArgumentException($"Unsupported op {op}")
            };
        }

        public EvalResult Evaluate(double? sensorVal, DateTime nowUtc)
        {
            if (!sensorVal.HasValue)
            {
                // treat unavailable as indeterminate – window paused
                _inWindow = false;
                return EvalResult.Indeterminate;
            }

            var ok = _cmp(sensorVal.Value, _threshold);
            if (ok)
            {
                if (!_inWindow)
                {
                    _start = nowUtc;
                    _inWindow = true;
                }
                return (nowUtc - _start >= _duration) ? EvalResult.True : EvalResult.False;
            }
            else
            {
                _inWindow = false;
                return EvalResult.False;
            }
        }
    }

    // 3️⃣  Emit guard helper ---------------------------------------------------
    public enum EmitMode { Always, OnChange, OnEnter }

    public sealed class EmitGuard
    {
        private readonly EmitMode _mode;
        private readonly ConcurrentDictionary<string, object?> _lastValues = new();
        private bool _wasActive;

        public EmitGuard(EmitMode mode) => _mode = mode;

        public bool ShouldEmit(string branchId, string key, object? newValue, bool branchActive)
        {
            if (_mode == EmitMode.Always) return branchActive;

            if (_mode == EmitMode.OnEnter)
            {
                var fire = branchActive && !_wasActive;
                _wasActive = branchActive;
                return fire;
            }

            // OnChange
            if (!branchActive)
            {
                _wasActive = false;
                return false;
            }

            var changed = !_lastValues.TryGetValue(key, out var old) || !(Equals(old, newValue));
            if (changed) _lastValues[key] = newValue;
            _wasActive = true;
            return changed;
        }
    }
}