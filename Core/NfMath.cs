// Nightfall - Copyright (C) 2026 DaUnknown-0
// Licensed under GPL-3.0-or-later. See LICENSE for details.

/*
 * The primitive types of the shared renderer core.
 *
 * WHY THESE EXIST INSTEAD OF UnityEngine.Vector2
 * ----------------------------------------------
 * Everything under Core\ is compiled TWICE: once into the Among Us plugin, and once into the
 * offline render tool that draws the very same view into PNG files outside the game. That second
 * host has no Unity at all. A single `using UnityEngine;` anywhere in this folder would make the
 * offline check impossible, and the offline check is the whole reason the view can be verified
 * before anyone launches the game.
 *
 * So the core speaks in its own vocabulary and the two hosts convert at their edges. The types are
 * deliberately tiny and struct-based: the renderer touches them a few hundred thousand times per
 * frame, and a heap allocation in that path would be felt.
 */

using System;
using System.Runtime.CompilerServices;

namespace Nightfall.Core;

public struct NfVec2
{
    public float X, Y;

    public NfVec2(float x, float y) { X = x; Y = y; }

    public static NfVec2 operator +(NfVec2 a, NfVec2 b) => new(a.X + b.X, a.Y + b.Y);
    public static NfVec2 operator -(NfVec2 a, NfVec2 b) => new(a.X - b.X, a.Y - b.Y);
    public static NfVec2 operator *(NfVec2 a, float s) => new(a.X * s, a.Y * s);
    public static NfVec2 operator /(NfVec2 a, float s) => new(a.X / s, a.Y / s);

    public float Length
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => MathF.Sqrt(X * X + Y * Y);
    }

    public float SqrLength
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => X * X + Y * Y;
    }

    public NfVec2 Normalized
    {
        get
        {
            float l = Length;
            return l > 1e-6f ? new NfVec2(X / l, Y / l) : new NfVec2(0f, 0f);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Dot(NfVec2 a, NfVec2 b) => a.X * b.X + a.Y * b.Y;

    /// The 2D "cross product": the z component of the 3D cross. Sign tells which side of `a` the
    /// vector `b` lies on, which is what the segment intersection below is built from.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Cross(NfVec2 a, NfVec2 b) => a.X * b.Y - a.Y * b.X;

    public static NfVec2 FromAngle(float radians) => new(MathF.Cos(radians), MathF.Sin(radians));

    public override string ToString() => $"({X:0.###}, {Y:0.###})";
}

/// Linear RGB in 0..1. Kept floating point through the whole pipeline and only quantised to bytes
/// when a frame is handed to the host: the flashlight multiplies brightness per pixel, and doing
/// that in 8-bit steps banded the falloff visibly at the edge of the cone.
public struct NfColor
{
    public float R, G, B;

    public NfColor(float r, float g, float b) { R = r; G = g; B = b; }

    public static NfColor operator *(NfColor c, float s) => new(c.R * s, c.G * s, c.B * s);
    public static NfColor operator +(NfColor a, NfColor b) => new(a.R + b.R, a.G + b.G, a.B + b.B);

    public static NfColor Lerp(NfColor a, NfColor b, float t)
    {
        t = NfMath.Clamp01(t);
        return new NfColor(a.R + (b.R - a.R) * t, a.G + (b.G - a.G) * t, a.B + (b.B - a.B) * t);
    }

    /// Packs into the byte order Unity's TextureFormat.RGBA32 expects, which is also what the
    /// offline PNG writer consumes, so both hosts see identical bytes.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ToBytes(byte[] buffer, int offset, byte alpha = 255)
    {
        buffer[offset] = NfMath.ToByte(R);
        buffer[offset + 1] = NfMath.ToByte(G);
        buffer[offset + 2] = NfMath.ToByte(B);
        buffer[offset + 3] = alpha;
    }

    public static NfColor FromBytes(byte r, byte g, byte b) => new(r / 255f, g / 255f, b / 255f);

    public static readonly NfColor Black = new(0f, 0f, 0f);
    public static readonly NfColor White = new(1f, 1f, 1f);
}

public static class NfMath
{
    public const float Pi = 3.14159265358979f;
    public const float TwoPi = Pi * 2f;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Clamp(float v, float min, float max) => v < min ? min : (v > max ? max : v);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int ClampInt(int v, int min, int max) => v < min ? min : (v > max ? max : v);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Lerp(float a, float b, float t) => a + (b - a) * Clamp01(t);

    /// Straight quantisation with no tone curve. Used when writing TEXTURE data, where the value
    /// is a colour rather than a lit result and must survive the round trip unchanged.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte ToByteRaw(float v) => (byte)(Clamp01(v) * 255f + 0.5f);

    /// Where a light value becomes a pixel, with a soft shoulder instead of a hard clip.
    ///
    /// The torch deliberately overshoots at close range, because that overshoot is what makes it
    /// feel like a lamp rather than an ambient tint. Clipping that overshoot to flat white turned
    /// every nearby wall into a featureless slab and swallowed player colours whole: a crewmate two
    /// metres away was a white blob with a black outline. Compressing everything above 0.75 keeps
    /// the sense of glare while leaving the texture and the colour readable inside it.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte ToByte(float v)
    {
        if (v > 0.75f) v = 0.75f + (v - 0.75f) / (1f + (v - 0.75f) * 4f);
        return (byte)(Clamp01(v) * 255f + 0.5f);
    }

    /// Base-two logarithm, read off the float's own exponent with a quadratic fitted to the
    /// mantissa. Accurate to about a hundredth of a level, which is far below what a mip choice can
    /// show, and it runs in a handful of instructions instead of a library call.
    ///
    /// It is called once per textured pixel, so the difference is not academic: MathF.Log2 there
    /// costs more than the whole rest of the inner loop put together.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float FastLog2(float x)
    {
        if (x <= 0f) return -60f;
        int i = BitConverter.SingleToInt32Bits(x);
        float e = ((i >> 23) & 0xFF) - 127;
        float m = BitConverter.Int32BitsToSingle((i & 0x007FFFFF) | (127 << 23));
        return e + (-0.34484843f * m + 2.02466578f) * m - 1.67487759f;
    }

    /// Wraps an angle into -PI..PI. Used wherever a difference of two headings is taken: without it
    /// a player standing just clockwise of straight ahead would be judged to be nearly behind you.
    public static float WrapAngle(float a)
    {
        while (a > Pi) a -= TwoPi;
        while (a < -Pi) a += TwoPi;
        return a;
    }

    /// Smooth 0..1 ramp with zero derivative at both ends. The flashlight cone uses it so the beam
    /// has no visible hard rim.
    ///
    /// edge1 BELOW edge0 is legal and means a falling ramp - which is how most calls here use it
    /// (SmoothStep(outerAngle, innerAngle, off) is "bright in the middle, dark at the edge"). The
    /// degenerate-edge guard therefore has to test the ABSOLUTE difference. It did not, at first,
    /// and the result was that every falling ramp in the renderer silently returned 0: the torch
    /// had no cone at all and every frame was lit by ambient alone. Nothing crashed, nothing warned,
    /// the picture was just uniformly, plausibly wrong.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float SmoothStep(float edge0, float edge1, float x)
    {
        float d = edge1 - edge0;
        if (MathF.Abs(d) < 1e-6f) return x < edge0 ? 0f : 1f;
        float t = Clamp01((x - edge0) / d);
        return t * t * (3f - 2f * t);
    }

    /// Deterministic value noise. The textures have to look identical in the game and in the
    /// offline renderer, so nothing in this codebase may use a seeded RNG whose implementation
    /// could differ between hosts: everything is derived from these integer hashes instead.
    public static float Hash(int x, int y, int seed = 0)
    {
        unchecked
        {
            int h = x * 374761393 + y * 668265263 + seed * 1442695041;
            h = (h ^ (h >> 13)) * 1274126177;
            h ^= h >> 16;
            return (h & 0x7FFFFFFF) / (float)0x7FFFFFFF;
        }
    }

    /// Smoothly interpolated value noise on the integer lattice.
    public static float ValueNoise(float x, float y, int seed = 0)
    {
        int xi = (int)MathF.Floor(x), yi = (int)MathF.Floor(y);
        float xf = x - xi, yf = y - yi;
        float u = xf * xf * (3f - 2f * xf), v = yf * yf * (3f - 2f * yf);
        float a = Hash(xi, yi, seed), b = Hash(xi + 1, yi, seed);
        float c = Hash(xi, yi + 1, seed), d = Hash(xi + 1, yi + 1, seed);
        return (a + (b - a) * u) + ((c + (d - c) * u) - (a + (b - a) * u)) * v;
    }

    /// Several octaves of the above. The wall textures are built almost entirely from this.
    public static float Fbm(float x, float y, int octaves, int seed = 0, float gain = 0.5f)
    {
        float sum = 0f, amp = 1f, norm = 0f, fx = x, fy = y;
        for (int i = 0; i < octaves; i++)
        {
            sum += ValueNoise(fx, fy, seed + i * 17) * amp;
            norm += amp;
            amp *= gain;
            fx *= 2f;
            fy *= 2f;
        }
        return norm > 0f ? sum / norm : 0f;
    }
}
