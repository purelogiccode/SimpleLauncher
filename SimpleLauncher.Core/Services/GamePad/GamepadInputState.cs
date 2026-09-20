namespace SimpleLauncher.Core.Services.GamePad;

/// <summary>
///     A snapshot of controller input produced by a platform backend. Analog values are
///     normalized to [-1, 1] with the XInput convention (stick up is positive) and are
///     reported before the user-configured dead zone is applied.
/// </summary>
internal readonly record struct GamepadInputState(
    float LeftX,
    float LeftY,
    float RightX,
    float RightY,
    bool DpadUp,
    bool DpadDown,
    bool DpadLeft,
    bool DpadRight,
    bool PrimaryPressed,
    bool SecondaryPressed)
{
    /// <summary>
    ///     Gets a neutral state (sticks centered, no button or D-pad pressed).
    /// </summary>
    public static GamepadInputState Empty { get; } =
        new(0f, 0f, 0f, 0f, false, false, false, false, false, false);

    /// <summary>
    ///     Applies the user-configured dead zone to both sticks and rescales the surviving
    ///     range back to [-1, 1], mirroring the processing used by the Windows input path.
    /// </summary>
    public GamepadInputState WithDeadZone(float deadZoneX, float deadZoneY)
    {
        return this with
        {
            LeftX = ProcessAxis(LeftX, deadZoneX),
            LeftY = ProcessAxis(LeftY, deadZoneY),
            RightX = ProcessAxis(RightX, deadZoneX),
            RightY = ProcessAxis(RightY, deadZoneY),
        };
    }

    private static float ProcessAxis(float value, float deadZone)
    {
        var magnitude = Math.Abs(value);
        if (magnitude < deadZone) return 0f;

        var result = (magnitude - deadZone) * Math.Sign(value);
        return deadZone > 0f ? result / (1f - deadZone) : result;
    }
}
