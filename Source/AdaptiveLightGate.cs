using System;

namespace ScreenGlass;

// Keep the original response curve, but hold the shade across small fluctuations.
public sealed class AdaptiveLightGate
{
    private double heldFactor;
    private bool initialized;

    public void Observe(double brightness)
    {
        if (!double.IsFinite(brightness)) return;
        var t = Math.Clamp((brightness - 0.18) / 0.57, 0, 1);
        var factor = t * t * (3 - 2 * t);
        // Compare against the last applied value so gradual substantial changes still register.
        // Large changes take effect on this sample, without a confirmation delay.
        if (!initialized || factor == 0 || factor == 1 || Math.Abs(factor - heldFactor) >= 0.12)
        {
            heldFactor = factor;
            initialized = true;
        }
    }

    public double Target(double strength) => AdaptiveMath.Target(strength) * heldFactor;

    public void Reset() { heldFactor = 0; initialized = false; }
}
