using System;
using UnityEngine;

public class MathExtentions
{
    public static float FastInverseSqrt(float number)
    {
        const float threehalfs = 1.5f;
        float x2 = number * 0.5f;
        float y = number;
        // Convert float to int (reinterpret bits)
        byte[] bytes = BitConverter.GetBytes(y);
        int i = BitConverter.ToInt32(bytes, 0);
        // Apply the magic constant operation
        i = 0x5f3759df - (i >> 1);
        // Convert int back to float (reinterpret bits)
        bytes = BitConverter.GetBytes(i);
        y = BitConverter.ToSingle(bytes, 0);
        // Perform the Newton-Raphson iteration
        y = y * (threehalfs - (x2 * y * y));
        return y;
    }

    public static Vector3 FastNorimalize(Vector3 vector)
    {
        return vector * FastInverseSqrt(vector.sqrMagnitude);
    }

    public static float FastMagnitude(Vector3 vector)
    {
        return 1 / FastInverseSqrt(vector.sqrMagnitude);
    }
}