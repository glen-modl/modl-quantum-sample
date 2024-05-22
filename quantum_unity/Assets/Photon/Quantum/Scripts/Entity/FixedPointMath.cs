using Photon.Deterministic;
using Quantum;  // Ensure the Quantum namespace contains the FP type

public static class FixedPointMath
{
    public const int FractionalBits = 16;
    public const long One = 1L << FractionalBits;

    // Addition
    public static FP Add(FP a, FP b)
    {
        return a + b;
    }

    // Subtraction
    public static FP Subtract(FP a, FP b)
    {
        return a - b;
    }

    // Multiplication using inline arithmetic
    public static FP Multiply(FP a, FP b)
    {
        FP result = default;
        result.RawValue = (a.RawValue * b.RawValue) >> FractionalBits;
        return result;
    }

    // Division using inline arithmetic
    public static FP Divide(FP a, FP b)
    {
        FP result = default;
        result.RawValue = (a.RawValue << FractionalBits) / b.RawValue;
        return result;
    }

    // Conversion from float to FP
    public static FP FloatToFixed(float value)
    {
        // Use FromFloat_UNSAFE only during edit or build time
        return FP.FromFloat_UNSAFE(value);
    }

    // Conversion from FP to float (unsafe for simulation, use for debugging or edit time)
    public static float FixedToFloat(FP value)
    {
        return (float)value;
    }
}