using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

/// <summary>
/// The keys the Tomplay keyboard sends (it shows up as a normal keyboard), so a PC keyboard plays the same way.
/// </summary>
public static class TomplayInput
{
    // Three octaves, C to B, in the order of the mapping table: low, middle, high.
    static readonly Key[] Keys =
    {
        Key.Z, Key.Digit1, Key.X, Key.Digit2, Key.C, Key.V, Key.Digit3, Key.B, Key.Digit4, Key.N, Key.Digit5, Key.M,
        Key.A, Key.Digit6, Key.S, Key.Digit7, Key.D, Key.F, Key.Digit8, Key.G, Key.Digit9, Key.H, Key.Digit0, Key.J,
        Key.Q, Key.I, Key.W, Key.O, Key.E, Key.R, Key.P, Key.T, Key.K, Key.Y, Key.L, Key.U,
    };

    /// <summary>True on the frame any piano key goes down, whatever its pitch.</summary>
    public static bool AnyKeyPressedThisFrame()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null) return false;

        foreach (var key in Keys)
        {
            KeyControl control = keyboard[key];
            if (control.wasPressedThisFrame) return true;
        }
        return false;
    }
}
