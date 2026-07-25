using Microsoft.Xna.Framework.Input;

namespace FlatRedBall2.Input;

/// <summary>
/// Adapts a gamepad's D-pad to an <see cref="I2DInput"/>. The D-pad is exposed as four buttons
/// (not axes — there is no <see cref="GamepadAxis"/> for it), so this fills the same role for
/// D-pad input that <see cref="KeyboardInput2D"/> fills for keyboard input.
/// </summary>
/// <remarks>
/// Y+ is up: holding <see cref="Buttons.DPadUp"/> reports <c>Y = +1</c>, matching world-space
/// coordinates. Not normalized to unit length. Combine with <see cref="GamepadInput2D"/> (analog
/// stick) via <see cref="I2DInputExtensions.Or"/> so both control schemes work together.
/// </remarks>
public class GamepadDPadInput2D : I2DInput
{
    private readonly IGamepad _gamepad;

    /// <param name="gamepad">The source gamepad. Obtain via <c>Engine.Input.GetGamepad(index)</c>.</param>
    public GamepadDPadInput2D(IGamepad gamepad) => _gamepad = gamepad;

    /// <summary>−1 if only D-pad left is held, +1 if only D-pad right is held, 0 otherwise (including both held).</summary>
    public float X
    {
        get
        {
            var x = 0f;
            if (_gamepad.IsButtonDown(Buttons.DPadLeft))  x -= 1f;
            if (_gamepad.IsButtonDown(Buttons.DPadRight)) x += 1f;
            return x;
        }
    }

    /// <summary>+1 if only D-pad up is held (Y+ up), −1 if only D-pad down is held, 0 otherwise (including both held).</summary>
    public float Y
    {
        get
        {
            var y = 0f;
            if (_gamepad.IsButtonDown(Buttons.DPadDown)) y -= 1f;
            if (_gamepad.IsButtonDown(Buttons.DPadUp))   y += 1f;
            return y;
        }
    }
}
