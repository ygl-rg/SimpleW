﻿using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace LitJWT
{
    internal static class FarmHash
    {
        // entry point

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe ulong Hash64(byte[] bytes, int offset, int count)
        {
            fixed (byte* p = &bytes[offset])
            {
                return Hash64(p, (uint)count);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe ulong Hash64(ReadOnlySpan<byte> bytes)
        {
            fixed (byte* p = &MemoryMarshal.GetReference(bytes))
            {
                return Hash64(p, (uint)bytes.Length);
            }
        }

        // port from farmhash.cc
        static unsafe uint Fetch32(byte* p)
        {
            return *(uint*)p;
        }

        struct Pair
        {
            public ulong first;
            public ulong second;

            public Pair(ulong first, ulong second)
            {
                this.first = first;
                this.second = second;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static Pair make_pair(ulong first, ulong second)
        {
            return new Pair(first, second);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void swap(ref ulong x, ref ulong z)
        {
            var temp = z;
            z = x;
            x = temp;
        }

        // Some primes between 2^63 and 2^64 for various uses.
        const ulong k0 = 0xc3a5c85c97cb3127UL;
        const ulong k1 = 0xb492b66fbe98f273UL;
        const ulong k2 = 0x9ae16a3b2f90404fUL;

        static unsafe ulong Fetch64(byte* p)
        {
            return *(ulong*)p;
        }

        static ulong Rotate64(ulong val, int shift)
        {
            return shift == 0 ? val : (val >> shift) | (val << (64 - shift));
        }

        // farmhashna.cc
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static ulong ShiftMix(ulong val)
        {
            return val ^ (val >> 47);
        }

        // farmhashna.cc
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static ulong HashLen16(ulong u, ulong v, ulong mul)
        {
            unchecked
            {
                // Murmur-inspired hashing.
                ulong a = (u ^ v) * mul;
                a ^= a >> 47;
                ulong b = (v ^ a) * mul;
                b ^= b >> 47;
                b *= mul;
                return b;
            }
        }

        // farmhashxo.cc
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static unsafe ulong Hash64(byte* s, uint len)
        {
            if (len <= 16)
            {
                // farmhashna::
                return HashLen0to16(s, len);
            }

            if (len <= 32)
            {
                // farmhashna::
                return HashLen17to32(s, len);
            }

            if (len <= 64)
            {
                return HashLen33to64(s, len);
            }

            if (len <= 96)
            {
                return HashLen65to96(s, len);
            }

            if (len <= 256)
            {
                // farmhashna::
                return Hash64NA(s, len);
            }

            // farmhashuo::
            return Hash64UO(s, len);
        }

        // 0-16 farmhashna.cc
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static unsafe ulong HashLen0to16(byte* s, uint len)
        {
            unchecked
            {
                if (len >= 8)
                {
                    ulong mul = k2 + len * 2;
                    ulong a = Fetch64(s) + k2;
                    ulong b = Fetch64(s + len - 8);
                    ulong c = Rotate64(b, 37) * mul + a;
                    ulong d = (Rotate64(a, 25) + b) * mul;
                    return HashLen16(c, d, mul);
                }
                if (len >= 4)
                {
                    ulong mul = k2 + len * 2;
                    ulong a = Fetch32(s);
                    return HashLen16(len + (a << 3), Fetch32(s + len - 4), mul);
                }
                if (len > 0)
                {
                    ushort a = s[0];
                    ushort b = s[len >> 1];
                    ushort c = s[len - 1];
                    uint y = a + ((uint)b << 8);
                    uint z = len + ((uint)c << 2);
                    return ShiftMix(y * k2 ^ z * k0) * k2;
                }
                return k2;
            }
        }

        // 17-32 farmhashna.cc
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static unsafe ulong HashLen17to32(byte* s, uint len)
        {
            unchecked
            {
                ulong mul = k2 + len * 2;
                ulong a = Fetch64(s) * k1;
                ulong b = Fetch64(s + 8);
                ulong c = Fetch64(s + len - 8) * mul;
                ulong d = Fetch64(s + len - 16) * k2;
                return HashLen16(Rotate64(a + b, 43) + Rotate64(c, 30) + d,
                                 a + Rotate64(b + k2, 18) + c, mul);
            }
        }

        // farmhashxo.cc
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static unsafe ulong H32(byte* s, uint len, ulong mul, ulong seed0 = 0, ulong seed1 = 0)
        {
            unchecked
            {
                ulong a = Fetch64(s) * k1;
                ulong b = Fetch64(s + 8);
                ulong c = Fetch64(s + len - 8) * mul;
                ulong d = Fetch64(s + len - 16) * k2;
                ulong u = Rotate64(a + b, 43) + Rotate64(c, 30) + d + seed0;
                ulong v = a + Rotate64(b + k2, 18) + c + seed1;
                a = ShiftMix((u ^ v) * mul);
                b = ShiftMix((v ^ a) * mul);
                return b;
            }
        }

        // 33-64 farmhashxo.cc
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static unsafe ulong HashLen33to64(byte* s, uint len)
        {
            const ulong mul0 = k2 - 30;

            unchecked
            {
                ulong mul1 = k2 - 30 + 2 * len;
                ulong h0 = H32(s, 32, mul0);
                ulong h1 = H32(s + len - 32, 32, mul1);
                return (h1 * mul1 + h0) * mul1;
            }
        }

        // 65-96 farmhashxo.cc
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static unsafe ulong HashLen65to96(byte* s, uint len)
        {
            const ulong mul0 = k2 - 114;

            unchecked
            {
                ulong mul1 = k2 - 114 + 2 * len;
                ulong h0 = H32(s, 32, mul0);
                ulong h1 = H32(s + 32, 32, mul1);
                ulong h2 = H32(s + len - 32, 32, mul1, h0, h1);
                return (h2 * 9 + (h0 >> 17) + (h1 >> 21)) * mul1;
            }
        }

        // farmhashna.cc
        // Return a 16-byte hash for 48 bytes.  Quick and dirty.
        // Callers do best to use "random-looking" values for a and b.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static unsafe Pair WeakHashLen32WithSeeds(ulong w, ulong x, ulong y, ulong z, ulong a, ulong b)
        {
            unchecked
            {
                a += w;
                b = Rotate64(b + a + z, 21);
                ulong c = a;
                a += x;
                a += y;
                b += Rotate64(a, 44);
                return make_pair(a + z, b + c);
            }
        }

        // farmhashna.cc
        // Return a 16-byte hash for s[0] ... s[31], a, and b.  Quick and dirty.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static unsafe Pair WeakHashLen32WithSeeds(byte* s, ulong a, ulong b)
        {
            return WeakHashLen32WithSeeds(Fetch64(s),
                                          Fetch64(s + 8),
                                          Fetch64(s + 16),
                                          Fetch64(s + 24),
                                          a,
                                          b);
        }

        // na(97-256) farmhashna.cc
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static unsafe ulong Hash64NA(byte* s, uint len)
        {
            const ulong seed = 81;

            unchecked
            {
                // For strings over 64 bytes we loop.  Internal state consists of
                // 56 bytes: v, w, x, y, and z.
                ulong x = seed;
                ulong y = seed * k1 + 113;
                ulong z = ShiftMix(y * k2 + 113) * k2;
                var v = make_pair(0, 0);
                var w = make_pair(0, 0);
                x = x * k2 + Fetch64(s);

                // Set end so that after the loop we have 1 to 64 bytes left to process.
                byte* end = s + ((len - 1) / 64) * 64;
                byte* last64 = end + ((len - 1) & 63) - 63;

                do
                {
                    x = Rotate64(x + y + v.first + Fetch64(s + 8), 37) * k1;
                    y = Rotate64(y + v.second + Fetch64(s + 48), 42) * k1;
                    x ^= w.second;
                    y += v.first + Fetch64(s + 40);
                    z = Rotate64(z + w.first, 33) * k1;
                    v = WeakHashLen32WithSeeds(s, v.second * k1, x + w.first);
                    w = WeakHashLen32WithSeeds(s + 32, z + w.second, y + Fetch64(s + 16));
                    swap(ref z, ref x);
                    s += 64;
                } while (s != end);
                ulong mul = k1 + ((z & 0xff) << 1);
                // Make s point to the last 64 bytes of input.
                s = last64;
                w.first += ((len - 1) & 63);
                v.first += w.first;
                w.first += v.first;
                x = Rotate64(x + y + v.first + Fetch64(s + 8), 37) * mul;
                y = Rotate64(y + v.second + Fetch64(s + 48), 42) * mul;
                x ^= w.second * 9;
                y += v.first * 9 + Fetch64(s + 40);
                z = Rotate64(z + w.first, 33) * mul;
                v = WeakHashLen32WithSeeds(s, v.second * mul, x + w.first);
                w = WeakHashLen32WithSeeds(s + 32, z + w.second, y + Fetch64(s + 16));
                swap(ref z, ref x);
                return HashLen16(HashLen16(v.first, w.first, mul) + ShiftMix(y) * k0 + z,
                                 HashLen16(v.second, w.second, mul) + x,
                                 mul);
            }
        }

        // farmhashuo.cc
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static ulong H(ulong x, ulong y, ulong mul, int r)
        {
            unchecked
            {
                ulong a = (x ^ y) * mul;
                a ^= (a >> 47);
                ulong b = (y ^ a) * mul;
                return Rotate64(b, r) * mul;
            }
        }

        // uo(257-) farmhashuo.cc, Hash64WithSeeds
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static unsafe ulong Hash64UO(byte* s, uint len)
        {
            const ulong seed0 = 81;
            const ulong seed1 = 0;

            unchecked
            {
                // For strings over 64 bytes we loop.  Internal state consists of
                // 64 bytes: u, v, w, x, y, and z.
                ulong x = seed0;
                ulong y = seed1 * k2 + 113;
                ulong z = ShiftMix(y * k2) * k2;
                var v = make_pair(seed0, seed1);
                var w = make_pair(0, 0);
                ulong u = x - z;
                x *= k2;
                ulong mul = k2 + (u & 0x82);

                // Set end so that after the loop we have 1 to 64 bytes left to process.
                byte* end = s + ((len - 1) / 64) * 64;
                byte* last64 = end + ((len - 1) & 63) - 63;

                do
                {
                    ulong a0 = Fetch64(s);
                    ulong a1 = Fetch64(s + 8);
                    ulong a2 = Fetch64(s + 16);
                    ulong a3 = Fetch64(s + 24);
                    ulong a4 = Fetch64(s + 32);
                    ulong a5 = Fetch64(s + 40);
                    ulong a6 = Fetch64(s + 48);
                    ulong a7 = Fetch64(s + 56);
                    x += a0 + a1;
                    y += a2;
                    z += a3;
                    v.first += a4;
                    v.second += a5 + a1;
                    w.first += a6;
                    w.second += a7;

                    x = Rotate64(x, 26);
                    x *= 9;
                    y = Rotate64(y, 29);
                    z *= mul;
                    v.first = Rotate64(v.first, 33);
                    v.second = Rotate64(v.second, 30);
                    w.first ^= x;
                    w.first *= 9;
                    z = Rotate64(z, 32);
                    z += w.second;
                    w.second += z;
                    z *= 9;
                    swap(ref u, ref y);

                    z += a0 + a6;
                    v.first += a2;
                    v.second += a3;
                    w.first += a4;
                    w.second += a5 + a6;
                    x += a1;
                    y += a7;

                    y += v.first;
                    v.first += x - y;
                    v.second += w.first;
                    w.first += v.second;
                    w.second += x - y;
                    x += w.second;
                    w.second = Rotate64(w.second, 34);
                    swap(ref u, ref z);
                    s += 64;
                } while (s != end);
                // Make s point to the last 64 bytes of input.
                s = last64;
                u *= 9;
                v.second = Rotate64(v.second, 28);
                v.first = Rotate64(v.first, 20);
                w.first += ((len - 1) & 63);
                u += y;
                y += u;
                x = Rotate64(y - x + v.first + Fetch64(s + 8), 37) * mul;
                y = Rotate64(y ^ v.second ^ Fetch64(s + 48), 42) * mul;
                x ^= w.second * 9;
                y += v.first + Fetch64(s + 40);
                z = Rotate64(z + w.first, 33) * mul;
                v = WeakHashLen32WithSeeds(s, v.second * mul, x + w.first);
                w = WeakHashLen32WithSeeds(s + 32, z + w.second, y + Fetch64(s + 16));
                return H(HashLen16(v.first + x, w.first ^ y, mul) + z - u,
                         H(v.second + y, w.second + z, k2, 30) ^ x,
                         k2,
                         31);
            }
        }
    }
}