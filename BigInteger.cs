/*
Written in 2013 by Peter O.

Parts of the code were adapted by Peter O. from
the public-domain code from the library
CryptoPP by Wei Dai.

Any copyright is dedicated to the Public Domain.
http://creativecommons.org/publicdomain/zero/1.0/
If you like this, you should donate to Peter O.
at: http://upokecenter.com/d/
 */
using System;

namespace PeterO {
    /// <summary>An arbitrary-precision integer. Instances of this class
    /// are immutable, so they are inherently safe for use by multiple threads.</summary>
  public sealed partial class BigInteger : IComparable<BigInteger>, IEquatable<BigInteger>
  {
    private static int CountWords(short[] array, int n) {
      while (n != 0 && array[n - 1] == 0) {
        --n;
      }
      return (int)n;
    }

    private static short ShiftWordsLeftByBits(short[] r, int rstart, int n, int shiftBits) {
      #if DEBUG
      if (!(shiftBits < 16)) {
        throw new ArgumentException("doesn't satisfy shiftBits<16");
      }
      #endif

      unchecked {
        short u, carry = 0;
        if (shiftBits != 0) {
          for (int i = 0; i < n; ++i) {
            u = r[rstart + i];
            r[rstart + i] = (short)((int)(u << (int)shiftBits) | (((int)carry) & 0xffff));
            carry = (short)((((int)u) & 0xffff) >> (int)(16 - shiftBits));
          }
        }
        return carry;
      }
    }

    private static short ShiftWordsRightByBits(short[] r, int rstart, int n, int shiftBits) {
      // DebugAssert.IsTrue(shiftBits<16,"{0} line {1}: shiftBits<16","words.h",67);
      short u, carry = 0;
      unchecked {
        if (shiftBits != 0) {
          for (int i = n; i > 0; --i) {
            u = r[rstart + i - 1];
            r[rstart + i - 1] = (short)((((((int)u) & 0xffff) >> (int)shiftBits) & 0xffff) | (((int)carry) & 0xffff));
            carry = (short)((((int)u) & 0xffff) << (int)(16 - shiftBits));
          }
        }
        return carry;
      }
    }

    private static short ShiftWordsRightByBitsSignExtend(short[] r, int rstart, int n, int shiftBits) {
      // DebugAssert.IsTrue(shiftBits<16,"{0} line {1}: shiftBits<16","words.h",67);
      unchecked {
        short u, carry = (short)((int)0xffff << (int)(16 - shiftBits));
        if (shiftBits != 0) {
          for (int i = n; i > 0; --i) {
            u = r[rstart + i - 1];
            r[rstart + i - 1] = (short)(((((int)u) & 0xffff) >> (int)shiftBits) | (((int)carry) & 0xffff));
            carry = (short)((((int)u) & 0xffff) << (int)(16 - shiftBits));
          }
        }
        return carry;
      }
    }

    private static void ShiftWordsLeftByWords(short[] r, int rstart, int n, int shiftWords) {
      shiftWords = Math.Min(shiftWords, n);
      if (shiftWords != 0) {
        for (int i = n - 1; i >= shiftWords; --i) {
          r[rstart + i] = r[rstart + i - shiftWords];
        }
        Array.Clear((short[])r, rstart, shiftWords);
      }
    }

    private static void ShiftWordsRightByWordsSignExtend(short[] r, int rstart, int n, int shiftWords) {
      shiftWords = Math.Min(shiftWords, n);
      if (shiftWords != 0) {
        for (int i = 0; i + shiftWords < n; ++i) {
          r[rstart + i] = r[rstart + i + shiftWords];
        }
        rstart += n - shiftWords;
        // Sign extend
        for (int i = 0; i < shiftWords; ++i) {
          r[rstart + i] = unchecked((short)0xffff);
        }
      }
    }

    private static int Compare(short[] words1, int astart, short[] words2, int bstart, int n) {
      while (unchecked(n--) != 0) {
        int an = ((int)words1[astart + n]) & 0xffff;
        int bn = ((int)words2[bstart + n]) & 0xffff;
        if (an > bn) {
          return 1;
        } else if (an < bn) {
          return -1;
        }
      }
      return 0;
    }

    /*
    private static int CompareUnevenSize(
      short[] words1,
      int astart,
      int acount,
      short[] words2,
      int bstart,
      int bcount) {
      int n = acount;
      if (acount > bcount) {
        while (unchecked(acount--) != bcount) {
          if (words1[astart + acount] != 0) {
            return 1;
          }
        }
        n = bcount;
      } else if (bcount > acount) {
        while (unchecked(bcount--) != acount) {
          if (words1[astart + acount] != 0) {
            return -1;
          }
        }
        n = acount;
      }
      while (unchecked(n--) != 0) {
        int an = ((int)words1[astart + n]) & 0xffff;
        int bn = ((int)words2[bstart + n]) & 0xffff;
        if (an > bn) {
          return 1;
        } else if (an < bn) {
          return -1;
        }
      }
      return 0;
    }
     */

    private static int CompareWithOneBiggerWords1(short[] words1, int astart, short[] words2, int bstart, int words1Count) {
      // NOTE: Assumes that words2's count is 1 less
      if (words1[astart + words1Count - 1] != 0) {
        return 1;
      }
      --words1Count;
      while (unchecked(words1Count--) != 0) {
        int an = ((int)words1[astart + words1Count]) & 0xffff;
        int bn = ((int)words2[bstart + words1Count]) & 0xffff;
        if (an > bn) {
          return 1;
        } else if (an < bn) {
          return -1;
        }
      }
      return 0;
    }

    private static int Increment(short[] words1, int words1Start, int n, short words2) {
      unchecked {
        // DebugAssert.IsTrue(n!=0,"{0} line {1}: n","integer.cpp",63);
        short tmp = words1[words1Start];
        words1[words1Start] = (short)(tmp + words2);
        if ((((int)words1[words1Start]) & 0xffff) >= (((int)tmp) & 0xffff)) {
          return 0;
        }
        for (int i = 1; i < n; ++i) {
          ++words1[words1Start + i];
          if (words1[words1Start + i] != 0) {
            return 0;
          }
        }
        return 1;
      }
    }

    private static int Decrement(short[] words1, int words1Start, int n, short words2) {
      // DebugAssert.IsTrue(n!=0,"{0} line {1}: n","integer.cpp",76);
      unchecked {
        short tmp = words1[words1Start];
        words1[words1Start] = (short)(tmp - words2);
        if ((((int)words1[words1Start]) & 0xffff) <= (((int)tmp) & 0xffff)) {
          return 0;
        }
        for (int i = 1; i < n; ++i) {
          tmp = words1[words1Start + i];
          --words1[words1Start + i];
          if (tmp != 0) {
            return 0;
          }
        }
        return 1;
      }
    }

    private static void TwosComplement(short[] words1, int words1Start, int n) {
      Decrement(words1, words1Start, n, (short)1);
      for (int i = 0; i < n; ++i) {
        words1[words1Start + i] = unchecked((short)(~words1[words1Start + i]));
      }
    }

    private static int Add(
      short[] c,
      int cstart,
      short[] words1,
      int astart,
      short[] words2,
      int bstart,
      int n) {
      // DebugAssert.IsTrue(n%2 == 0,"{0} line {1}: n%2 == 0","integer.cpp",799);
      unchecked {
        int u;
        u = 0;
        for (int i = 0; i < n; i += 2) {
          u = (((int)words1[astart + i]) & 0xffff) + (((int)words2[bstart + i]) & 0xffff) + (short)(u >> 16);
          c[cstart + i] = (short)u;
          u = (((int)words1[astart + i + 1]) & 0xffff) + (((int)words2[bstart + i + 1]) & 0xffff) + (short)(u >> 16);
          c[cstart + i + 1] = (short)u;
        }
        return ((int)u >> 16) & 0xffff;
      }
    }

    private static int AddOneByOne(
      short[] c,
      int cstart,
      short[] words1,
      int astart,
      short[] words2,
      int bstart,
      int n) {
      // DebugAssert.IsTrue(n%2 == 0,"{0} line {1}: n%2 == 0","integer.cpp",799);
      unchecked {
        int u;
        u = 0;
        for (int i = 0; i < n; i += 1) {
          u = (((int)words1[astart + i]) & 0xffff) + (((int)words2[bstart + i]) & 0xffff) + (short)(u >> 16);
          c[cstart + i] = (short)u;
        }
        return ((int)u >> 16) & 0xffff;
      }
    }

    private static int SubtractOneBiggerWords1(
      short[] c,
      int cstart,
      short[] words1,
      int astart,
      short[] words2,
      int bstart,
      int words1Count) {
      // Assumes that words2's count is 1 less
      unchecked {
        int u;
        u = 0;
        int cm1 = words1Count - 1;
        for (int i = 0; i < cm1; i += 1) {
          u = (((int)words1[astart]) & 0xffff) - (((int)words2[bstart]) & 0xffff) - (int)((u >> 31) & 1);
          c[cstart++] = (short)u;
          ++astart;
          ++bstart;
        }
        u = (((int)words1[astart]) & 0xffff) - (int)((u >> 31) & 1);
        c[cstart++] = (short)u;
        return (int)((u >> 31) & 1);
      }
    }

    private static int SubtractOneBiggerWords2(
      short[] c,
      int cstart,
      short[] words1,
      int astart,
      short[] words2,
      int bstart,
      int words2Count) {
      // Assumes that words1's count is 1 less
      unchecked {
        int u;
        u = 0;
        int cm1 = words2Count - 1;
        for (int i = 0; i < cm1; i += 1) {
          u = (((int)words1[astart]) & 0xffff) - (((int)words2[bstart]) & 0xffff) - (int)((u >> 31) & 1);
          c[cstart++] = (short)u;
          ++astart;
          ++bstart;
        }
        u = 0 - (((int)words2[bstart]) & 0xffff) - (int)((u >> 31) & 1);
        c[cstart++] = (short)u;
        return (int)((u >> 31) & 1);
      }
    }

    private static int AddUnevenSize(
      short[] c,
      int cstart,
      short[] wordsBigger,
      int astart,
      int acount,
      short[] wordsSmaller,
      int bstart,
      int bcount) {
      #if DEBUG
      if (acount < bcount) {
        throw new ArgumentException("acount (" + Convert.ToString((long)acount, System.Globalization.CultureInfo.InvariantCulture) + ") is less than " + Convert.ToString((long)bcount, System.Globalization.CultureInfo.InvariantCulture));
      }
      #endif
      unchecked {
        int u;
        u = 0;
        for (int i = 0; i < bcount; i += 1) {
          u = (((int)wordsBigger[astart + i]) & 0xffff) + (((int)wordsSmaller[bstart + i]) & 0xffff) + (short)(u >> 16);
          c[cstart + i] = (short)u;
        }
        for (int i = bcount; i < acount; i += 1) {
          u = (((int)wordsBigger[astart + i]) & 0xffff) + (short)(u >> 16);
          c[cstart + i] = (short)u;
        }
        return ((int)u >> 16) & 0xffff;
      }
    }

    private static int Subtract(
      short[] c,
      int cstart,
      short[] words1,
      int astart,
      short[] words2,
      int bstart,
      int n) {
      // DebugAssert.IsTrue(n%2 == 0,"{0} line {1}: n%2 == 0","integer.cpp",799);
      unchecked {
        int u;
        u = 0;
        for (int i = 0; i < n; i += 2) {
          u = (((int)words1[astart]) & 0xffff) - (((int)words2[bstart]) & 0xffff) - (int)((u >> 31) & 1);
          c[cstart++] = (short)u;
          ++astart;
          ++bstart;
          u = (((int)words1[astart]) & 0xffff) - (((int)words2[bstart]) & 0xffff) - (int)((u >> 31) & 1);
          c[cstart++] = (short)u;
          ++astart;
          ++bstart;
        }
        return (int)((u >> 31) & 1);
      }
    }

    private static int SubtractOneByOne(
      short[] c,
      int cstart,
      short[] words1,
      int astart,
      short[] words2,
      int bstart,
      int n) {
      // DebugAssert.IsTrue(n%2 == 0,"{0} line {1}: n%2 == 0","integer.cpp",799);
      unchecked {
        int u;
        u = 0;
        for (int i = 0; i < n; i += 1) {
          u = (((int)words1[astart]) & 0xffff) - (((int)words2[bstart]) & 0xffff) - (int)((u >> 31) & 1);
          c[cstart++] = (short)u;
          ++astart;
          ++bstart;
        }
        return (int)((u >> 31) & 1);
      }
    }

    private static short LinearMultiplyAdd(
      short[] productArr,
      int cstart,
      short[] words1,
      int astart,
      short words2,
      int n) {
      unchecked {
        short carry = 0;
        int bint = ((int)words2) & 0xffff;
        for (int i = 0; i < n; ++i) {
          int p;
          p = (((int)words1[astart + i]) & 0xffff) * bint;
          p += ((int)carry) & 0xffff;
          p += ((int)productArr[cstart + i]) & 0xffff;
          productArr[cstart + i] = (short)p;
          carry = (short)(p >> 16);
        }
        return carry;
      }
    }

    private static short LinearMultiply(
      short[] productArr,
      int cstart,
      short[] words1,
      int astart,
      short words2,
      int n) {
      unchecked {
        short carry = 0;
        int bint = ((int)words2) & 0xffff;
        for (int i = 0; i < n; ++i) {
          int p;
          p = (((int)words1[astart + i]) & 0xffff) * bint;
          p += ((int)carry) & 0xffff;
          productArr[cstart + i] = (short)p;
          carry = (short)(p >> 16);
        }
        return carry;
      }
    }
    //-----------------------------
    // Baseline Square
    //-----------------------------
    #region Baseline Square

    private static void Baseline_Square2(short[] result, int rstart, short[] words1, int astart) {
      unchecked {
        int p; short c; int d; int e;
        p = (((int)words1[astart]) & 0xffff) * (((int)words1[astart]) & 0xffff); result[rstart] = (short)p; e = ((int)p >> 16) & 0xffff;
        p = (((int)words1[astart]) & 0xffff) * (((int)words1[astart + 1]) & 0xffff); c = (short)p; d = ((int)p >> 16) & 0xffff; d = (int)((d << 1) + (((int)c >> 15) & 1)); c <<= 1;
        e += ((int)c) & 0xffff; c = (short)e; e = d + (((int)e >> 16) & 0xffff); result[rstart + 1] = c;
        p = (((int)words1[astart + 1]) & 0xffff) * (((int)words1[astart + 1]) & 0xffff);
        p += e; result[rstart + 2] = (short)p; result[rstart + 3] = (short)(p >> 16);
      }
    }

    private static void Baseline_Square4(short[] result, int rstart, short[] words1, int astart) {
      unchecked {
        int p; short c; int d; int e;
        p = (((int)words1[astart]) & 0xffff) * (((int)words1[astart]) & 0xffff); result[rstart] = (short)p; e = ((int)p >> 16) & 0xffff;
        p = (((int)words1[astart]) & 0xffff) * (((int)words1[astart + 1]) & 0xffff); c = (short)p; d = ((int)p >> 16) & 0xffff; d = (int)((d << 1) + (((int)c >> 15) & 1)); c <<= 1;
        e += ((int)c) & 0xffff; c = (short)e; e = d + (((int)e >> 16) & 0xffff); result[rstart + 1] = c;
        p = (((int)words1[astart]) & 0xffff) * (((int)words1[astart + 2]) & 0xffff); c = (short)p; d = ((int)p >> 16) & 0xffff; d = (int)((d << 1) + (((int)c >> 15) & 1)); c <<= 1;
        p = (((int)words1[astart + 1]) & 0xffff) * (((int)words1[astart + 1]) & 0xffff);
        p += ((int)c) & 0xffff; c = (short)p;
        d += ((int)p >> 16) & 0xffff;
        e += ((int)c) & 0xffff; c = (short)e; e = d + (((int)e >> 16) & 0xffff); result[rstart + 2] = c;
        p = (((int)words1[astart]) & 0xffff) * (((int)words1[astart + 3]) & 0xffff); c = (short)p; d = ((int)p >> 16) & 0xffff;
        p = (((int)words1[astart + 1]) & 0xffff) * (((int)words1[astart + 2]) & 0xffff);
        p += ((int)c) & 0xffff; c = (short)p;
        d += ((int)p >> 16) & 0xffff; d = (int)((d << 1) + (((int)c >> 15) & 1)); c <<= 1;
        e += ((int)c) & 0xffff; c = (short)e; e = d + (((int)e >> 16) & 0xffff); result[rstart + 3] = c;
        p = (((int)words1[astart + 1]) & 0xffff) * (((int)words1[astart + 3]) & 0xffff); c = (short)p; d = ((int)p >> 16) & 0xffff; d = (int)((d << 1) + (((int)c >> 15) & 1)); c <<= 1;
        p = (((int)words1[astart + 2]) & 0xffff) * (((int)words1[astart + 2]) & 0xffff);
        p += ((int)c) & 0xffff; c = (short)p;
        d += ((int)p >> 16) & 0xffff;
        e += ((int)c) & 0xffff; c = (short)e; e = d + (((int)e >> 16) & 0xffff); result[rstart + 4] = c;
        p = (((int)words1[astart + 2]) & 0xffff) * (((int)words1[astart + 3]) & 0xffff); c = (short)p; d = ((int)p >> 16) & 0xffff; d = (int)((d << 1) + (((int)c >> 15) & 1)); c <<= 1;
        e += ((int)c) & 0xffff; c = (short)e; e = d + (((int)e >> 16) & 0xffff); result[rstart + (2 * 4) - 3] = c;
        p = (((int)words1[astart + 3]) & 0xffff) * (((int)words1[astart + 3]) & 0xffff);
        p += e; result[rstart + 6] = (short)p; result[rstart + 7] = (short)(p >> 16);
      }
    }

    private static void Baseline_Square8(short[] result, int rstart, short[] words1, int astart) {
      unchecked {
        int p; short c; int d; int e;
        p = (((int)words1[astart]) & 0xffff) * (((int)words1[astart]) & 0xffff); result[rstart] = (short)p; e = ((int)p >> 16) & 0xffff;
        p = (((int)words1[astart]) & 0xffff) * (((int)words1[astart + 1]) & 0xffff); c = (short)p; d = ((int)p >> 16) & 0xffff; d = (int)((d << 1) + (((int)c >> 15) & 1)); c <<= 1;
        e += ((int)c) & 0xffff; c = (short)e; e = d + (((int)e >> 16) & 0xffff); result[rstart + 1] = c;
        p = (((int)words1[astart]) & 0xffff) * (((int)words1[astart + 2]) & 0xffff); c = (short)p; d = ((int)p >> 16) & 0xffff; d = (int)((d << 1) + (((int)c >> 15) & 1)); c <<= 1;
        p = (((int)words1[astart + 1]) & 0xffff) * (((int)words1[astart + 1]) & 0xffff);
        p += ((int)c) & 0xffff; c = (short)p;
        d += ((int)p >> 16) & 0xffff;
        e += ((int)c) & 0xffff; c = (short)e; e = d + (((int)e >> 16) & 0xffff); result[rstart + 2] = c;
        p = (((int)words1[astart]) & 0xffff) * (((int)words1[astart + 3]) & 0xffff); c = (short)p; d = ((int)p >> 16) & 0xffff;
        p = (((int)words1[astart + 1]) & 0xffff) * (((int)words1[astart + 2]) & 0xffff);
        p += ((int)c) & 0xffff; c = (short)p;
        d += ((int)p >> 16) & 0xffff; d = (int)((d << 1) + (((int)c >> 15) & 1)); c <<= 1;
        e += ((int)c) & 0xffff; c = (short)e; e = d + (((int)e >> 16) & 0xffff); result[rstart + 3] = c;
        p = (((int)words1[astart]) & 0xffff) * (((int)words1[astart + 4]) & 0xffff); c = (short)p; d = ((int)p >> 16) & 0xffff;
        p = (((int)words1[astart + 1]) & 0xffff) * (((int)words1[astart + 3]) & 0xffff);
        p += ((int)c) & 0xffff; c = (short)p;
        d += ((int)p >> 16) & 0xffff; d = (int)((d << 1) + (((int)c >> 15) & 1)); c <<= 1;
        p = (((int)words1[astart + 2]) & 0xffff) * (((int)words1[astart + 2]) & 0xffff);
        p += ((int)c) & 0xffff; c = (short)p;
        d += ((int)p >> 16) & 0xffff;
        e += ((int)c) & 0xffff; c = (short)e; e = d + (((int)e >> 16) & 0xffff); result[rstart + 4] = c;
        p = (((int)words1[astart]) & 0xffff) * (((int)words1[astart + 5]) & 0xffff); c = (short)p; d = ((int)p >> 16) & 0xffff;
        p = (((int)words1[astart + 1]) & 0xffff) * (((int)words1[astart + 4]) & 0xffff);
        p += ((int)c) & 0xffff; c = (short)p;
        d += ((int)p >> 16) & 0xffff;
        p = (((int)words1[astart + 2]) & 0xffff) * (((int)words1[astart + 3]) & 0xffff);
        p += ((int)c) & 0xffff; c = (short)p;
        d += ((int)p >> 16) & 0xffff; d = (int)((d << 1) + (((int)c >> 15) & 1)); c <<= 1;
        e += ((int)c) & 0xffff; c = (short)e; e = d + (((int)e >> 16) & 0xffff); result[rstart + 5] = c;
        p = (((int)words1[astart]) & 0xffff) * (((int)words1[astart + 6]) & 0xffff); c = (short)p; d = ((int)p >> 16) & 0xffff;
        p = (((int)words1[astart + 1]) & 0xffff) * (((int)words1[astart + 5]) & 0xffff);
        p += ((int)c) & 0xffff; c = (short)p;
        d += ((int)p >> 16) & 0xffff;
        p = (((int)words1[astart + 2]) & 0xffff) * (((int)words1[astart + 4]) & 0xffff);
        p += ((int)c) & 0xffff; c = (short)p;
        d += ((int)p >> 16) & 0xffff; d = (int)((d << 1) + (((int)c >> 15) & 1)); c <<= 1;
        p = (((int)words1[astart + 3]) & 0xffff) * (((int)words1[astart + 3]) & 0xffff);
        p += ((int)c) & 0xffff; c = (short)p;
        d += ((int)p >> 16) & 0xffff;
        e += ((int)c) & 0xffff; c = (short)e; e = d + (((int)e >> 16) & 0xffff); result[rstart + 6] = c;
        p = (((int)words1[astart]) & 0xffff) * (((int)words1[astart + 7]) & 0xffff); c = (short)p; d = ((int)p >> 16) & 0xffff;
        p = (((int)words1[astart + 1]) & 0xffff) * (((int)words1[astart + 6]) & 0xffff);
        p += ((int)c) & 0xffff; c = (short)p;
        d += ((int)p >> 16) & 0xffff;
        p = (((int)words1[astart + 2]) & 0xffff) * (((int)words1[astart + 5]) & 0xffff);
        p += ((int)c) & 0xffff; c = (short)p;
        d += ((int)p >> 16) & 0xffff;
        p = (((int)words1[astart + 3]) & 0xffff) * (((int)words1[astart + 4]) & 0xffff);
        p += ((int)c) & 0xffff; c = (short)p;
        d += ((int)p >> 16) & 0xffff; d = (int)((d << 1) + (((int)c >> 15) & 1)); c <<= 1;
        e += ((int)c) & 0xffff; c = (short)e; e = d + (((int)e >> 16) & 0xffff); result[rstart + 7] = c;
        p = (((int)words1[astart + 1]) & 0xffff) * (((int)words1[astart + 7]) & 0xffff); c = (short)p; d = ((int)p >> 16) & 0xffff;
        p = (((int)words1[astart + 2]) & 0xffff) * (((int)words1[astart + 6]) & 0xffff);
        p += ((int)c) & 0xffff; c = (short)p;
        d += ((int)p >> 16) & 0xffff;
        p = (((int)words1[astart + 3]) & 0xffff) * (((int)words1[astart + 5]) & 0xffff);
        p += ((int)c) & 0xffff; c = (short)p;
        d += ((int)p >> 16) & 0xffff; d = (int)((d << 1) + (((int)c >> 15) & 1)); c <<= 1;
        p = (((int)words1[astart + 4]) & 0xffff) * (((int)words1[astart + 4]) & 0xffff);
        p += ((int)c) & 0xffff; c = (short)p;
        d += ((int)p >> 16) & 0xffff;
        e += ((int)c) & 0xffff; c = (short)e; e = d + (((int)e >> 16) & 0xffff); result[rstart + 8] = c;
        p = (((int)words1[astart + 2]) & 0xffff) * (((int)words1[astart + 7]) & 0xffff); c = (short)p; d = ((int)p >> 16) & 0xffff;
        p = (((int)words1[astart + 3]) & 0xffff) * (((int)words1[astart + 6]) & 0xffff);
        p += ((int)c) & 0xffff; c = (short)p;
        d += ((int)p >> 16) & 0xffff;
        p = (((int)words1[astart + 4]) & 0xffff) * (((int)words1[astart + 5]) & 0xffff);
        p += ((int)c) & 0xffff; c = (short)p;
        d += ((int)p >> 16) & 0xffff; d = (int)((d << 1) + (((int)c >> 15) & 1)); c <<= 1;
        e += ((int)c) & 0xffff; c = (short)e; e = d + (((int)e >> 16) & 0xffff); result[rstart + 9] = c;
        p = (((int)words1[astart + 3]) & 0xffff) * (((int)words1[astart + 7]) & 0xffff); c = (short)p; d = ((int)p >> 16) & 0xffff;
        p = (((int)words1[astart + 4]) & 0xffff) * (((int)words1[astart + 6]) & 0xffff);
        p += ((int)c) & 0xffff; c = (short)p;
        d += ((int)p >> 16) & 0xffff; d = (int)((d << 1) + (((int)c >> 15) & 1)); c <<= 1;
        p = (((int)words1[astart + 5]) & 0xffff) * (((int)words1[astart + 5]) & 0xffff);
        p += ((int)c) & 0xffff; c = (short)p;
        d += ((int)p >> 16) & 0xffff;
        e += ((int)c) & 0xffff; c = (short)e; e = d + (((int)e >> 16) & 0xffff); result[rstart + 10] = c;
        p = (((int)words1[astart + 4]) & 0xffff) * (((int)words1[astart + 7]) & 0xffff); c = (short)p; d = ((int)p >> 16) & 0xffff;
        p = (((int)words1[astart + 5]) & 0xffff) * (((int)words1[astart + 6]) & 0xffff);
        p += ((int)c) & 0xffff; c = (short)p;
        d += ((int)p >> 16) & 0xffff; d = (int)((d << 1) + (((int)c >> 15) & 1)); c <<= 1;
        e += ((int)c) & 0xffff; c = (short)e; e = d + (((int)e >> 16) & 0xffff); result[rstart + 11] = c;
        p = (((int)words1[astart + 5]) & 0xffff) * (((int)words1[astart + 7]) & 0xffff); c = (short)p; d = ((int)p >> 16) & 0xffff; d = (int)((d << 1) + (((int)c >> 15) & 1)); c <<= 1;
        p = (((int)words1[astart + 6]) & 0xffff) * (((int)words1[astart + 6]) & 0xffff);
        p += ((int)c) & 0xffff; c = (short)p;
        d += ((int)p >> 16) & 0xffff;
        e += ((int)c) & 0xffff; c = (short)e; e = d + (((int)e >> 16) & 0xffff); result[rstart + 12] = c;
        p = (((int)words1[astart + 6]) & 0xffff) * (((int)words1[astart + 7]) & 0xffff); c = (short)p; d = ((int)p >> 16) & 0xffff; d = (int)((d << 1) + (((int)c >> 15) & 1)); c <<= 1;
        e += ((int)c) & 0xffff; c = (short)e; e = d + (((int)e >> 16) & 0xffff); result[rstart + 13] = c;
        p = (((int)words1[astart + 7]) & 0xffff) * (((int)words1[astart + 7]) & 0xffff);
        p += e; result[rstart + 14] = (short)p; result[rstart + 15] = (short)(p >> 16);
      }
    }
    #endregion
    //---------------------
    // Baseline multiply
    //---------------------
    #region Baseline Multiply

    private static void Baseline_Multiply2(short[] result, int rstart, short[] words1, int astart, short[] words2, int bstart) {
      unchecked {
        int p; short c; int d;
        int a0 = ((int)words1[astart]) & 0xffff;
        int a1 = ((int)words1[astart + 1]) & 0xffff;
        int b0 = ((int)words2[bstart]) & 0xffff;
        int b1 = ((int)words2[bstart + 1]) & 0xffff;
        p = a0 * b0; c = (short)p; d = ((int)p >> 16) & 0xffff; result[rstart] = c; c = (short)d; d = ((int)d >> 16) & 0xffff;
        p = a0 * b1;
        p += ((int)c) & 0xffff; c = (short)p;
        d += ((int)p >> 16) & 0xffff;
        p = a1 * b0;
        p += ((int)c) & 0xffff; c = (short)p;
        d += ((int)p >> 16) & 0xffff; result[rstart + 1] = c;
        p = a1 * b1;
        p += d; result[rstart + 2] = (short)p; result[rstart + 3] = (short)(p >> 16);
      }
    }

    private static void Baseline_Multiply4(short[] result, int rstart, short[] words1, int astart, short[] words2, int bstart) {
      int mask = 0xffff;
      unchecked {
        int p; short c; int d;
        int a0 = ((int)words1[astart]) & mask;
        int b0 = ((int)words2[bstart]) & mask;
        p = a0 * b0; c = (short)p; d = ((int)p >> 16) & mask; result[rstart] = c; c = (short)d; d = ((int)d >> 16) & mask;
        p = a0 * (((int)words2[bstart + 1]) & mask);
        p += ((int)c) & mask; c = (short)p;
        d += ((int)p >> 16) & mask;
        p = (((int)words1[astart + 1]) & mask) * b0;
        p += ((int)c) & mask; c = (short)p;
        d += ((int)p >> 16) & mask; result[rstart + 1] = c; c = (short)d; d = ((int)d >> 16) & mask;
        p = a0 * (((int)words2[bstart + 2]) & mask);

        p += ((int)c) & mask; c = (short)p;
        d += ((int)p >> 16) & mask;
        p = (((int)words1[astart + 1]) & mask) * (((int)words2[bstart + 1]) & mask);
        p += ((int)c) & mask; c = (short)p;
        d += ((int)p >> 16) & mask;
        p = (((int)words1[astart + 2]) & mask) * b0;
        p += ((int)c) & mask; c = (short)p;
        d += ((int)p >> 16) & mask; result[rstart + 2] = c; c = (short)d; d = ((int)d >> 16) & mask;
        p = a0 * (((int)words2[bstart + 3]) & mask);
        p += ((int)c) & mask; c = (short)p;
        d += ((int)p >> 16) & mask;
        p = (((int)words1[astart + 1]) & mask) * (((int)words2[bstart + 2]) & mask);
        p += ((int)c) & mask; c = (short)p;
        d += ((int)p >> 16) & mask;

        p = (((int)words1[astart + 2]) & mask) * (((int)words2[bstart + 1]) & mask);
        p += ((int)c) & mask; c = (short)p;
        d += ((int)p >> 16) & mask;
        p = (((int)words1[astart + 3]) & mask) * b0;
        p += ((int)c) & mask; c = (short)p;
        d += ((int)p >> 16) & mask; result[rstart + 3] = c; c = (short)d; d = ((int)d >> 16) & mask;
        p = (((int)words1[astart + 1]) & mask) * (((int)words2[bstart + 3]) & mask);
        p += ((int)c) & mask; c = (short)p;
        d += ((int)p >> 16) & mask;
        p = (((int)words1[astart + 2]) & mask) * (((int)words2[bstart + 2]) & mask);
        p += ((int)c) & mask; c = (short)p;
        d += ((int)p >> 16) & mask;
        p = (((int)words1[astart + 3]) & mask) * (((int)words2[bstart + 1]) & mask);
        p += ((int)c) & mask; c = (short)p;
        d += ((int)p >> 16) & mask; result[rstart + 4] = c; c = (short)d; d = ((int)d >> 16) & mask;
        p = (((int)words1[astart + 2]) & mask) * (((int)words2[bstart + 3]) & mask);
        p += ((int)c) & mask; c = (short)p;
        d += ((int)p >> 16) & mask;
        p = (((int)words1[astart + 3]) & mask) * (((int)words2[bstart + 2]) & mask);
        p += ((int)c) & mask; c = (short)p;
        d += ((int)p >> 16) & mask; result[rstart + 5] = c;
        p = (((int)words1[astart + 3]) & mask) * (((int)words2[bstart + 3]) & mask);
        p += d; result[rstart + 6] = (short)p; result[rstart + 7] = (short)(p >> 16);
      }
    }

    private static void Baseline_Multiply8(short[] result, int rstart, short[] words1, int astart, short[] words2, int bstart) {
      int mask = 0xffff;
      unchecked {
        int p; short c; int d;
        p = (((int)words1[astart]) & mask) * (((int)words2[bstart]) & mask); c = (short)p; d = ((int)p >> 16) & mask; result[rstart] = c; c = (short)d; d = ((int)d >> 16) & mask;
        p = (((int)words1[astart]) & mask) * (((int)words2[bstart + 1]) & mask);
        p += ((int)c) & mask; c = (short)p;
        d += ((int)p >> 16) & mask;
        p = (((int)words1[astart + 1]) & mask) * (((int)words2[bstart]) & mask);
        p += ((int)c) & mask; c = (short)p;
        d += ((int)p >> 16) & mask; result[rstart + 1] = c; c = (short)d; d = ((int)d >> 16) & mask;
        p = (((int)words1[astart]) & mask) * (((int)words2[bstart + 2]) & mask);
        p += ((int)c) & mask; c = (short)p;
        d += ((int)p >> 16) & mask;
        p = (((int)words1[astart + 1]) & mask) * (((int)words2[bstart + 1]) & mask);
        p += ((int)c) & mask; c = (short)p;
        d += ((int)p >> 16) & mask;
        p = (((int)words1[astart + 2]) & mask) * (((int)words2[bstart]) & mask);
        p += ((int)c) & mask; c = (short)p;
        d += ((int)p >> 16) & mask; result[rstart + 2] = c; c = (short)d; d = ((int)d >> 16) & mask;
        p = (((int)words1[astart]) & mask) * (((int)words2[bstart + 3]) & mask);
        p += ((int)c) & mask; c = (short)p;
        d += ((int)p >> 16) & mask;
        p = (((int)words1[astart + 1]) & mask) * (((int)words2[bstart + 2]) & mask);
        p += ((int)c) & mask; c = (short)p;
        d += ((int)p >> 16) & mask;
        p = (((int)words1[astart + 2]) & mask) * (((int)words2[bstart + 1]) & mask);
        p += ((int)c) & mask; c = (short)p;
        d += ((int)p >> 16) & mask;
        p = (((int)words1[astart + 3]) & mask) * (((int)words2[bstart]) & mask);
        p += ((int)c) & mask; c = (short)p;
        d += ((int)p >> 16) & mask; result[rstart + 3] = c; c = (short)d; d = ((int)d >> 16) & mask;
        p = (((int)words1[astart]) & mask) * (((int)words2[bstart + 4]) & mask);
        p += ((int)c) & mask; c = (short)p;
        d += ((int)p >> 16) & mask;
        p = (((int)words1[astart + 1]) & mask) * (((int)words2[bstart + 3]) & mask);
        p += ((int)c) & mask; c = (short)p;
        d += ((int)p >> 16) & mask;
        p = (((int)words1[astart + 2]) & mask) * (((int)words2[bstart + 2]) & mask);
        p += ((int)c) & mask; c = (short)p;
        d += ((int)p >> 16) & mask;
        p = (((int)words1[astart + 3]) & mask) * (((int)words2[bstart + 1]) & mask);
        p += ((int)c) & mask; c = (short)p;
        d += ((int)p >> 16) & mask;
        p = (((int)words1[astart + 4]) & mask) * (((int)words2[bstart]) & mask);
        p += ((int)c) & mask; c = (short)p;
        d += ((int)p >> 16) & mask; result[rstart + 4] = c; c = (short)d; d = ((int)d >> 16) & mask;
        p = (((int)words1[astart]) & mask) * (((int)words2[bstart + 5]) & mask);
        p += ((int)c) & mask; c = (short)p;
        d += ((int)p >> 16) & mask;
        p = (((int)words1[astart + 1]) & mask) * (((int)words2[bstart + 4]) & mask);
        p += ((int)c) & mask; c = (short)p;
        d += ((int)p >> 16) & mask;
        p = (((int)words1[astart + 2]) & mask) * (((int)words2[bstart + 3]) & mask);
        p += ((int)c) & mask; c = (short)p;
        d += ((int)p >> 16) & mask;
        p = (((int)words1[astart + 3]) & mask) * (((int)words2[bstart + 2]) & mask);
        p += ((int)c) & mask; c = (short)p;
        d += ((int)p >> 16) & mask;
        p = (((int)words1[astart + 4]) & mask) * (((int)words2[bstart + 1]) & mask);
        p += ((int)c) & mask; c = (short)p;
        d += ((int)p >> 16) & mask;
        p = (((int)words1[astart + 5]) & mask) * (((int)words2[bstart]) & mask);
        p += ((int)c) & mask; c = (short)p;
        d += ((int)p >> 16) & mask; result[rstart + 5] = c; c = (short)d; d = ((int)d >> 16) & mask;
        p = (((int)words1[astart]) & mask) * (((int)words2[bstart + 6]) & mask);
        p += ((int)c) & mask; c = (short)p;
        d += ((int)p >> 16) & mask;
        p = (((int)words1[astart + 1]) & mask) * (((int)words2[bstart + 5]) & mask);
        p += ((int)c) & mask; c = (short)p;
        d += ((int)p >> 16) & mask;
        p = (((int)words1[astart + 2]) & mask) * (((int)words2[bstart + 4]) & mask);
        p += ((int)c) & mask; c = (short)p;
        d += ((int)p >> 16) & mask;
        p = (((int)words1[astart + 3]) & mask) * (((int)words2[bstart + 3]) & mask);
        p += ((int)c) & mask; c = (short)p;
        d += ((int)p >> 16) & mask;
        p = (((int)words1[astart + 4]) & mask) * (((int)words2[bstart + 2]) & mask);
        p += ((int)c) & mask; c = (short)p;
        d += ((int)p >> 16) & mask;
        p = (((int)words1[astart + 5]) & mask) * (((int)words2[bstart + 1]) & mask);
        p += ((int)c) & mask; c = (short)p;
        d += ((int)p >> 16) & mask;
        p = (((int)words1[astart + 6]) & mask) * (((int)words2[bstart]) & mask);
        p += ((int)c) & mask; c = (short)p;
        d += ((int)p >> 16) & mask; result[rstart + 6] = c; c = (short)d; d = ((int)d >> 16) & mask;
        p = (((int)words1[astart]) & mask) * (((int)words2[bstart + 7]) & mask);
        p += ((int)c) & mask; c = (short)p;
        d += ((int)p >> 16) & mask;
        p = (((int)words1[astart + 1]) & mask) * (((int)words2[bstart + 6]) & mask);
        p += ((int)c) & mask; c = (short)p;
        d += ((int)p >> 16) & mask;
        p = (((int)words1[astart + 2]) & mask) * (((int)words2[bstart + 5]) & mask);
        p += ((int)c) & mask; c = (short)p;
        d += ((int)p >> 16) & mask;
        p = (((int)words1[astart + 3]) & mask) * (((int)words2[bstart + 4]) & mask);
        p += ((int)c) & mask; c = (short)p;
        d += ((int)p >> 16) & mask;
        p = (((int)words1[astart + 4]) & mask) * (((int)words2[bstart + 3]) & mask);
        p += ((int)c) & mask; c = (short)p;
        d += ((int)p >> 16) & mask;
        p = (((int)words1[astart + 5]) & mask) * (((int)words2[bstart + 2]) & mask);
        p += ((int)c) & mask; c = (short)p;
        d += ((int)p >> 16) & mask;
        p = (((int)words1[astart + 6]) & mask) * (((int)words2[bstart + 1]) & mask);
        p += ((int)c) & mask; c = (short)p;
        d += ((int)p >> 16) & mask;
        p = (((int)words1[astart + 7]) & mask) * (((int)words2[bstart]) & mask);
        p += ((int)c) & mask; c = (short)p;
        d += ((int)p >> 16) & mask; result[rstart + 7] = c; c = (short)d; d = ((int)d >> 16) & mask;
        p = (((int)words1[astart + 1]) & mask) * (((int)words2[bstart + 7]) & mask);
        p += ((int)c) & mask; c = (short)p;
        d += ((int)p >> 16) & mask;
        p = (((int)words1[astart + 2]) & mask) * (((int)words2[bstart + 6]) & mask);
        p += ((int)c) & mask; c = (short)p;
        d += ((int)p >> 16) & mask;
        p = (((int)words1[astart + 3]) & mask) * (((int)words2[bstart + 5]) & mask);
        p += ((int)c) & mask; c = (short)p;
        d += ((int)p >> 16) & mask;
        p = (((int)words1[astart + 4]) & mask) * (((int)words2[bstart + 4]) & mask);
        p += ((int)c) & mask; c = (short)p;
        d += ((int)p >> 16) & mask;
        p = (((int)words1[astart + 5]) & mask) * (((int)words2[bstart + 3]) & mask);
        p += ((int)c) & mask; c = (short)p;
        d += ((int)p >> 16) & mask;
        p = (((int)words1[astart + 6]) & mask) * (((int)words2[bstart + 2]) & mask);
        p += ((int)c) & mask; c = (short)p;
        d += ((int)p >> 16) & mask;
        p = (((int)words1[astart + 7]) & mask) * (((int)words2[bstart + 1]) & mask);
        p += ((int)c) & mask; c = (short)p;
        d += ((int)p >> 16) & mask; result[rstart + 8] = c; c = (short)d; d = ((int)d >> 16) & mask;
        p = (((int)words1[astart + 2]) & mask) * (((int)words2[bstart + 7]) & mask);
        p += ((int)c) & mask; c = (short)p;
        d += ((int)p >> 16) & mask;
        p = (((int)words1[astart + 3]) & mask) * (((int)words2[bstart + 6]) & mask);
        p += ((int)c) & mask; c = (short)p;
        d += ((int)p >> 16) & mask;
        p = (((int)words1[astart + 4]) & mask) * (((int)words2[bstart + 5]) & mask);
        p += ((int)c) & mask; c = (short)p;
        d += ((int)p >> 16) & mask;
        p = (((int)words1[astart + 5]) & mask) * (((int)words2[bstart + 4]) & mask);
        p += ((int)c) & mask; c = (short)p;
        d += ((int)p >> 16) & mask;
        p = (((int)words1[astart + 6]) & mask) * (((int)words2[bstart + 3]) & mask);
        p += ((int)c) & mask; c = (short)p;
        d += ((int)p >> 16) & mask;
        p = (((int)words1[astart + 7]) & mask) * (((int)words2[bstart + 2]) & mask);
        p += ((int)c) & mask; c = (short)p;
        d += ((int)p >> 16) & mask; result[rstart + 9] = c; c = (short)d; d = ((int)d >> 16) & mask;
        p = (((int)words1[astart + 3]) & mask) * (((int)words2[bstart + 7]) & mask);
        p += ((int)c) & mask; c = (short)p;
        d += ((int)p >> 16) & mask;
        p = (((int)words1[astart + 4]) & mask) * (((int)words2[bstart + 6]) & mask);
        p += ((int)c) & mask; c = (short)p;
        d += ((int)p >> 16) & mask;
        p = (((int)words1[astart + 5]) & mask) * (((int)words2[bstart + 5]) & mask);
        p += ((int)c) & mask; c = (short)p;
        d += ((int)p >> 16) & mask;
        p = (((int)words1[astart + 6]) & mask) * (((int)words2[bstart + 4]) & mask);
        p += ((int)c) & mask; c = (short)p;
        d += ((int)p >> 16) & mask;
        p = (((int)words1[astart + 7]) & mask) * (((int)words2[bstart + 3]) & mask);
        p += ((int)c) & mask; c = (short)p;
        d += ((int)p >> 16) & mask; result[rstart + 10] = c; c = (short)d; d = ((int)d >> 16) & mask;
        p = (((int)words1[astart + 4]) & mask) * (((int)words2[bstart + 7]) & mask);
        p += ((int)c) & mask; c = (short)p;
        d += ((int)p >> 16) & mask;
        p = (((int)words1[astart + 5]) & mask) * (((int)words2[bstart + 6]) & mask);
        p += ((int)c) & mask; c = (short)p;
        d += ((int)p >> 16) & mask;
        p = (((int)words1[astart + 6]) & mask) * (((int)words2[bstart + 5]) & mask);
        p += ((int)c) & mask; c = (short)p;
        d += ((int)p >> 16) & mask;
        p = (((int)words1[astart + 7]) & mask) * (((int)words2[bstart + 4]) & mask);
        p += ((int)c) & mask; c = (short)p;
        d += ((int)p >> 16) & mask; result[rstart + 11] = c; c = (short)d; d = ((int)d >> 16) & mask;
        p = (((int)words1[astart + 5]) & mask) * (((int)words2[bstart + 7]) & mask);
        p += ((int)c) & mask; c = (short)p;
        d += ((int)p >> 16) & mask;
        p = (((int)words1[astart + 6]) & mask) * (((int)words2[bstart + 6]) & mask);
        p += ((int)c) & mask; c = (short)p;
        d += ((int)p >> 16) & mask;
        p = (((int)words1[astart + 7]) & mask) * (((int)words2[bstart + 5]) & mask);
        p += ((int)c) & mask; c = (short)p;
        d += ((int)p >> 16) & mask; result[rstart + 12] = c; c = (short)d; d = ((int)d >> 16) & mask;
        p = (((int)words1[astart + 6]) & mask) * (((int)words2[bstart + 7]) & mask);
        p += ((int)c) & mask; c = (short)p;
        d += ((int)p >> 16) & mask;
        p = (((int)words1[astart + 7]) & mask) * (((int)words2[bstart + 6]) & mask);
        p += ((int)c) & mask; c = (short)p;
        d += ((int)p >> 16) & mask; result[rstart + 13] = c;
        p = (((int)words1[astart + 7]) & mask) * (((int)words2[bstart + 7]) & mask);
        p += d; result[rstart + 14] = (short)p; result[rstart + 15] = (short)(p >> 16);
      }
    }

    #endregion
    private const int RecursionLimit = 10;

    // NOTE: Renamed from RecursiveMultiply to better show that
    // this function only takes operands of the same size, as opposed
    // to AsymmetricMultiply.
    private static void SameSizeMultiply(
      short[] resultArr,  // size 2*count
      int resultStart,
      short[] tempArr,  // size 2*count
      int tempStart,
      short[] words1,
      int words1Start,  // size count
      short[] words2,
      int words2Start,  // size count
      int count) {
      // Console.WriteLine("RecursiveMultiply " + count + " " + count + " [r=" + resultStart + " t=" + tempStart + " a=" + words1Start + " b=" + words2Start + "]");
      #if DEBUG
      if (resultArr == null) {
        throw new ArgumentNullException("resultArr");
      }

      if (resultStart < 0) {
        throw new ArgumentException("resultStart (" + Convert.ToString((long)resultStart, System.Globalization.CultureInfo.InvariantCulture) + ") is less than " + "0");
      }

      if (resultStart > resultArr.Length) {
        throw new ArgumentException("resultStart (" + Convert.ToString((long)resultStart, System.Globalization.CultureInfo.InvariantCulture) + ") is more than " + Convert.ToString((long)resultArr.Length, System.Globalization.CultureInfo.InvariantCulture));
      }

      if (count + count < 0) {
        throw new ArgumentException("count plus count (" + Convert.ToString((long)count + count, System.Globalization.CultureInfo.InvariantCulture) + ") is less than " + "0");
      }

      if (count + count > resultArr.Length) {
        throw new ArgumentException("count plus count (" + Convert.ToString((long)count + count, System.Globalization.CultureInfo.InvariantCulture) + ") is more than " + Convert.ToString((long)resultArr.Length, System.Globalization.CultureInfo.InvariantCulture));
      }

      if (resultArr.Length - resultStart < count + count) {
        throw new ArgumentException("resultArr.Length minus resultStart (" + Convert.ToString((long)resultArr.Length - resultStart, System.Globalization.CultureInfo.InvariantCulture) + ") is less than " + Convert.ToString((long)count + count, System.Globalization.CultureInfo.InvariantCulture));
      }

      if (tempArr == null) {
        throw new ArgumentNullException("tempArr");
      }

      if (tempStart < 0) {
        throw new ArgumentException("tempStart (" + Convert.ToString((long)tempStart, System.Globalization.CultureInfo.InvariantCulture) + ") is less than " + "0");
      }

      if (tempStart > tempArr.Length) {
        throw new ArgumentException("tempStart (" + Convert.ToString((long)tempStart, System.Globalization.CultureInfo.InvariantCulture) + ") is more than " + Convert.ToString((long)tempArr.Length, System.Globalization.CultureInfo.InvariantCulture));
      }

      if (count + count < 0) {
        throw new ArgumentException("count plus count (" + Convert.ToString((long)count + count, System.Globalization.CultureInfo.InvariantCulture) + ") is less than " + "0");
      }

      if (count + count > tempArr.Length) {
        throw new ArgumentException("count plus count (" + Convert.ToString((long)count + count, System.Globalization.CultureInfo.InvariantCulture) + ") is more than " + Convert.ToString((long)tempArr.Length, System.Globalization.CultureInfo.InvariantCulture));
      }

      if (tempArr.Length - tempStart < count + count) {
        throw new ArgumentException("tempArr.Length minus tempStart (" + Convert.ToString((long)tempArr.Length - tempStart, System.Globalization.CultureInfo.InvariantCulture) + ") is less than " + Convert.ToString((long)count + count, System.Globalization.CultureInfo.InvariantCulture));
      }

      if (words1 == null) {
        throw new ArgumentNullException("words1");
      }

      if (words1Start < 0) {
        throw new ArgumentException("words1Start (" + Convert.ToString((long)words1Start, System.Globalization.CultureInfo.InvariantCulture) + ") is less than " + "0");
      }

      if (words1Start > words1.Length) {
        throw new ArgumentException("words1Start (" + Convert.ToString((long)words1Start, System.Globalization.CultureInfo.InvariantCulture) + ") is more than " + Convert.ToString((long)words1.Length, System.Globalization.CultureInfo.InvariantCulture));
      }

      if (count < 0) {
        throw new ArgumentException("count (" + Convert.ToString((long)count, System.Globalization.CultureInfo.InvariantCulture) + ") is less than " + "0");
      }

      if (count > words1.Length) {
        throw new ArgumentException("count (" + Convert.ToString((long)count, System.Globalization.CultureInfo.InvariantCulture) + ") is more than " + Convert.ToString((long)words1.Length, System.Globalization.CultureInfo.InvariantCulture));
      }

      if (words1.Length - words1Start < count) {
        throw new ArgumentException("words1.Length minus words1Start (" + Convert.ToString((long)words1.Length - words1Start, System.Globalization.CultureInfo.InvariantCulture) + ") is less than " + Convert.ToString((long)count, System.Globalization.CultureInfo.InvariantCulture));
      }

      if (words2 == null) {
        throw new ArgumentNullException("words2");
      }

      if (words2Start < 0) {
        throw new ArgumentException("words2Start (" + Convert.ToString((long)words2Start, System.Globalization.CultureInfo.InvariantCulture) + ") is less than " + "0");
      }

      if (words2Start > words2.Length) {
        throw new ArgumentException("words2Start (" + Convert.ToString((long)words2Start, System.Globalization.CultureInfo.InvariantCulture) + ") is more than " + Convert.ToString((long)words2.Length, System.Globalization.CultureInfo.InvariantCulture));
      }

      if (count < 0) {
        throw new ArgumentException("count (" + Convert.ToString((long)count, System.Globalization.CultureInfo.InvariantCulture) + ") is less than " + "0");
      }

      if (count > words2.Length) {
        throw new ArgumentException("count (" + Convert.ToString((long)count, System.Globalization.CultureInfo.InvariantCulture) + ") is more than " + Convert.ToString((long)words2.Length, System.Globalization.CultureInfo.InvariantCulture));
      }

      if (words2.Length - words2Start < count) {
        throw new ArgumentException("words2.Length minus words2Start (" + Convert.ToString((long)words2.Length - words2Start, System.Globalization.CultureInfo.InvariantCulture) + ") is less than " + Convert.ToString((long)count, System.Globalization.CultureInfo.InvariantCulture));
      }
      #endif

      if (count <= RecursionLimit) {
        if (count == 2) {
          Baseline_Multiply2(resultArr, resultStart, words1, words1Start, words2, words2Start);
        } else if (count == 4) {
          Baseline_Multiply4(resultArr, resultStart, words1, words1Start, words2, words2Start);
        } else if (count == 8) {
          Baseline_Multiply8(resultArr, resultStart, words1, words1Start, words2, words2Start);
        } else {
          SchoolbookMultiply(resultArr, resultStart, words1, words1Start, count, words2, words2Start, count);
        }
      } else {
        int countA = count;
        while (countA != 0 && words1[words1Start + countA - 1] == 0) {
          --countA;
        }
        int countB = count;
        while (countB != 0 && words2[words2Start + countB - 1] == 0) {
          --countB;
        }
        int offset2For1 = 0;
        int offset2For2 = 0;
        if (countA == 0 || countB == 0) {
          // words1 or words2 is empty, so result is 0
          Array.Clear((short[])resultArr, resultStart, count << 1);
          return;
        }
        // Split words1 and words2 in two parts each
        if ((count & 1) == 0) {
          int count2 = count >> 1;
          if (countA <= count2 && countB <= count2) {
            // Console.WriteLine("Can be smaller: " + AN + "," + BN + "," + (count2));
            Array.Clear((short[])resultArr, resultStart + count, count);
            if (count2 == 8) {
              Baseline_Multiply8(resultArr, resultStart, words1, words1Start, words2, words2Start);
            } else {
              SameSizeMultiply(resultArr, resultStart, tempArr, tempStart, words1, words1Start, words2, words2Start, count2);
            }
            return;
          }
          int resultMediumHigh = resultStart + count;
          int resultHigh = resultMediumHigh + count2;
          int resultMediumLow = resultStart + count2;
          int tsn = tempStart + count;
          offset2For1 = Compare(words1, words1Start, words1, words1Start + count2, count2) > 0 ? 0 : count2;
          // Absolute value of low part minus high part of words1
          SubtractOneByOne(resultArr, resultStart, words1, words1Start + offset2For1, words1, (int)(words1Start + (count2 ^ offset2For1)), count2);
          offset2For2 = Compare(words2, words2Start, words2, (int)(words2Start + count2), count2) > 0 ? 0 : count2;
          // Absolute value of low part minus high part of words2
          SubtractOneByOne(resultArr, resultMediumLow, words2, words2Start + offset2For2, words2, (int)(words2Start + (count2 ^ offset2For2)), count2);
          //---------
          // HighA * HighB
          SameSizeMultiply(resultArr, resultMediumHigh, tempArr, tsn, words1, (int)(words1Start + count2), words2, (int)(words2Start + count2), count2);
          // Medium high result = Abs(LowA-HighA) * Abs(LowB-HighB)
          SameSizeMultiply(tempArr, tempStart, tempArr, tsn, resultArr, resultStart, resultArr, (int)resultMediumLow, count2);
          // Low result = LowA * LowB
          SameSizeMultiply(resultArr, resultStart, tempArr, tsn, words1, words1Start, words2, words2Start, count2);
          int c2 = AddOneByOne(resultArr, resultMediumHigh, resultArr, resultMediumHigh, resultArr, resultMediumLow, count2);
          int c3 = c2;
          c2 += AddOneByOne(resultArr, resultMediumLow, resultArr, resultMediumHigh, resultArr, resultStart, count2);
          c3 += AddOneByOne(resultArr, resultMediumHigh, resultArr, resultMediumHigh, resultArr, resultHigh, count2);
          if (offset2For1 == offset2For2) {
            c3 -= SubtractOneByOne(resultArr, resultMediumLow, resultArr, resultMediumLow, tempArr, tempStart, count);
          } else {
            c3 += AddOneByOne(resultArr, resultMediumLow, resultArr, resultMediumLow, tempArr, tempStart, count);
          }
          c3 += Increment(resultArr, resultMediumHigh, count2, (short)c2);
          // DebugWords(resultArr,resultStart,count*2,"p6");
          if (c3 != 0) {
            Increment(resultArr, resultHigh, count2, (short)c3);
          }
          // DebugWords(resultArr,resultStart,count*2,"p7");
        } else {
          // Count is odd, high part will be 1 shorter
          int countHigh = count >> 1;  // Shorter part
          int countLow = count - countHigh;  // Longer part
          offset2For1 = CompareWithOneBiggerWords1(words1, words1Start, words1, words1Start + countLow, countLow) > 0 ? 0 : countLow;
          if (offset2For1 == 0) {
            SubtractOneBiggerWords1(resultArr, resultStart, words1, words1Start, words1, words1Start + countLow, countLow);
          } else {
            SubtractOneBiggerWords2(resultArr, resultStart, words1, words1Start + countLow, words1, words1Start, countLow);
          }
          offset2For2 = CompareWithOneBiggerWords1(words2, words2Start, words2, words2Start + countLow, countLow) > 0 ? 0 : countLow;
          if (offset2For2 == 0) {
            SubtractOneBiggerWords1(tempArr, tempStart, words2, words2Start, words2, words2Start + countLow, countLow);
          } else {
            SubtractOneBiggerWords2(tempArr, tempStart, words2, words2Start + countLow, words2, words2Start, countLow);
          }
          // Abs(LowA-HighA) * Abs(LowB-HighB)
          int shorterOffset = countHigh << 1;
          int longerOffset = countLow << 1;
          SameSizeMultiply(
            tempArr,
            tempStart + shorterOffset,
            resultArr,
            resultStart + shorterOffset,
            resultArr,
            resultStart,
            tempArr,
            tempStart,
            countLow);
          // DebugWords(resultArr, resultStart+shorterOffset,countLow << 1,"w1*w2");
          short resultTmp0 = tempArr[tempStart + shorterOffset];
          short resultTmp1 = tempArr[tempStart + shorterOffset + 1];
          // HighA * HighB
          SameSizeMultiply(
            resultArr,
            resultStart + longerOffset,
            resultArr,
            resultStart,
            words1,
            words1Start + countLow,
            words2,
            words2Start + countLow,
            countHigh);
          // LowA * LowB
          SameSizeMultiply(
            resultArr,
            resultStart,
            tempArr,
            tempStart,
            words1,
            words1Start,
            words2,
            words2Start,
            countLow);
          tempArr[tempStart + shorterOffset] = resultTmp0;
          tempArr[tempStart + shorterOffset + 1] = resultTmp1;
          int countMiddle = countLow << 1;
          // DebugWords(resultArr,resultStart,count*2,"q1");
          int c2 = AddOneByOne(resultArr, resultStart + countMiddle, resultArr, resultStart + countMiddle, resultArr, resultStart + countLow, countLow);
          int c3 = c2;
          // DebugWords(resultArr,resultStart,count*2,"q2");
          c2 += AddOneByOne(resultArr, resultStart + countLow, resultArr, resultStart + countMiddle, resultArr, resultStart, countLow);
          // DebugWords(resultArr,resultStart,count*2,"q3");
          c3 += AddUnevenSize(
            resultArr,
            resultStart + countMiddle,
            resultArr,
            resultStart + countMiddle,
            countLow,
            resultArr,
            resultStart + countMiddle + countLow,
            countLow - 2);
          // DebugWords(resultArr,resultStart,count*2,"q4");
          if (offset2For1 == offset2For2) {
            c3 -= SubtractOneByOne(resultArr, resultStart + countLow, resultArr, resultStart + countLow, tempArr, tempStart + shorterOffset, countLow << 1);
          } else {
            c3 += AddOneByOne(resultArr, resultStart + countLow, resultArr, resultStart + countLow, tempArr, tempStart + shorterOffset, countLow << 1);
          }
          // DebugWords(resultArr,resultStart,count*2,"q5");
          c3 += Increment(resultArr, resultStart + countMiddle, countLow, (short)c2);
          // DebugWords(resultArr,resultStart,count*2,"q6");
          if (c3 != 0) {
            Increment(resultArr, resultStart + countMiddle + countLow, countLow - 2, (short)c3);
          }
          // DebugWords(resultArr,resultStart,count*2,"q7");
        }
      }
    }

    private static void RecursiveSquare(
      short[] resultArr,
      int resultStart,
      short[] tempArr,
      int tempStart,
      short[] words1,
      int words1Start,
      int count) {
      if (count <= RecursionLimit) {
        if (count == 2) {
          Baseline_Square2(resultArr, resultStart, words1, words1Start);
        } else if (count == 4) {
          Baseline_Square4(resultArr, resultStart, words1, words1Start);
        } else if (count == 8) {
          Baseline_Square8(resultArr, resultStart, words1, words1Start);
        } else {
          SchoolbookSquare(resultArr, resultStart, words1, words1Start, count);
        }
      } else if ((count & 1) == 0) {
        int count2 = count >> 1;
        RecursiveSquare(resultArr, resultStart, tempArr, tempStart + count, words1, words1Start, count2);
        RecursiveSquare(resultArr, resultStart + count, tempArr, tempStart + count, words1, words1Start + count2, count2);
        SameSizeMultiply(
          tempArr,
          tempStart,
          tempArr,
          tempStart + count,
          words1,
          words1Start,
          words1,
          words1Start + count2,
          count2);
        int carry = AddOneByOne(resultArr, (int)(resultStart + count2), resultArr, (int)(resultStart + count2), tempArr, tempStart, count);
        carry += AddOneByOne(resultArr, (int)(resultStart + count2), resultArr, (int)(resultStart + count2), tempArr, tempStart, count);
        Increment(resultArr, (int)(resultStart + count + count2), count2, (short)carry);
      } else {
        SameSizeMultiply(resultArr, resultStart, tempArr, tempStart, words1, words1Start, words1, words1Start, count);
      }
    }

    private static void SchoolbookSquare(
      short[] resultArr,
      int resultStart,
      short[] words1,
      int words1Start,
      int words1Count) {
      // Method assumes that resultArr was already zeroed,
      // if resultArr is the same as words1
      int cstart;
      for (int i = 0; i < words1Count; ++i) {
        cstart = resultStart + i;
        unchecked {
          short carry = 0;
          int valueBint = ((int)words1[words1Start + i]) & 0xffff;
          for (int j = 0; j < words1Count; ++j) {
            int p;
            p = (((int)words1[words1Start + j]) & 0xffff) * valueBint;
            p += ((int)carry) & 0xffff;
            if (i != 0) {
              p += ((int)resultArr[cstart + j]) & 0xffff;
            }
            resultArr[cstart + j] = (short)p;
            carry = (short)(p >> 16);
          }
          resultArr[cstart + words1Count] = carry;
        }
      }
    }

    private static void SchoolbookMultiply(
      short[] resultArr,
      int resultStart,
      short[] words1,
      int words1Start,
      int words1Count,
      short[] words2,
      int words2Start,
      int words2Count) {
      // Method assumes that resultArr was already zeroed,
      // if resultArr is the same as words1 or words2
      int cstart;
      if (words1Count < words2Count) {
        // words1 is shorter than words2, so put words2 on top
        for (int i = 0; i < words1Count; ++i) {
          cstart = resultStart + i;
          unchecked {
            short carry = 0;
            int valueBint = ((int)words1[words1Start + i]) & 0xffff;
            for (int j = 0; j < words2Count; ++j) {
              int p;
              p = (((int)words2[words2Start + j]) & 0xffff) * valueBint;
              p += ((int)carry) & 0xffff;
              if (i != 0) {
                p += ((int)resultArr[cstart + j]) & 0xffff;
              }
              resultArr[cstart + j] = (short)p;
              carry = (short)(p >> 16);
            }
            resultArr[cstart + words2Count] = carry;
          }
        }
      } else {
        // words2 is shorter than words1
        for (int i = 0; i < words2Count; ++i) {
          cstart = resultStart + i;
          unchecked {
            short carry = 0;
            int valueBint = ((int)words2[words2Start + i]) & 0xffff;
            for (int j = 0; j < words1Count; ++j) {
              int p;
              p = (((int)words1[words1Start + j]) & 0xffff) * valueBint;
              p += ((int)carry) & 0xffff;
              if (i != 0) {
                p += ((int)resultArr[cstart + j]) & 0xffff;
              }
              resultArr[cstart + j] = (short)p;
              carry = (short)(p >> 16);
            }
            resultArr[cstart + words1Count] = carry;
          }
        }
      }
    }
    /*
    private static void DebugWords(short[] a, int astart, int count, string msg) {
      Console.Write("Words(" + msg + "): ");
      for (int i = 0; i < count; ++i) {
        Console.Write("{0:X4} ", a[astart + i]);
      }
      Console.WriteLine(String.Empty);
      BigInteger bi = new BigInteger();
      bi.reg = new short[count];
      bi.wordCount = count;
      Array.Copy(a, astart, bi.reg, 0, count);
      Console.WriteLine("Value(" + msg + "): " + bi);
    }
     */
    private static void ChunkedLinearMultiply(
      short[] productArr,
      int cstart,
      short[] tempArr,
      int tempStart,  // uses bcount*4 space
      short[] words1,
      int astart,
      int acount,  // Equal size or longer
      short[] words2,
      int bstart,
      int bcount) {
      #if DEBUG
      if (acount < bcount) {
        throw new ArgumentException("acount (" + Convert.ToString((long)acount, System.Globalization.CultureInfo.InvariantCulture) + ") is less than " + Convert.ToString((long)bcount, System.Globalization.CultureInfo.InvariantCulture));
      }

      if (productArr == null) {
        throw new ArgumentNullException("productArr");
      }

      if (cstart < 0) {
        throw new ArgumentException("cstart (" + Convert.ToString((long)cstart, System.Globalization.CultureInfo.InvariantCulture) + ") is less than " + "0");
      }

      if (cstart > productArr.Length) {
        throw new ArgumentException("cstart (" + Convert.ToString((long)cstart, System.Globalization.CultureInfo.InvariantCulture) + ") is more than " + Convert.ToString((long)productArr.Length, System.Globalization.CultureInfo.InvariantCulture));
      }

      if (acount + bcount < 0) {
        throw new ArgumentException("acount plus bcount (" + Convert.ToString((long)acount + bcount, System.Globalization.CultureInfo.InvariantCulture) + ") is less than " + "0");
      }

      if (acount + bcount > productArr.Length) {
        throw new ArgumentException("acount plus bcount (" + Convert.ToString((long)acount + bcount, System.Globalization.CultureInfo.InvariantCulture) + ") is more than " + Convert.ToString((long)productArr.Length, System.Globalization.CultureInfo.InvariantCulture));
      }

      if (productArr.Length - cstart < acount + bcount) {
        throw new ArgumentException("productArr.Length minus cstart (" + Convert.ToString((long)productArr.Length - cstart, System.Globalization.CultureInfo.InvariantCulture) + ") is less than " + Convert.ToString((long)acount + bcount, System.Globalization.CultureInfo.InvariantCulture));
      }

      if (tempArr == null) {
        throw new ArgumentNullException("tempArr");
      }

      if (tempStart < 0) {
        throw new ArgumentException("tempStart (" + Convert.ToString((long)tempStart, System.Globalization.CultureInfo.InvariantCulture) + ") is less than " + "0");
      }

      if (tempStart > tempArr.Length) {
        throw new ArgumentException("tempStart (" + Convert.ToString((long)tempStart, System.Globalization.CultureInfo.InvariantCulture) + ") is more than " + Convert.ToString((long)tempArr.Length, System.Globalization.CultureInfo.InvariantCulture));
      }

      if ((bcount * 4) < 0) {
        throw new ArgumentException("bcount * 4 less than 0 (" + Convert.ToString((long)(bcount * 4), System.Globalization.CultureInfo.InvariantCulture) + ")");
      }

      if ((bcount * 4) > tempArr.Length) {
        throw new ArgumentException("bcount * 4 more than " + Convert.ToString((long)tempArr.Length, System.Globalization.CultureInfo.InvariantCulture) + " (" + Convert.ToString((long)(bcount * 4), System.Globalization.CultureInfo.InvariantCulture) + ")");
      }

      if (tempArr.Length - tempStart < bcount * 4) {
        throw new ArgumentException("tempArr.Length minus tempStart (" + Convert.ToString((long)tempArr.Length - tempStart, System.Globalization.CultureInfo.InvariantCulture) + ") is less than " + Convert.ToString((long)bcount * 4, System.Globalization.CultureInfo.InvariantCulture));
      }

      if (words1 == null) {
        throw new ArgumentNullException("words1");
      }

      if (astart < 0) {
        throw new ArgumentException("astart (" + Convert.ToString((long)astart, System.Globalization.CultureInfo.InvariantCulture) + ") is less than " + "0");
      }

      if (astart > words1.Length) {
        throw new ArgumentException("astart (" + Convert.ToString((long)astart, System.Globalization.CultureInfo.InvariantCulture) + ") is more than " + Convert.ToString((long)words1.Length, System.Globalization.CultureInfo.InvariantCulture));
      }

      if (acount < 0) {
        throw new ArgumentException("acount (" + Convert.ToString((long)acount, System.Globalization.CultureInfo.InvariantCulture) + ") is less than " + "0");
      }

      if (acount > words1.Length) {
        throw new ArgumentException("acount (" + Convert.ToString((long)acount, System.Globalization.CultureInfo.InvariantCulture) + ") is more than " + Convert.ToString((long)words1.Length, System.Globalization.CultureInfo.InvariantCulture));
      }

      if (words1.Length - astart < acount) {
        throw new ArgumentException("words1.Length minus astart (" + Convert.ToString((long)words1.Length - astart, System.Globalization.CultureInfo.InvariantCulture) + ") is less than " + Convert.ToString((long)acount, System.Globalization.CultureInfo.InvariantCulture));
      }

      if (words2 == null) {
        throw new ArgumentNullException("words2");
      }

      if (bstart < 0) {
        throw new ArgumentException("bstart (" + Convert.ToString((long)bstart, System.Globalization.CultureInfo.InvariantCulture) + ") is less than " + "0");
      }

      if (bstart > words2.Length) {
        throw new ArgumentException("bstart (" + Convert.ToString((long)bstart, System.Globalization.CultureInfo.InvariantCulture) + ") is more than " + Convert.ToString((long)words2.Length, System.Globalization.CultureInfo.InvariantCulture));
      }

      if (bcount < 0) {
        throw new ArgumentException("bcount (" + Convert.ToString((long)bcount, System.Globalization.CultureInfo.InvariantCulture) + ") is less than " + "0");
      }

      if (bcount > words2.Length) {
        throw new ArgumentException("bcount (" + Convert.ToString((long)bcount, System.Globalization.CultureInfo.InvariantCulture) + ") is more than " + Convert.ToString((long)words2.Length, System.Globalization.CultureInfo.InvariantCulture));
      }

      if (words2.Length - bstart < bcount) {
        throw new ArgumentException("words2.Length minus bstart (" + Convert.ToString((long)words2.Length - bstart, System.Globalization.CultureInfo.InvariantCulture) + ") is less than " + Convert.ToString((long)bcount, System.Globalization.CultureInfo.InvariantCulture));
      }
      #endif

      unchecked {
        int carryPos = 0;
        // Set carry to zero
        Array.Clear((short[])productArr, cstart, bcount);
        for (int i = 0; i < acount; i += bcount) {
          int diff = acount - i;
          if (diff > bcount) {
            SameSizeMultiply(
              tempArr,
              tempStart,  // uses bcount*2 space
              tempArr,
              tempStart + bcount + bcount,  // uses bcount*2 space
              words1,
              astart + i,
              words2,
              bstart,
              bcount);
            // Add carry
            AddUnevenSize(
              tempArr,
              tempStart,
              tempArr,
              tempStart,
              bcount + bcount,
              productArr,
              cstart + carryPos,
              bcount);
            // Copy product and carry
            Array.Copy(tempArr, tempStart, productArr, cstart + i, bcount + bcount);
            carryPos += bcount;
          } else {
            AsymmetricMultiply(
              tempArr,
              tempStart,  // uses diff + bcount space
              tempArr,
              tempStart + diff + bcount,  // uses diff + bcount space
              words1,
              astart + i,
              diff,
              words2,
              bstart,
              bcount);
            // Add carry
            AddUnevenSize(
              tempArr,
              tempStart,
              tempArr,
              tempStart,
              diff + bcount,
              productArr,
              cstart + carryPos,
              bcount);
            // Copy product without carry
            Array.Copy(tempArr, tempStart, productArr, cstart + i, diff + bcount);
          }
        }
      }
    }

    // Multiplies two operands of different sizes
    private static void AsymmetricMultiply(
      short[] resultArr,
      int resultStart,  // uses words1Count + words2Count space
      short[] tempArr,
      int tempStart,  // uses words1Count + words2Count space
      short[] words1,
      int words1Start,
      int words1Count,
      short[] words2,
      int words2Start,
      int words2Count) {
      // Console.WriteLine("AsymmetricMultiply " + words1Count + " " + words2Count + " [r=" + resultStart + " t=" + tempStart + " a=" + words1Start + " b=" + words2Start + "]");
      #if DEBUG
      if (resultArr == null) {
        throw new ArgumentNullException("resultArr");
      }

      if (resultStart < 0) {
        throw new ArgumentException("resultStart (" + Convert.ToString((long)resultStart, System.Globalization.CultureInfo.InvariantCulture) + ") is less than " + "0");
      }

      if (resultStart > resultArr.Length) {
        throw new ArgumentException("resultStart (" + Convert.ToString((long)resultStart, System.Globalization.CultureInfo.InvariantCulture) + ") is more than " + Convert.ToString((long)resultArr.Length, System.Globalization.CultureInfo.InvariantCulture));
      }

      if (words1Count + words2Count < 0) {
        throw new ArgumentException("words1Count plus words2Count (" + Convert.ToString((long)words1Count + words2Count, System.Globalization.CultureInfo.InvariantCulture) + ") is less than " + "0");
      }

      if (words1Count + words2Count > resultArr.Length) {
        throw new ArgumentException("words1Count plus words2Count (" + Convert.ToString((long)words1Count + words2Count, System.Globalization.CultureInfo.InvariantCulture) + ") is more than " + Convert.ToString((long)resultArr.Length, System.Globalization.CultureInfo.InvariantCulture));
      }

      if (resultArr.Length - resultStart < words1Count + words2Count) {
        throw new ArgumentException("resultArr.Length minus resultStart (" + Convert.ToString((long)resultArr.Length - resultStart, System.Globalization.CultureInfo.InvariantCulture) + ") is less than " + Convert.ToString((long)words1Count + words2Count, System.Globalization.CultureInfo.InvariantCulture));
      }

      if (tempArr == null) {
        throw new ArgumentNullException("tempArr");
      }

      if (tempStart < 0) {
        throw new ArgumentException("tempStart (" + Convert.ToString((long)tempStart, System.Globalization.CultureInfo.InvariantCulture) + ") is less than " + "0");
      }

      if (tempStart > tempArr.Length) {
        throw new ArgumentException("tempStart (" + Convert.ToString((long)tempStart, System.Globalization.CultureInfo.InvariantCulture) + ") is more than " + Convert.ToString((long)tempArr.Length, System.Globalization.CultureInfo.InvariantCulture));
      }

      if (words1Count + words2Count < 0) {
        throw new ArgumentException("words1Count plus words2Count (" + Convert.ToString((long)words1Count + words2Count, System.Globalization.CultureInfo.InvariantCulture) + ") is less than " + "0");
      }

      if (words1Count + words2Count > tempArr.Length) {
        throw new ArgumentException("words1Count plus words2Count (" + Convert.ToString((long)words1Count + words2Count, System.Globalization.CultureInfo.InvariantCulture) + ") is more than " + Convert.ToString((long)tempArr.Length, System.Globalization.CultureInfo.InvariantCulture));
      }

      if (tempArr.Length - tempStart < words1Count + words2Count) {
        throw new ArgumentException("tempArr.Length minus tempStart (" + Convert.ToString((long)tempArr.Length - tempStart, System.Globalization.CultureInfo.InvariantCulture) + ") is less than " + Convert.ToString((long)words1Count + words2Count, System.Globalization.CultureInfo.InvariantCulture));
      }

      if (words1 == null) {
        throw new ArgumentNullException("words1");
      }

      if (words1Start < 0) {
        throw new ArgumentException("words1Start (" + Convert.ToString((long)words1Start, System.Globalization.CultureInfo.InvariantCulture) + ") is less than " + "0");
      }

      if (words1Start > words1.Length) {
        throw new ArgumentException("words1Start (" + Convert.ToString((long)words1Start, System.Globalization.CultureInfo.InvariantCulture) + ") is more than " + Convert.ToString((long)words1.Length, System.Globalization.CultureInfo.InvariantCulture));
      }

      if (words1Count < 0) {
        throw new ArgumentException("words1Count (" + Convert.ToString((long)words1Count, System.Globalization.CultureInfo.InvariantCulture) + ") is less than " + "0");
      }

      if (words1Count > words1.Length) {
        throw new ArgumentException("words1Count (" + Convert.ToString((long)words1Count, System.Globalization.CultureInfo.InvariantCulture) + ") is more than " + Convert.ToString((long)words1.Length, System.Globalization.CultureInfo.InvariantCulture));
      }

      if (words1.Length - words1Start < words1Count) {
        throw new ArgumentException("words1.Length minus words1Start (" + Convert.ToString((long)words1.Length - words1Start, System.Globalization.CultureInfo.InvariantCulture) + ") is less than " + Convert.ToString((long)words1Count, System.Globalization.CultureInfo.InvariantCulture));
      }

      if (words2 == null) {
        throw new ArgumentNullException("words2");
      }

      if (words2Start < 0) {
        throw new ArgumentException("words2Start (" + Convert.ToString((long)words2Start, System.Globalization.CultureInfo.InvariantCulture) + ") is less than " + "0");
      }

      if (words2Start > words2.Length) {
        throw new ArgumentException("words2Start (" + Convert.ToString((long)words2Start, System.Globalization.CultureInfo.InvariantCulture) + ") is more than " + Convert.ToString((long)words2.Length, System.Globalization.CultureInfo.InvariantCulture));
      }

      if (words2Count < 0) {
        throw new ArgumentException("words2Count (" + Convert.ToString((long)words2Count, System.Globalization.CultureInfo.InvariantCulture) + ") is less than " + "0");
      }

      if (words2Count > words2.Length) {
        throw new ArgumentException("words2Count (" + Convert.ToString((long)words2Count, System.Globalization.CultureInfo.InvariantCulture) + ") is more than " + Convert.ToString((long)words2.Length, System.Globalization.CultureInfo.InvariantCulture));
      }

      if (words2.Length - words2Start < words2Count) {
        throw new ArgumentException("words2.Length minus words2Start (" + Convert.ToString((long)words2.Length - words2Start, System.Globalization.CultureInfo.InvariantCulture) + ") is less than " + Convert.ToString((long)words2Count, System.Globalization.CultureInfo.InvariantCulture));
      }
      #endif

      if (words1Count == words2Count) {
        if (words1Start == words2Start && words1 == words2) {
          // Both operands have the same value and the same word count
          RecursiveSquare(resultArr, resultStart, tempArr, tempStart, words1, words1Start, words1Count);
        } else if (words1Count == 2) {
          // Both operands have a word count of 2
          Baseline_Multiply2(resultArr, resultStart, words1, words1Start, words2, words2Start);
        } else {
          // Other cases where both operands have the same word count
          SameSizeMultiply(resultArr, resultStart, tempArr, tempStart, words1, words1Start, words2, words2Start, words1Count);
        }

        return;
      }
      if (words1Count > words2Count) {
        // Ensure that words1 is smaller by swapping if necessary
        short[] tmp1 = words1; words1 = words2; words2 = tmp1;
        int tmp3 = words1Start; words1Start = words2Start; words2Start = tmp3;
        int tmp2 = words1Count; words1Count = words2Count; words2Count = tmp2;
      }

      if (words1Count == 1 || (words1Count == 2 && words1[words1Start + 1] == 0)) {
        switch (words1[words1Start]) {
          case 0:
            // words1 is zero, so result is 0
            Array.Clear((short[])resultArr, resultStart, words2Count + 2);
            return;
          case 1:
            Array.Copy(words2, words2Start, resultArr, resultStart, (int)words2Count);
            resultArr[resultStart + words2Count] = (short)0;
            resultArr[resultStart + words2Count + 1] = (short)0;
            return;
          default:
            resultArr[resultStart + words2Count] = LinearMultiply(resultArr, resultStart, words2, words2Start, words1[words1Start], words2Count);
            resultArr[resultStart + words2Count + 1] = (short)0;
            return;
        }
      } else if (words1Count == 2 && (words2Count & 1) == 0) {
        int a0 = ((int)words1[words1Start]) & 0xffff;
        int a1 = ((int)words1[words1Start + 1]) & 0xffff;
        resultArr[resultStart + words2Count] = (short)0;
        resultArr[resultStart + words2Count + 1] = (short)0;
        AtomicMultiplyOpt(resultArr, resultStart, a0, a1, words2, words2Start, 0, words2Count);
        AtomicMultiplyAddOpt(resultArr, resultStart, a0, a1, words2, words2Start, 2, words2Count);
        return;
      } else if (words1Count <= 10 && words2Count <= 10) {
        SchoolbookMultiply(resultArr, resultStart, words1, words1Start, words1Count, words2, words2Start, words2Count);
      } else {
        int wordsRem = words2Count % words1Count;
        int evenmult = (words2Count / words1Count) & 1;
        int i;
        // Console.WriteLine("counts=" + words1Count + "," + words2Count + " res=" + (resultStart + words1Count) + " temp=" + (tempStart + (words1Count << 1)) + " rem=" + wordsRem + " evenwc=" + evenmult);
        if (wordsRem == 0) {
          // words2Count is divisible by words1count
          if (evenmult == 0) {
            SameSizeMultiply(resultArr, resultStart, tempArr, tempStart, words1, words1Start, words2, words2Start, words1Count);
            Array.Copy(resultArr, resultStart + words1Count, tempArr, (int)(tempStart + (words1Count << 1)), words1Count);
            for (i = words1Count << 1; i < words2Count; i += words1Count << 1) {
              SameSizeMultiply(tempArr, tempStart + words1Count + i, tempArr, tempStart, words1, words1Start, words2, words2Start + i, words1Count);
            }
            for (i = words1Count; i < words2Count; i += words1Count << 1) {
              SameSizeMultiply(resultArr, resultStart + i, tempArr, tempStart, words1, words1Start, words2, words2Start + i, words1Count);
            }
          } else {
            for (i = 0; i < words2Count; i += words1Count << 1) {
              SameSizeMultiply(resultArr, resultStart + i, tempArr, tempStart, words1, words1Start, words2, words2Start + i, words1Count);
            }
            for (i = words1Count; i < words2Count; i += words1Count << 1) {
              SameSizeMultiply(tempArr, tempStart + words1Count + i, tempArr, tempStart, words1, words1Start, words2, words2Start + i, words1Count);
            }
          }
          if (Add(resultArr, resultStart + words1Count, resultArr, resultStart + words1Count, tempArr, tempStart + (words1Count << 1), words2Count - words1Count) != 0) {
            Increment(resultArr, (int)(resultStart + words2Count), words1Count, (short)1);
          }
        } else if ((words1Count + words2Count) >= (words1Count << 2)) {
          // Console.WriteLine("Chunked Linear Multiply Long");
          ChunkedLinearMultiply(
            resultArr,
            resultStart,
            tempArr,
            tempStart,
            words2,
            words2Start,
            words2Count,
            words1,
            words1Start,
            words1Count);
        } else if (words1Count + 1 == words2Count ||
                   (words1Count + 2 == words2Count && words2[words2Start + words2Count - 1] == 0)) {
          Array.Clear((short[])resultArr, resultStart, words1Count + words2Count);
          // Multiply the low parts of each operand
          SameSizeMultiply(
            resultArr,
            resultStart,
            tempArr,
            tempStart,
            words1,
            words1Start,
            words2,
            words2Start,
            words1Count);
          // Multiply the high parts
          // while adding carry from the high part of the product
          short carry = LinearMultiplyAdd(
            resultArr,
            resultStart + words1Count,
            words1,
            words1Start,
            words2[words2Start + words1Count],
            words1Count);
          resultArr[resultStart + words1Count + words1Count] = carry;
        } else {
          short[] t2 = new short[words1Count << 2];
          // Console.WriteLine("Chunked Linear Multiply Short");
          ChunkedLinearMultiply(
            resultArr,
            resultStart,
            t2,
            0,
            words2,
            words2Start,
            words2Count,
            words1,
            words1Start,
            words1Count);
        }
      }
    }

    private static int MakeUint(short first, short second) {
      return unchecked((int)((((int)first) & 0xffff) | ((int)second << 16)));
    }

    private static short GetLowHalf(int val) {
      return unchecked((short)(val & 0xffff));
    }

    private static short GetHighHalf(int val) {
      return unchecked((short)((val >> 16) & 0xffff));
    }

    private static short GetHighHalfAsBorrow(int val) {
      return unchecked((short)(0 - ((val >> 16) & 0xffff)));
    }

    private static int BitPrecision(short numberValue) {
      if (numberValue == 0) {
        return 0;
      }
      int i = 16;
      unchecked {
        if ((numberValue >> 8) == 0) {
          numberValue <<= 8;
          i -= 8;
        }

        if ((numberValue >> 12) == 0) {
          numberValue <<= 4;
          i -= 4;
        }

        if ((numberValue >> 14) == 0) {
          numberValue <<= 2;
          i -= 2;
        }

        if ((numberValue >> 15) == 0) {
          --i;
        }
      }
      return i;
    }

    private static short Divide32By16(int dividendLow, short divisorShort, bool returnRemainder) {
      int tmpInt;
      int dividendHigh = 0;
      int intDivisor = ((int)divisorShort) & 0xffff;
      for (int i = 0; i < 32; ++i) {
        tmpInt = dividendHigh >> 31;
        dividendHigh <<= 1;
        dividendHigh = unchecked((int)(dividendHigh | ((int)((dividendLow >> 31) & 1))));
        dividendLow <<= 1;
        tmpInt |= dividendHigh;
        // unsigned greater-than-or-equal check
        if (((tmpInt >> 31) != 0) || (tmpInt >= intDivisor)) {
          unchecked {
            dividendHigh -= intDivisor;
            ++dividendLow;
          }
        }
      }
      return returnRemainder ?
        unchecked((short)(((int)dividendHigh) & 0xffff)) :
        unchecked((short)(((int)dividendLow) & 0xffff));
    }

    private static short DivideUnsigned(int x, short y) {
      unchecked {
        int iy = ((int)y) & 0xffff;
        if ((x >> 31) == 0) {
          // x is already nonnegative
          return (short)(((int)x / iy) & 0xffff);
        } else {
          return Divide32By16(x, y, false);
        }
      }
    }

    private static short RemainderUnsigned(int x, short y) {
      unchecked {
        int iy = ((int)y) & 0xffff;
        if ((x >> 31) == 0) {
          // x is already nonnegative
          return (short)(((int)x % iy) & 0xffff);
        } else {
          return Divide32By16(x, y, true);
        }
      }
    }

    private static short DivideThreeWordsByTwo(short[] words1, int words1Start, short valueB0, short valueB1) {
      // DebugAssert.IsTrue(words1[2] < valueB1 || (words1[2]==valueB1 && words1[1] < valueB0),"{0} line {1}: words1[2] < valueB1 || (words1[2]==valueB1 && words1[1] < valueB0)","integer.cpp",360);
      short valueQ;
      unchecked {
        if ((short)(valueB1 + 1) == 0) {
          valueQ = words1[words1Start + 2];
        } else if (valueB1 != 0) {
          valueQ = DivideUnsigned(MakeUint(words1[words1Start + 1], words1[words1Start + 2]), (short)(((int)valueB1 + 1) & 0xffff));
        } else {
          valueQ = DivideUnsigned(MakeUint(words1[words1Start], words1[words1Start + 1]), valueB0);
        }

        int valueQint = ((int)valueQ) & 0xffff;
        int valueB0int = ((int)valueB0) & 0xffff;
        int valueB1int = ((int)valueB1) & 0xffff;
        int p = valueB0int * valueQint;
        int u = (((int)words1[words1Start]) & 0xffff) - (p & 0xffff);
        words1[words1Start] = GetLowHalf(u);
        u = (((int)words1[words1Start + 1]) & 0xffff) - ((p >> 16) & 0xffff) -
          (((int)GetHighHalfAsBorrow(u)) & 0xffff) - (valueB1int * valueQint);
        words1[words1Start + 1] = GetLowHalf(u);
        words1[words1Start + 2] += GetHighHalf(u);
        while (words1[words1Start + 2] != 0 ||
               (((int)words1[words1Start + 1]) & 0xffff) > (((int)valueB1) & 0xffff) ||
               (words1[words1Start + 1] == valueB1 && (((int)words1[words1Start]) & 0xffff) >= (((int)valueB0) & 0xffff))) {
          u = (((int)words1[words1Start]) & 0xffff) - valueB0int;
          words1[words1Start] = GetLowHalf(u);
          u = (((int)words1[words1Start + 1]) & 0xffff) - valueB1int - (((int)GetHighHalfAsBorrow(u)) & 0xffff);
          words1[words1Start + 1] = GetLowHalf(u);
          words1[words1Start + 2] += GetHighHalf(u);
          ++valueQ;
        }
      }
      return valueQ;
    }

    private static void DivideFourWordsByTwo(
      short[] quotient,
      int quotientStart,
      short[] words1,
      int words1Start,
      short word2A,
      short word2B,
      short[] temp) {
      if (word2A == 0 && word2B == 0) {
        // if divisor is 0, we assume divisor == 2**32
        quotient[quotientStart] = words1[words1Start + 2];
        quotient[quotientStart + 1] = words1[words1Start + 3];
      } else {
        temp[0] = words1[words1Start];
        temp[1] = words1[words1Start + 1];
        temp[2] = words1[words1Start + 2];
        temp[3] = words1[words1Start + 3];
        short valueQ1 = DivideThreeWordsByTwo(temp, 1, word2A, word2B);
        short valueQ0 = DivideThreeWordsByTwo(temp, 0, word2A, word2B);
        quotient[quotientStart] = valueQ0;
        quotient[quotientStart + 1] = valueQ1;
      }
    }

    private static void AtomicMultiplyOpt(short[] c, int valueCstart, int valueA0, int valueA1, short[] words2, int words2Start, int istart, int iend) {
      short s;
      int d;
      int first1MinusFirst0 = ((int)valueA1 - valueA0) & 0xffff;
      valueA1 &= 0xffff;
      valueA0 &= 0xffff;
      unchecked {
        if (valueA1 >= valueA0) {
          for (int i = istart; i < iend; i += 4) {
            int valueB0 = ((int)words2[words2Start + i]) & 0xffff;
            int valueB1 = ((int)words2[words2Start + i + 1]) & 0xffff;
            int csi = valueCstart + i;
            if (valueB0 >= valueB1) {
              s = (short)0;
              d = first1MinusFirst0 * (((int)valueB0 - valueB1) & 0xffff);
            } else {
              s = (short)first1MinusFirst0;
              d = (((int)s) & 0xffff) * (((int)valueB0 - valueB1) & 0xffff);
            }
            int valueA0B0 = valueA0 * valueB0;
            c[csi] = (short)(((int)valueA0B0) & 0xffff);
            int a0b0high = (valueA0B0 >> 16) & 0xffff;
            int valueA1B1 = valueA1 * valueB1;
            int tempInt;
            tempInt = a0b0high +
              (((int)valueA0B0) & 0xffff) + (((int)d) & 0xffff) + (((int)valueA1B1) & 0xffff);
            c[csi + 1] = (short)(((int)tempInt) & 0xffff);

            tempInt = valueA1B1 + (((int)(tempInt >> 16)) & 0xffff) +
              a0b0high + (((int)(d >> 16)) & 0xffff) + (((int)(valueA1B1 >> 16)) & 0xffff) -
              (((int)s) & 0xffff);

            c[csi + 2] = (short)(((int)tempInt) & 0xffff);
            c[csi + 3] = (short)(((int)(tempInt >> 16)) & 0xffff);
          }
        } else {
          for (int i = istart; i < iend; i += 4) {
            int valueB0 = ((int)words2[words2Start + i]) & 0xffff;
            int valueB1 = ((int)words2[words2Start + i + 1]) & 0xffff;
            int csi = valueCstart + i;
            if (valueB0 > valueB1) {
              s = (short)(((int)valueB0 - valueB1) & 0xffff);
              d = first1MinusFirst0 * (((int)s) & 0xffff);
            } else {
              s = (short)0;
              d = (((int)valueA0 - valueA1) & 0xffff) * (((int)valueB1 - valueB0) & 0xffff);
            }
            int valueA0B0 = valueA0 * valueB0;
            int a0b0high = (valueA0B0 >> 16) & 0xffff;
            c[csi] = (short)(((int)valueA0B0) & 0xffff);

            int valueA1B1 = valueA1 * valueB1;
            int tempInt;
            tempInt = a0b0high +
              (((int)valueA0B0) & 0xffff) + (((int)d) & 0xffff) + (((int)valueA1B1) & 0xffff);
            c[csi + 1] = (short)(((int)tempInt) & 0xffff);

            tempInt = valueA1B1 + (((int)(tempInt >> 16)) & 0xffff) +
              a0b0high + (((int)(d >> 16)) & 0xffff) + (((int)(valueA1B1 >> 16)) & 0xffff) -
              (((int)s) & 0xffff);

            c[csi + 2] = (short)(((int)tempInt) & 0xffff);
            c[csi + 3] = (short)(((int)(tempInt >> 16)) & 0xffff);
          }
        }
      }
    }

    private static void AtomicMultiplyAddOpt(short[] c, int valueCstart, int valueA0, int valueA1, short[] words2, int words2Start, int istart, int iend) {
      short s;
      int d;
      int first1MinusFirst0 = ((int)valueA1 - valueA0) & 0xffff;
      valueA1 &= 0xffff;
      valueA0 &= 0xffff;
      unchecked {
        if (valueA1 >= valueA0) {
          for (int i = istart; i < iend; i += 4) {
            int b0 = ((int)words2[words2Start + i]) & 0xffff;
            int b1 = ((int)words2[words2Start + i + 1]) & 0xffff;
            int csi = valueCstart + i;
            if (b0 >= b1) {
              s = (short)0;
              d = first1MinusFirst0 * (((int)b0 - b1) & 0xffff);
            } else {
              s = (short)first1MinusFirst0;
              d = (((int)s) & 0xffff) * (((int)b0 - b1) & 0xffff);
            }
            int valueA0B0 = valueA0 * b0;
            int a0b0high = (valueA0B0 >> 16) & 0xffff;
            int tempInt;
            tempInt = valueA0B0 + (((int)c[csi]) & 0xffff);
            c[csi] = (short)(((int)tempInt) & 0xffff);

            int valueA1B1 = valueA1 * b1;
            int a1b1low = valueA1B1 & 0xffff;
            int a1b1high = ((int)(valueA1B1 >> 16)) & 0xffff;
            tempInt = (((int)(tempInt >> 16)) & 0xffff) + (((int)valueA0B0) & 0xffff) + (((int)d) & 0xffff) + a1b1low + (((int)c[csi + 1]) & 0xffff);
            c[csi + 1] = (short)(((int)tempInt) & 0xffff);

            tempInt = (((int)(tempInt >> 16)) & 0xffff) + a1b1low + a0b0high + (((int)(d >> 16)) & 0xffff) +
              a1b1high - (((int)s) & 0xffff) + (((int)c[csi + 2]) & 0xffff);
            c[csi + 2] = (short)(((int)tempInt) & 0xffff);

            tempInt = (((int)(tempInt >> 16)) & 0xffff) + a1b1high + (((int)c[csi + 3]) & 0xffff);
            c[csi + 3] = (short)(((int)tempInt) & 0xffff);
            if ((tempInt >> 16) != 0) {
              ++c[csi + 4];
              c[csi + 5] += (short)((c[csi + 4] == 0) ? 1 : 0);
            }
          }
        } else {
          for (int i = istart; i < iend; i += 4) {
            int valueB0 = ((int)words2[words2Start + i]) & 0xffff;
            int valueB1 = ((int)words2[words2Start + i + 1]) & 0xffff;
            int csi = valueCstart + i;
            if (valueB0 > valueB1) {
              s = (short)(((int)valueB0 - valueB1) & 0xffff);
              d = first1MinusFirst0 * (((int)s) & 0xffff);
            } else {
              s = (short)0;
              d = (((int)valueA0 - valueA1) & 0xffff) * (((int)valueB1 - valueB0) & 0xffff);
            }
            int valueA0B0 = valueA0 * valueB0;
            int a0b0high = (valueA0B0 >> 16) & 0xffff;
            int tempInt;
            tempInt = valueA0B0 + (((int)c[csi]) & 0xffff);
            c[csi] = (short)(((int)tempInt) & 0xffff);

            int valueA1B1 = valueA1 * valueB1;
            int a1b1low = valueA1B1 & 0xffff;
            int a1b1high = (valueA1B1 >> 16) & 0xffff;
            tempInt = (((int)(tempInt >> 16)) & 0xffff) + (((int)valueA0B0) & 0xffff) + (((int)d) & 0xffff) + a1b1low + (((int)c[csi + 1]) & 0xffff);
            c[csi + 1] = (short)(((int)tempInt) & 0xffff);

            tempInt = (((int)(tempInt >> 16)) & 0xffff) + a1b1low + a0b0high + (((int)(d >> 16)) & 0xffff) +
              a1b1high - (((int)s) & 0xffff) + (((int)c[csi + 2]) & 0xffff);
            c[csi + 2] = (short)(((int)tempInt) & 0xffff);

            tempInt = (((int)(tempInt >> 16)) & 0xffff) + a1b1high + (((int)c[csi + 3]) & 0xffff);
            c[csi + 3] = (short)(((int)tempInt) & 0xffff);
            if ((tempInt >> 16) != 0) {
              ++c[csi + 4];
              c[csi + 5] += (short)((c[csi + 4] == 0) ? 1 : 0);
            }
          }
        }
      }
    }

    private static void Divide(
      short[] remainderArr,
      int remainderStart,  // remainder; size: words2Count
      short[] quotientArr,
      int quotientStart,  // quotient
      short[] tempArr,
      int tempStart,  // scratch space
      short[] words1,
      int words1Start,
      int words1Count,  // dividend
      short[] words2,
      int words2Start,
      int words2Count) {
      // set up temporary work space
      #if DEBUG
      if (words1Count <= 0) {
        throw new ArgumentException("words1Count (" + Convert.ToString((long)words1Count, System.Globalization.CultureInfo.InvariantCulture) + ") is not greater than " + "0");
      }
      if (words2Count <= 0) {
        throw new ArgumentException("words2Count (" + Convert.ToString((long)words2Count, System.Globalization.CultureInfo.InvariantCulture) + ") is not greater than " + "0");
      }
      #endif
      if (words2Count == 0) {
        throw new DivideByZeroException("division by zero");
      }
      if (words2Count == 1) {
        if (words2[words2Start] == 0) {
          throw new DivideByZeroException("division by zero");
        }
        int smallRemainder = ((int)FastDivideAndRemainder(
          quotientArr,
          quotientStart,
          words1,
          words1Start,
          words1Count,
          words2[words2Start])) & 0xffff;
        remainderArr[remainderStart] = (short)smallRemainder;
        return;
      }
      #if DEBUG
      if (!(words1Count % 2 == 0 && words2Count % 2 == 0)) {
        throw new ArgumentException("doesn't satisfy valueNA%2==0 && valueNB%2==0");
      }
      if (!(words2[words2Start + words2Count - 1] != 0 ||
            words2[words2Start + words2Count - 2] != 0)) {
        throw new ArgumentException("doesn't satisfy words2[valueNB-1]!=0 || words2[valueNB-2]!=0");
      }
      if (!(words2Count <= words1Count)) {
        throw new ArgumentException("doesn't satisfy valueNB<= valueNA");
      }
      #endif
      short[] quot = quotientArr;
      if (quotientArr == null) {
        quot = new short[2];
      }
      int valueTBstart = (int)(tempStart + (words1Count + 2));
      int valueTPstart = (int)(tempStart + (words1Count + 2 + words2Count));
      unchecked {
        // copy words2 into TB and normalize it so that TB has highest bit set to 1
        int shiftWords = (short)(words2[words2Start + words2Count - 1] == 0 ? 1 : 0);
        tempArr[valueTBstart] = (short)0;
        tempArr[valueTBstart + words2Count - 1] = (short)0;
        Array.Copy(words2, words2Start, tempArr, (int)(valueTBstart + shiftWords), words2Count - shiftWords);
        short shiftBits = (short)((short)16 - BitPrecision(tempArr[valueTBstart + words2Count - 1]));
        ShiftWordsLeftByBits(
          tempArr,
          valueTBstart,
          words2Count,
          shiftBits);
        // copy words1 into valueTA and normalize it
        tempArr[0] = (short)0;
        tempArr[words1Count] = (short)0;
        tempArr[words1Count + 1] = (short)0;
        Array.Copy(words1, words1Start, tempArr, (int)(tempStart + shiftWords), words1Count);
        ShiftWordsLeftByBits(
          tempArr,
          tempStart,
          words1Count + 2,
          shiftBits);

        if (tempArr[tempStart + words1Count + 1] == 0 && (((int)tempArr[tempStart + words1Count]) & 0xffff) <= 1) {
          if (quotientArr != null) {
            quotientArr[quotientStart + words1Count - words2Count + 1] = (short)0;
            quotientArr[quotientStart + words1Count - words2Count] = (short)0;
          }
          while (
            tempArr[words1Count] != 0 || Compare(
              tempArr,
              (int)(tempStart + words1Count - words2Count),
              tempArr,
              valueTBstart,
              words2Count) >= 0) {
            tempArr[words1Count] -= (short)Subtract(
              tempArr,
              tempStart + words1Count - words2Count,
              tempArr,
              tempStart + words1Count - words2Count,
              tempArr,
              valueTBstart,
              words2Count);
            if (quotientArr != null) {
              quotientArr[quotientStart + words1Count - words2Count] += (short)1;
            }
          }
        } else {
          words1Count += 2;
        }

        short valueBT0 = (short)(tempArr[valueTBstart + words2Count - 2] + (short)1);
        short valueBT1 = (short)(tempArr[valueTBstart + words2Count - 1] + (short)(valueBT0 == (short)0 ? 1 : 0));

        // start reducing valueTA mod TB, 2 words at a time
        short[] valueTAtomic = new short[4];
        for (int i = words1Count - 2; i >= words2Count; i -= 2) {
          int qs = (quotientArr == null) ? 0 : quotientStart + i - words2Count;
          DivideFourWordsByTwo(quot, qs, tempArr, (int)(tempStart + i - 2), valueBT0, valueBT1, valueTAtomic);
          // now correct the underestimated quotient
          int valueRstart2 = tempStart + i - words2Count;
          int n = words2Count;
          unchecked {
            int quotient0 = quot[qs];
            int quotient1 = quot[qs + 1];
            if (quotient1 == 0) {
              short carry = LinearMultiply(tempArr, valueTPstart, tempArr, valueTBstart, (short)quotient0, n);
              tempArr[valueTPstart + n] = carry;
              tempArr[valueTPstart + n + 1] = 0;
            } else if (n == 2) {
              Baseline_Multiply2(tempArr, valueTPstart, quot, qs, tempArr, valueTBstart);
            } else {
              tempArr[valueTPstart + n] = (short)0;
              tempArr[valueTPstart + n + 1] = (short)0;
              quotient0 &= 0xffff;
              quotient1 &= 0xffff;
              AtomicMultiplyOpt(tempArr, valueTPstart, quotient0, quotient1, tempArr, valueTBstart, 0, n);
              AtomicMultiplyAddOpt(tempArr, valueTPstart, quotient0, quotient1, tempArr, valueTBstart, 2, n);
            }
            Subtract(tempArr, valueRstart2, tempArr, valueRstart2, tempArr, valueTPstart, n + 2);
            while (tempArr[valueRstart2 + n] != 0 || Compare(tempArr, valueRstart2, tempArr, valueTBstart, n) >= 0) {
              tempArr[valueRstart2 + n] -= (short)Subtract(tempArr, valueRstart2, tempArr, valueRstart2, tempArr, valueTBstart, n);
              if (quotientArr != null) {
                ++quotientArr[qs];
                quotientArr[qs + 1] += (short)((quotientArr[qs] == 0) ? 1 : 0);
              }
            }
          }
        }
        if (remainderArr != null) {  // If the remainder is non-null
          // copy valueTA into result, and denormalize it
          Array.Copy(tempArr, (int)(tempStart + shiftWords), remainderArr, remainderStart, words2Count);
          ShiftWordsRightByBits(remainderArr, remainderStart, words2Count, shiftBits);
        }
      }
    }

    private static int RoundupSize(int n) {
      return n + (n & 1);
    }

    private bool negative;
    private int wordCount = -1;
    private short[] reg;

    private BigInteger() {
    }

    /// <summary>Initializes a BigInteger object from an array of bytes.</summary>
    /// <param name='bytes'>A byte array.</param>
    /// <returns>A BigInteger object.</returns>
    /// <param name='littleEndian'>A Boolean object.</param>
    /// <exception cref='System.ArgumentNullException'>The parameter
    /// <paramref name='bytes'/> is null.</exception>
    public static BigInteger fromByteArray(byte[] bytes, bool littleEndian) {
      if (bytes == null) {
 throw new ArgumentNullException("bytes");
}
      if (bytes.Length == 0) {
 return BigInteger.Zero;
}
      BigInteger bigint = new BigInteger();
      bigint.fromByteArrayInternal(bytes, littleEndian);
      return bigint;
    }

    private void fromByteArrayInternal(byte[] bytes, bool littleEndian) {
      if (bytes == null) {
        throw new ArgumentNullException("bytes");
      }
      #if DEBUG
if (bytes.Length <= 0) {
 throw new ArgumentException("bytes.Length (" + Convert.ToString((long)bytes.Length, System.Globalization.CultureInfo.InvariantCulture) + ") is not greater than " + "0");
}
#endif

      int len = bytes.Length;
      int wordLength = ((int)len + 1) >> 1;
      wordLength = RoundupSize(wordLength);
      this.reg = new short[wordLength];
      int valueJIndex = littleEndian ? len - 1 : 0;
      bool negative = (bytes[valueJIndex] & 0x80) != 0;
      this.negative = negative;
      int j = 0;
      if (!negative) {
        for (int i = 0; i < len; i += 2, j++) {
          int index = littleEndian ? i : len - 1 - i;
          int index2 = littleEndian ? i + 1 : len - 2 - i;
          this.reg[j] = (short)(((int)bytes[index]) & 0xff);
          if (index2 >= 0 && index2 < len) {
            this.reg[j] |= unchecked((short)(((short)bytes[index2]) << 8));
          }
        }
      } else {
        for (int i = 0; i < len; i += 2, j++) {
          int index = littleEndian ? i : len - 1 - i;
          int index2 = littleEndian ? i + 1 : len - 2 - i;
          this.reg[j] = (short)(((int)bytes[index]) & 0xff);
          if (index2 >= 0 && index2 < len) {
            this.reg[j] |= unchecked((short)(((short)bytes[index2]) << 8));
          } else {
            // sign extend the last byte
            this.reg[j] |= unchecked((short)0xff00);
          }
        }
        for (; j < this.reg.Length; ++j) {
          this.reg[j] = unchecked((short)0xffff);  // sign extend remaining words
        }
        TwosComplement(this.reg, 0, (int)this.reg.Length);
      }
      this.wordCount = this.reg.Length;
      while (this.wordCount != 0 &&
             this.reg[this.wordCount - 1] == 0) {
        --this.wordCount;
      }
    }

    private BigInteger Allocate(int length) {
      this.reg = new short[RoundupSize(length)];  // will be initialized to 0
      this.negative = false;
      this.wordCount = 0;
      return this;
    }

    private static short[] GrowForCarry(short[] a, short carry) {
      int oldLength = a.Length;
      short[] ret = CleanGrow(a, RoundupSize(oldLength + 1));
      ret[oldLength] = carry;
      return ret;
    }

    private static short[] CleanGrow(short[] a, int size) {
      if (size > a.Length) {
        short[] newa = new short[size];
        Array.Copy(a, newa, a.Length);
        return newa;
      }
      return a;
    }

    private void SetBitInternal(int n, bool value) {
      if (value) {
        this.reg = CleanGrow(this.reg, RoundupSize(BitsToWords(n + 1)));
        this.reg[(n >> 4)] |= (short)((short)1 << (int)(n & 0xf));
        this.wordCount = this.CalcWordCount();
      } else {
        if ((n >> 4) < this.reg.Length) {
          this.reg[(n >> 4)] &= unchecked((short)(~((short)1 << (int)(n % 16))));
        }
        this.wordCount = this.CalcWordCount();
      }
    }

    /// <summary>Returns whether a bit is set in the two's-complement representation
    /// of this object's value.</summary>
    /// <param name='index'>Zero based index of the bit to test. 0 means the
    /// least significant bit.</param>
    /// <returns>True if the specified bit is set; otherwise, false.</returns>
    public bool testBit(int index) {
      if (index < 0) {
        throw new ArgumentOutOfRangeException("index");
      }
      if (this.wordCount == 0) {
        return false;
      }
      if (this.negative) {
        int tcindex = 0;
        int wordpos = index / 16;
        if (wordpos >= this.reg.Length) {
          return true;
        }
        while (tcindex < wordpos && this.reg[tcindex] == 0) {
          ++tcindex;
        }
        short tc;
        unchecked {
          tc = this.reg[wordpos];
          if (tcindex == wordpos) {
            --tc;
          }
          tc = (short)~tc;
        }
        return (bool)(((tc >> (int)(index & 15)) & 1) != 0);
      } else {
        return this.GetUnsignedBit(index);
      }
    }

    private bool GetUnsignedBit(int n) {
      #if DEBUG
      if (n < 0) {
        throw new ArgumentException("n (" + Convert.ToString((long)n, System.Globalization.CultureInfo.InvariantCulture) + ") is less than " + "0");
      }
      #endif
      if ((n >> 4) >= this.reg.Length) {
        return false;
      } else {
        return (bool)(((this.reg[(n >> 4)] >> (int)(n & 15)) & 1) != 0);
      }
    }

    private BigInteger InitializeInt(int numberValue) {
      int iut;
      unchecked {
        this.negative = numberValue < 0;
        if (numberValue == Int32.MinValue) {
          this.reg = new short[2];
          this.reg[0] = 0;
          this.reg[1] = (short)0x8000;
          this.wordCount = 2;
        } else {
          iut = unchecked((numberValue < 0) ? -numberValue : numberValue);
          this.reg = new short[2];
          this.reg[0] = (short)iut;
          this.reg[1] = (short)(iut >> 16);
          this.wordCount = this.reg[1] != 0 ? 2 : (this.reg[0] == 0 ? 0 : 1);
        }
      }
      return this;
    }

    /// <summary>Returns a byte array of this object&apos;s value.</summary>
    /// <returns>A byte array that represents the value of this object.</returns>
    /// <param name='littleEndian'>A Boolean object.</param>
    public byte[] toByteArray(bool littleEndian) {
      int sign = this.Sign;
      if (sign == 0) {
        return new byte[] { (byte)0 };
      } else if (sign > 0) {
        int byteCount = this.ByteCount();
        int byteArrayLength = byteCount;
        if (this.GetUnsignedBit((byteCount * 8) - 1)) {
          ++byteArrayLength;
        }
        byte[] bytes = new byte[byteArrayLength];
        int j = 0;
        for (int i = 0; i < byteCount; i += 2, j++) {
          int index = littleEndian ? i : bytes.Length - 1 - i;
          int index2 = littleEndian ? i + 1 : bytes.Length - 2 - i;
          bytes[index] = (byte)(this.reg[j] & 0xff);
          if (index2 >= 0 && index2 < byteArrayLength) {
            bytes[index2] = (byte)((this.reg[j] >> 8) & 0xff);
          }
        }
        return bytes;
      } else {
        short[] regdata = new short[this.reg.Length];
        Array.Copy(this.reg, regdata, this.reg.Length);
        TwosComplement(regdata, 0, (int)regdata.Length);
        int byteCount = regdata.Length * 2;
        for (int i = regdata.Length - 1; i >= 0; --i) {
          if (regdata[i] == unchecked((short)0xffff)) {
            byteCount -= 2;
          } else if ((regdata[i] & 0xff80) == 0xff80) {
            // signed first byte, 0xff second
            --byteCount;
            break;
          } else if ((regdata[i] & 0x8000) == 0x8000) {
            // signed second byte
            break;
          } else {
            // unsigned second byte
            ++byteCount;
            break;
          }
        }
        if (byteCount == 0) {
          byteCount = 1;
        }
        byte[] bytes = new byte[byteCount];
        bytes[littleEndian ? bytes.Length - 1 : 0] = (byte)0xff;
        byteCount = Math.Min(byteCount, regdata.Length * 2);
        int j = 0;
        for (int i = 0; i < byteCount; i += 2, j++) {
          int index = littleEndian ? i : bytes.Length - 1 - i;
          int index2 = littleEndian ? i + 1 : bytes.Length - 2 - i;
          bytes[index] = (byte)(regdata[j] & 0xff);
          if (index2 >= 0 && index2 < byteCount) {
            bytes[index2] = (byte)((regdata[j] >> 8) & 0xff);
          }
        }
        return bytes;
      }
    }

    /// <summary>Shifts this object&apos;s value by a number of bits. A value
    /// of 1 doubles this value, a value of 2 multiplies it by 4, a value of 3 by
    /// 8, a value of 4 by 16, and so on.</summary>
    /// <param name='numberBits'>The number of bits to shift. Can be negative,
    /// in which case this is the same as shiftRight with the absolute value
    /// of numberBits.</param>
    /// <returns>A BigInteger object.</returns>
    public BigInteger shiftLeft(int numberBits) {
      if (numberBits == 0) {
        return this;
      }
      if (numberBits < 0) {
        if (numberBits == Int32.MinValue) {
          return this.shiftRight(1).shiftRight(Int32.MaxValue);
        }
        return this.shiftRight(-numberBits);
      }
      BigInteger ret = new BigInteger();
      int numWords = (int)this.wordCount;
      int shiftWords = (int)(numberBits >> 4);
      int shiftBits = (int)(numberBits & 15);
      bool neg = numWords > 0 && this.negative;
      if (!neg) {
        ret.negative = false;
        ret.reg = new short[RoundupSize(numWords + BitsToWords((int)numberBits))];
        Array.Copy(this.reg, 0, ret.reg, shiftWords, numWords);
        ShiftWordsLeftByBits(ret.reg, (int)shiftWords, numWords + BitsToWords(shiftBits), shiftBits);
        ret.wordCount = ret.CalcWordCount();
      } else {
        ret.negative = true;
        ret.reg = new short[RoundupSize(numWords + BitsToWords((int)numberBits))];
        Array.Copy(this.reg, ret.reg, numWords);
        TwosComplement(ret.reg, 0, (int)ret.reg.Length);
        ShiftWordsLeftByWords(ret.reg, 0, numWords + shiftWords, shiftWords);
        ShiftWordsLeftByBits(ret.reg, (int)shiftWords, numWords + BitsToWords(shiftBits), shiftBits);
        TwosComplement(ret.reg, 0, (int)ret.reg.Length);
        ret.wordCount = ret.CalcWordCount();
      }
      return ret;
    }

    /// <summary>Returns a big integer with the bits shifted to the right.</summary>
    /// <returns>A BigInteger object.</returns>
    /// <param name='numberBits'>Number of bits to shift right.</param>
    public BigInteger shiftRight(int numberBits) {
      if (numberBits == 0 || this.wordCount == 0) {
        return this;
      }
      if (numberBits < 0) {
        if (numberBits == Int32.MinValue) {
          return this.shiftLeft(1).shiftLeft(Int32.MaxValue);
        }
        return this.shiftLeft(-numberBits);
      }
      BigInteger ret;
      int numWords = (int)this.wordCount;
      int shiftWords = (int)(numberBits >> 4);
      int shiftBits = (int)(numberBits & 15);
      if (this.negative) {
        ret = new BigInteger();
        ret.reg = new short[this.reg.Length];
        Array.Copy(this.reg, ret.reg, numWords);
        TwosComplement(ret.reg, 0, (int)ret.reg.Length);
        ShiftWordsRightByWordsSignExtend(ret.reg, 0, numWords, shiftWords);
        if (numWords > shiftWords) {
          ShiftWordsRightByBitsSignExtend(ret.reg, 0, numWords - shiftWords, shiftBits);
        }
        TwosComplement(ret.reg, 0, (int)ret.reg.Length);
        ret.wordCount = ret.reg.Length;
      } else {
        if (shiftWords >= numWords) {
          return BigInteger.Zero;
        }
        ret = new BigInteger();
        ret.reg = new short[this.reg.Length];
        Array.Copy(this.reg, shiftWords, ret.reg, 0, numWords - shiftWords);
        if (shiftBits != 0) {
          ShiftWordsRightByBits(ret.reg, 0, numWords - shiftWords, shiftBits);
        }
        ret.wordCount = numWords - shiftWords;
      }
      ret.negative = this.negative;
      while (ret.wordCount != 0 &&
             ret.reg[ret.wordCount - 1] == 0) {
        --ret.wordCount;
      }
      if (shiftWords > 2) {
        this.ShortenArray();
      }
      return ret;
    }

    /// <summary>Converts a 64-bit signed integer to a big integer.</summary>
    /// <returns>A BigInteger object with the same value as the 64-bit number.</returns>
    /// <param name='longerValue'>A 64-bit signed integer.</param>
    public static BigInteger valueOf(long longerValue) {
      if (longerValue == 0) {
        return BigInteger.Zero;
      }
      if (longerValue == 1) {
        return BigInteger.One;
      }
      BigInteger ret = new BigInteger();
      unchecked {
        ret.negative = longerValue < 0;
        ret.reg = new short[4];
        if (longerValue == Int64.MinValue) {
          ret.reg[0] = 0;
          ret.reg[1] = 0;
          ret.reg[2] = 0;
          ret.reg[3] = (short)0x8000;
          ret.wordCount = 4;
        } else {
          long ut = longerValue;
          if (ut < 0) {
            ut = -ut;
          }
          ret.reg[0] = (short)(ut & 0xffff);
          ut >>= 16;
          ret.reg[1] = (short)(ut & 0xffff);
          ut >>= 16;
          ret.reg[2] = (short)(ut & 0xffff);
          ut >>= 16;
          ret.reg[3] = (short)(ut & 0xffff);
          // at this point, the word count can't
          // be 0 (the check for 0 was already done above)
          ret.wordCount = 4;
          while (ret.wordCount != 0 &&
                 ret.reg[ret.wordCount - 1] == 0) {
            --ret.wordCount;
          }
        }
      }
      return ret;
    }

    /// <summary>Converts this object's value to a 32-bit signed integer.</summary>
    /// <returns>A 32-bit signed integer.</returns>
    /// <exception cref='OverflowException'>This object's value is too
    /// big to fit a 32-bit signed integer.</exception>
    public int intValue() {
      int c = (int)this.wordCount;
      if (c == 0) {
        return 0;
      }
      if (c > 2) {
        throw new OverflowException();
      }
      if (c == 2 && (this.reg[1] & 0x8000) != 0) {
        if (((short)(this.reg[1] & (short)0x7fff) | this.reg[0]) == 0 && this.negative) {
          return Int32.MinValue;
        } else {
          throw new OverflowException();
        }
      } else {
        int ivv = ((int)this.reg[0]) & 0xffff;
        if (c > 1) {
          ivv |= (((int)this.reg[1]) & 0xffff) << 16;
        }
        if (this.negative) {
          ivv = -ivv;
        }
        return ivv;
      }
    }

    /// <summary>Returns whether this object's value can fit in a 32-bit
    /// signed integer.</summary>
    /// <returns>True if this object's value is MinValue or greater, and
    /// MaxValue or less; otherwise, false.</returns>
    public bool canFitInInt() {
      int c = (int)this.wordCount;
      if (c > 2) {
        return false;
      }
      if (c == 2 && (this.reg[1] & 0x8000) != 0) {
        return this.negative && this.reg[1] == unchecked((short)0x8000) &&
          this.reg[0] == 0;
      }
      return true;
    }

    private bool HasSmallValue() {
      int c = (int)this.wordCount;
      if (c > 4) {
        return false;
      }
      if (c == 4 && (this.reg[3] & 0x8000) != 0) {
        return this.negative && this.reg[3] == unchecked((short)0x8000) &&
          this.reg[2] == 0 &&
          this.reg[1] == 0 &&
          this.reg[0] == 0;
      }
      return true;
    }

    /// <summary>Converts this object's value to a 64-bit signed integer.</summary>
    /// <returns>A 64-bit signed integer.</returns>
    /// <exception cref='OverflowException'>This object's value is too
    /// big to fit a 64-bit signed integer.</exception>
    public long longValue() {
      int count = this.wordCount;
      if (count == 0) {
        return (long)0;
      }
      if (count > 4) {
        throw new OverflowException();
      }
      if (count == 4 && (this.reg[3] & 0x8000) != 0) {
        if (this.negative && this.reg[3] == unchecked((short)0x8000) &&
            this.reg[2] == 0 &&
            this.reg[1] == 0 &&
            this.reg[0] == 0) {
          return Int64.MinValue;
        } else {
          throw new OverflowException();
        }
      } else {
        int tmp = ((int)this.reg[0]) & 0xffff;
        long vv = (long)tmp;
        if (count > 1) {
          tmp = ((int)this.reg[1]) & 0xffff;
          vv |= (long)tmp << 16;
        }
        if (count > 2) {
          tmp = ((int)this.reg[2]) & 0xffff;
          vv |= (long)tmp << 32;
        }
        if (count > 3) {
          tmp = ((int)this.reg[3]) & 0xffff;
          vv |= (long)tmp << 48;
        }
        if (this.negative) {
          vv = -vv;
        }
        return vv;
      }
    }

    private static BigInteger Power2(int e) {
      BigInteger r = new BigInteger().Allocate(BitsToWords((int)(e + 1)));
      r.SetBitInternal((int)e, true);  // NOTE: Will recalculate word count
      return r;
    }

    /// <summary>Not documented yet.</summary>
    /// <returns>A BigInteger object.</returns>
    /// <param name='power'>A BigInteger object. (2).</param>
    /// <exception cref='System.ArgumentNullException'>The parameter
    /// <paramref name='power'/> is null.</exception>
    public BigInteger PowBigIntVar(BigInteger power) {
      if (power == null) {
        throw new ArgumentNullException("power");
      }
      int sign = power.Sign;
      if (sign < 0) {
        throw new ArgumentException("sign (" + Convert.ToString((long)sign, System.Globalization.CultureInfo.InvariantCulture) + ") is less than " + "0");
      }
      BigInteger thisVar = this;
      if (sign == 0) {
        return BigInteger.One;
      } else if (power.Equals(BigInteger.One)) {
        return this;
      } else if (power.wordCount == 1 && power.reg[0] == 2) {
        return thisVar * (BigInteger)thisVar;
      } else if (power.wordCount == 1 && power.reg[0] == 3) {
        return (thisVar * (BigInteger)thisVar) * (BigInteger)thisVar;
      }
      BigInteger r = BigInteger.One;
      while (!power.IsZero) {
        if (!power.IsEven) {
          r *= (BigInteger)thisVar;
        }
        power >>= 1;
        if (!power.IsZero) {
          thisVar *= (BigInteger)thisVar;
        }
      }
      return r;
    }

    /// <summary>Not documented yet.</summary>
    /// <param name='powerSmall'>A 32-bit signed integer.</param>
    /// <returns>A BigInteger object.</returns>
    public BigInteger pow(int powerSmall) {
      if (powerSmall < 0) {
        throw new ArgumentException("powerSmall (" + Convert.ToString((long)powerSmall, System.Globalization.CultureInfo.InvariantCulture) + ") is less than " + "0");
      }
      BigInteger thisVar = this;
      if (powerSmall == 0) {
        // however 0 to the power of 0 is undefined
        return BigInteger.One;
      } else if (powerSmall == 1) {
        return this;
      } else if (powerSmall == 2) {
        return thisVar * (BigInteger)thisVar;
      } else if (powerSmall == 3) {
        return (thisVar * (BigInteger)thisVar) * (BigInteger)thisVar;
      }
      BigInteger r = BigInteger.One;
      while (powerSmall != 0) {
        if ((powerSmall & 1) != 0) {
          r *= (BigInteger)thisVar;
        }
        powerSmall >>= 1;
        if (powerSmall != 0) {
          thisVar *= (BigInteger)thisVar;
        }
      }
      return r;
    }

    /// <summary>Gets the value of this object with the sign reversed.</summary>
    /// <returns>This object's value with the sign reversed.</returns>
    public BigInteger negate() {
      BigInteger bigintRet = new BigInteger();
      bigintRet.reg = this.reg;  // use the same reference
      bigintRet.wordCount = this.wordCount;
      bigintRet.negative = (this.wordCount != 0) && (!this.negative);
      return bigintRet;
    }

    /// <summary>Returns the absolute value of this object's value.</summary>
    /// <returns>This object's value with the sign removed.</returns>
    public BigInteger abs() {
      return (this.wordCount == 0 || !this.negative) ? this : this.negate();
    }

    private int CalcWordCount() {
      return (int)CountWords(this.reg, this.reg.Length);
    }

    private int ByteCount() {
      int wc = this.wordCount;
      if (wc == 0) {
        return 0;
      }
      short s = this.reg[wc - 1];
      wc = (wc - 1) << 1;
      if (s == 0) {
        return wc;
      }
      return ((s >> 8) == 0) ? wc + 1 : wc + 2;
    }

    /// <summary>Finds the minimum number of bits needed to represent this
    /// object&apos;s absolute value.</summary>
    /// <returns>The number of bits in this object's value. Returns 0 if this
    /// object's value is 0, and returns 1 if the value is negative 1.</returns>
    public int getUnsignedBitLength() {
      int wc = this.wordCount;
      if (wc != 0) {
        int numberValue = ((int)this.reg[wc - 1]) & 0xffff;
        wc = (wc - 1) << 4;
        if (numberValue == 0) {
          return wc;
        }
        wc += 16;
        unchecked {
          if ((numberValue >> 8) == 0) {
            numberValue <<= 8;
            wc -= 8;
          }
          if ((numberValue >> 12) == 0) {
            numberValue <<= 4;
            wc -= 4;
          }
          if ((numberValue >> 14) == 0) {
            numberValue <<= 2;
            wc -= 2;
          }
          if ((numberValue >> 15) == 0) {
            --wc;
          }
        }
        return wc;
      } else {
        return 0;
      }
    }

    private static int getUnsignedBitLengthEx(int numberValue, int wordCount) {
      int wc = wordCount;
      if (wc != 0) {
        wc = (wc - 1) << 4;
        if (numberValue == 0) {
          return wc;
        }
        wc += 16;
        unchecked {
          if ((numberValue >> 8) == 0) {
            numberValue <<= 8;
            wc -= 8;
          }
          if ((numberValue >> 12) == 0) {
            numberValue <<= 4;
            wc -= 4;
          }
          if ((numberValue >> 14) == 0) {
            numberValue <<= 2;
            wc -= 2;
          }
          if ((numberValue >> 15) == 0) {
            --wc;
          }
        }
        return wc;
      } else {
        return 0;
      }
    }

    /// <summary>Finds the minimum number of bits needed to represent this
    /// object&apos;s value, except for its sign. If the value is negative,
    /// finds the number of bits in (its absolute value minus 1).</summary>
    /// <returns>The number of bits in this object's value. Returns 0 if this
    /// object's value is 0 or negative 1.</returns>
    public int bitLength() {
      int wc = this.wordCount;
      if (wc != 0) {
        if (this.negative && !(wc >= 2 && this.reg[0] != 0)) {
          return this.abs().subtract(BigInteger.One).bitLength();
        }
        int numberValue = ((int)this.reg[wc - 1]) & 0xffff;
        wc = (wc - 1) << 4;
        if (numberValue == 0) {
          return wc;
        }
        wc += 16;
        unchecked {
          if (this.negative) {
            --numberValue;
            numberValue &= 0xffff;
          }
          if ((numberValue >> 8) == 0) {
            numberValue <<= 8;
            wc -= 8;
          }
          if ((numberValue >> 12) == 0) {
            numberValue <<= 4;
            wc -= 4;
          }
          if ((numberValue >> 14) == 0) {
            numberValue <<= 2;
            wc -= 2;
          }
          return ((numberValue >> 15) == 0) ? wc - 1 : wc;
        }
      } else {
        return 0;
      }
    }

    private const string HexChars = "0123456789ABCDEF";

    private static void ReverseChars(char[] chars, int offset, int length) {
      int half = length >> 1;
      int right = offset + length - 1;
      for (int i = 0; i < half; i++, right--) {
        char value = chars[offset + i];
        chars[offset + i] = chars[right];
        chars[right] = value;
      }
    }

    private string SmallValueToString() {
      long value = this.longValue();
      if (value == Int64.MinValue) {
        return "-9223372036854775808";
      }
      bool neg = value < 0;
      char[] chars = new char[24];
      int count = 0;
      if (neg) {
        chars[0] = '-';
        ++count;
        value = -value;
      }
      while (value != 0) {
        char digit = HexChars[(int)(value % 10)];
        chars[count++] = digit;
        value /= 10;
      }
      if (neg) {
        ReverseChars(chars, 1, count - 1);
      } else {
        ReverseChars(chars, 0, count);
      }
      return new String(chars, 0, count);
    }

    private static int ApproxLogTenOfTwo(int bitlen) {
      int bitlenLow = bitlen & 0xffff;
      int bitlenHigh = (bitlen >> 16) & 0xffff;
      short resultLow = 0;
      short resultHigh = 0;
      unchecked {
        int p; short c; int d;
        p = bitlenLow * 0x84fb; d = ((int)p >> 16) & 0xffff; c = (short)d; d = ((int)d >> 16) & 0xffff;
        p = bitlenLow * 0x209a;
        p += ((int)c) & 0xffff; c = (short)p;
        d += ((int)p >> 16) & 0xffff;
        p = bitlenHigh * 0x84fb;
        p += ((int)c) & 0xffff;
        d += ((int)p >> 16) & 0xffff; c = (short)d; d = ((int)d >> 16) & 0xffff;
        p = bitlenLow * 0x9a;
        p += ((int)c) & 0xffff; c = (short)p;
        d += ((int)p >> 16) & 0xffff;
        p = bitlenHigh * 0x209a;
        p += ((int)c) & 0xffff; c = (short)p;
        d += ((int)p >> 16) & 0xffff;
        p = ((int)c) & 0xffff; c = (short)p; resultLow = c; c = (short)d; d = ((int)d >> 16) & 0xffff;
        p = bitlenHigh * 0x9a;
        p += ((int)c) & 0xffff;
        resultHigh = (short)p;
        int result = ((int)resultLow) & 0xffff;
        result |= (((int)resultHigh) & 0xffff) << 16;
        return (result & 0x7fffffff) >> 9;
      }
    }

    /// <summary>Finds the number of decimal digits this number has.</summary>
    /// <returns>The number of decimal digits. Returns 1 if this object'
    /// s value is 0.</returns>
    public int getDigitCount() {
      if (this.IsZero) {
        return 1;
      }
      if (this.HasSmallValue()) {
        long value = this.longValue();
        if (value == Int64.MinValue) {
          return 19;
        }
        if (value < 0) {
          value = -value;
        }
        if (value >= 1000000000L) {
          if (value >= 1000000000000000000L) {
            return 19;
          }
          if (value >= 100000000000000000L) {
            return 18;
          }
          if (value >= 10000000000000000L) {
            return 17;
          }
          if (value >= 1000000000000000L) {
            return 16;
          }
          if (value >= 100000000000000L) {
            return 15;
          }
          if (value >= 10000000000000L) {
            return 14;
          }
          if (value >= 1000000000000L) {
            return 13;
          }
          if (value >= 100000000000L) {
            return 12;
          }
          if (value >= 10000000000L) {
            return 11;
          }
          if (value >= 1000000000L) {
            return 10;
          }
          return 9;
        } else {
          int v2 = (int)value;
          if (v2 >= 100000000) {
            return 9;
          }
          if (v2 >= 10000000) {
            return 8;
          }
          if (v2 >= 1000000) {
            return 7;
          }
          if (v2 >= 100000) {
            return 6;
          }
          if (v2 >= 10000) {
            return 5;
          }
          if (v2 >= 1000) {
            return 4;
          }
          if (v2 >= 100) {
            return 3;
          }
          if (v2 >= 10) {
            return 2;
          }
          return 1;
        }
      }
      int bitlen = this.getUnsignedBitLength();
      if (bitlen <= 2135) {
        // (x*631305) >> 21 is an approximation
        // to trunc(x*log10(2)) that is correct up
        // to x = 2135; the multiplication would require
        // up to 31 bits in all cases up to 2135
        // (cases up to 64 are already handled above)
        int minDigits = 1 + (((bitlen - 1) * 631305) >> 21);
        int maxDigits = 1 + ((bitlen * 631305) >> 21);
        if (minDigits == maxDigits) {
          // Number of digits is the same for
          // all numbers with this bit length
          return minDigits;
        }
      } else if (bitlen <= 6432162) {
        // Much more accurate approximation
        int minDigits = ApproxLogTenOfTwo(bitlen - 1);
        int maxDigits = ApproxLogTenOfTwo(bitlen);
        if (minDigits == maxDigits) {
          // Number of digits is the same for
          // all numbers with this bit length
          return 1 + minDigits;
        }
      }
      short[] tempReg = null;
      int wordCount = this.wordCount;
      int i = 0;
      while (wordCount != 0) {
        if (wordCount == 1 || (wordCount == 2 && tempReg[1] == 0)) {
          int rest = ((int)tempReg[0]) & 0xffff;
          if (rest >= 10000) {
            i += 5;
          } else if (rest >= 1000) {
            i += 4;
          } else if (rest >= 100) {
            i += 3;
          } else if (rest >= 10) {
            i += 2;
          } else {
            ++i;
          }
          break;
        } else if (wordCount == 2 && tempReg[1] > 0 && tempReg[1] <= 0x7fff) {
          int rest = ((int)tempReg[0]) & 0xffff;
          rest |= (((int)tempReg[1]) & 0xffff) << 16;
          if (rest >= 1000000000) {
            i += 10;
          } else if (rest >= 100000000) {
            i += 9;
          } else if (rest >= 10000000) {
            i += 8;
          } else if (rest >= 1000000) {
            i += 7;
          } else if (rest >= 100000) {
            i += 6;
          } else if (rest >= 10000) {
            i += 5;
          } else if (rest >= 1000) {
            i += 4;
          } else if (rest >= 100) {
            i += 3;
          } else if (rest >= 10) {
            i += 2;
          } else {
            ++i;
          }
          break;
        } else {
          int wci = wordCount;
          short remainderShort = 0;
          int quo, rem;
          bool firstdigit = false;
          short[] dividend = (tempReg == null) ? this.reg : tempReg;
          // Divide by 10000
          while ((wci--) > 0) {
            int curValue = ((int)dividend[wci]) & 0xffff;
            int currentDividend = unchecked((int)(curValue |
                                                  ((int)remainderShort << 16)));
            quo = currentDividend / 10000;
            if (!firstdigit && quo != 0) {
              firstdigit = true;
              // Since we are dividing from left to right, the first
              // nonzero result is the first part of the
              // new quotient
              bitlen = getUnsignedBitLengthEx(quo, wci + 1);
              if (bitlen <= 2135) {
                // (x*631305) >> 21 is an approximation
                // to trunc(x*log10(2)) that is correct up
                // to x = 2135; the multiplication would require
                // up to 31 bits in all cases up to 2135
                // (cases up to 64 are already handled above)
                int minDigits = 1 + (((bitlen - 1) * 631305) >> 21);
                int maxDigits = 1 + ((bitlen * 631305) >> 21);
                if (minDigits == maxDigits) {
                  // Number of digits is the same for
                  // all numbers with this bit length
                  return i + minDigits + 4;
                }
              } else if (bitlen <= 6432162) {
                // Much more accurate approximation
                int minDigits = ApproxLogTenOfTwo(bitlen - 1);
                int maxDigits = ApproxLogTenOfTwo(bitlen);
                if (minDigits == maxDigits) {
                  // Number of digits is the same for
                  // all numbers with this bit length
                  return i + 1 + minDigits + 4;
                }
              }
            }
            if (tempReg == null) {
              if (quo != 0) {
                tempReg = new short[this.wordCount];
                Array.Copy(this.reg, tempReg, tempReg.Length);
                // Use the calculated word count during division;
                // zeros that may have occurred in division
                // are not incorporated in the tempReg
                wordCount = wci + 1;
                tempReg[wci] = unchecked((short)quo);
              }
            } else {
              tempReg[wci] = unchecked((short)quo);
            }
            rem = currentDividend - (10000 * quo);
            remainderShort = unchecked((short)rem);
          }
          // Recalculate word count
          while (wordCount != 0 && tempReg[wordCount - 1] == 0) {
            --wordCount;
          }
          i += 4;
        }
      }
      return i;
    }

    /// <summary>Converts this object to a text string.</summary>
    /// <returns>A string representation of this object.</returns>
    public override string ToString() {
      if (this.IsZero) {
        return "0";
      }
      if (this.HasSmallValue()) {
        return this.SmallValueToString();
      }
      short[] tempReg = new short[this.wordCount];
      Array.Copy(this.reg, tempReg, tempReg.Length);
      int wordCount = tempReg.Length;
      while (wordCount != 0 && tempReg[wordCount - 1] == 0) {
        --wordCount;
      }
      int i = 0;
      char[] s = new char[(wordCount << 4) + 1];
      while (wordCount != 0) {
        if (wordCount == 1 && tempReg[0] > 0 && tempReg[0] <= 0x7fff) {
          int rest = tempReg[0];
          while (rest != 0) {
            // accurate approximation to rest/10 up to 43698,
            // and rest can go up to 32767
            int newrest = (rest * 26215) >> 18;
            s[i++] = HexChars[rest - (newrest * 10)];
            rest = newrest;
          }
          break;
        } else if (wordCount == 2 && tempReg[1] > 0 && tempReg[1] <= 0x7fff) {
          int rest = ((int)tempReg[0]) & 0xffff;
          rest |= (((int)tempReg[1]) & 0xffff) << 16;
          while (rest != 0) {
            int newrest = rest / 10;
            s[i++] = HexChars[rest - (newrest * 10)];
            rest = newrest;
          }
          break;
        } else {
          int wci = wordCount;
          short remainderShort = 0;
          int quo, rem;
          // Divide by 10000
          while ((wci--) > 0) {
            int currentDividend = unchecked((int)((((int)tempReg[wci]) & 0xffff) |
                                                  ((int)remainderShort << 16)));
            quo = currentDividend / 10000;
            tempReg[wci] = unchecked((short)quo);
            rem = currentDividend - (10000 * quo);
            remainderShort = unchecked((short)rem);
          }
          int remainderSmall = remainderShort;
          // Recalculate word count
          while (wordCount != 0 && tempReg[wordCount - 1] == 0) {
            --wordCount;
          }
          // accurate approximation to rest/10 up to 16388,
          // and rest can go up to 9999
          int newrest = (remainderSmall * 3277) >> 15;
          s[i++] = HexChars[(int)(remainderSmall - (newrest * 10))];
          remainderSmall = newrest;
          newrest = (remainderSmall * 3277) >> 15;
          s[i++] = HexChars[(int)(remainderSmall - (newrest * 10))];
          remainderSmall = newrest;
          newrest = (remainderSmall * 3277) >> 15;
          s[i++] = HexChars[(int)(remainderSmall - (newrest * 10))];
          remainderSmall = newrest;
          s[i++] = HexChars[remainderSmall];
        }
      }
      ReverseChars(s, 0, i);
      if (this.negative) {
        System.Text.StringBuilder sb = new System.Text.StringBuilder(i + 1);
        sb.Append('-');
        sb.Append(s, 0, i);
        return sb.ToString();
      } else {
        return new String(s, 0, i);
      }
    }

    /// <summary>Converts a string to an arbitrary-precision integer.</summary>
    /// <param name='str'>A string containing only digits, except that
    /// it may start with a minus sign.</param>
    /// <returns>A BigInteger object with the same value as given in the string.</returns>
    /// <exception cref='System.ArgumentNullException'>The parameter
    /// <paramref name='str'/> is null.</exception>
    /// <exception cref='FormatException'>The parameter <paramref name='str'/>
    /// is in an invalid format.</exception>
    public static BigInteger fromString(string str) {
      if (str == null) {
        throw new ArgumentNullException("str");
      }
      return fromSubstring(str, 0, str.Length);
    }

    private const int MaxSafeInt = 214748363;

    /// <summary>Converts a portion of a string to an arbitrary-precision
    /// integer.</summary>
    /// <returns>A BigInteger object with the same value as given in the string
    /// portion.</returns>
    /// <param name='str'>A string object.</param>
    /// <param name='index'>The index of the string that starts the string
    /// portion.</param>
    /// <param name='endIndex'>The index of the string that ends the string
    /// portion. The length will be index + endIndex - 1.</param>
    /// <exception cref='System.ArgumentNullException'>The parameter
    /// <paramref name='str'/> is null.</exception>
    /// <exception cref='FormatException'>The string portion is in an
    /// invalid format.</exception>
    public static BigInteger fromSubstring(string str, int index, int endIndex) {
      if (str == null) {
        throw new ArgumentNullException("str");
      }
      if (index < 0) {
        throw new ArgumentException("index (" + Convert.ToString((long)index, System.Globalization.CultureInfo.InvariantCulture) + ") is less than " + "0");
      }
      if (index > str.Length) {
        throw new ArgumentException("index (" + Convert.ToString((long)index, System.Globalization.CultureInfo.InvariantCulture) + ") is more than " + Convert.ToString((long)str.Length, System.Globalization.CultureInfo.InvariantCulture));
      }
      if (endIndex < 0) {
        throw new ArgumentException("endIndex (" + Convert.ToString((long)endIndex, System.Globalization.CultureInfo.InvariantCulture) + ") is less than " + "0");
      }
      if (endIndex > str.Length) {
        throw new ArgumentException("endIndex (" + Convert.ToString((long)endIndex, System.Globalization.CultureInfo.InvariantCulture) + ") is more than " + Convert.ToString((long)str.Length, System.Globalization.CultureInfo.InvariantCulture));
      }
      if (endIndex < index) {
        throw new ArgumentException("endIndex (" + Convert.ToString((long)endIndex, System.Globalization.CultureInfo.InvariantCulture) + ") is less than " + Convert.ToString((long)index, System.Globalization.CultureInfo.InvariantCulture));
      }
      if (index == endIndex) {
        throw new FormatException("No digits");
      }
      bool negative = false;
      if (str[0] == '-') {
        ++index;
        negative = true;
      }
      BigInteger bigint = new BigInteger().Allocate(4);
      bool haveDigits = false;
      bool haveSmallInt = true;
      int smallInt = 0;
      for (int i = index; i < endIndex; ++i) {
        char c = str[i];
        if (c < '0' || c > '9') {
          throw new FormatException("Illegal character found");
        }
        haveDigits = true;
        int digit = (int)(c - '0');
        if (haveSmallInt && smallInt < MaxSafeInt) {
          smallInt *= 10;
          smallInt += digit;
        } else {
          if (haveSmallInt) {
            bigint.reg[0] = unchecked((short)(smallInt & 0xffff));
            bigint.reg[1] = unchecked((short)((smallInt >> 16) & 0xffff));
            haveSmallInt = false;
          }
          // Multiply by 10
          short carry = 0;
          int n = bigint.reg.Length;
          for (int j = 0; j < n; ++j) {
            int p;
            unchecked {
              p = (((int)bigint.reg[j]) & 0xffff) * 10;
              p += ((int)carry) & 0xffff;
              bigint.reg[j] = (short)p;
              carry = (short)(p >> 16);
            }
          }
          if (carry != 0) {
            bigint.reg = GrowForCarry(bigint.reg, carry);
          }
          // Add the parsed digit
          if (digit != 0) {
            int d = bigint.reg[0] & 0xffff;
            if (d <= 65526) {
              bigint.reg[0] = unchecked((short)(d + digit));
            } else if (Increment(bigint.reg, 0, bigint.reg.Length, (short)digit) != 0) {
              bigint.reg = GrowForCarry(bigint.reg, (short)1);
            }
          }
        }
      }
      if (!haveDigits) {
        throw new FormatException("No digits");
      }
      if (haveSmallInt) {
        bigint.reg[0] = unchecked((short)(smallInt & 0xffff));
        bigint.reg[1] = unchecked((short)((smallInt >> 16) & 0xffff));
      }
      bigint.wordCount = bigint.CalcWordCount();
      bigint.negative = bigint.wordCount != 0 && negative;
      return bigint;
    }

    /// <summary>Not documented yet.</summary>
    /// <returns>A 32-bit signed integer.</returns>
    public int getLowestSetBit() {
      int retSetBit = 0;
      for (int i = 0; i < this.wordCount; ++i) {
        short c = this.reg[i];
        if (c == (short)0) {
          retSetBit += 16;
        } else {
          if (((c << 15) & 0xffff) != 0) {
            return retSetBit + 0;
          }
          if (((c << 14) & 0xffff) != 0) {
            return retSetBit + 1;
          }
          if (((c << 13) & 0xffff) != 0) {
            return retSetBit + 2;
          }
          if (((c << 12) & 0xffff) != 0) {
            return retSetBit + 3;
          }
          if (((c << 11) & 0xffff) != 0) {
            return retSetBit + 4;
          }
          if (((c << 10) & 0xffff) != 0) {
            return retSetBit + 5;
          }
          if (((c << 9) & 0xffff) != 0) {
            return retSetBit + 6;
          }
          if (((c << 8) & 0xffff) != 0) {
            return retSetBit + 7;
          }
          if (((c << 7) & 0xffff) != 0) {
            return retSetBit + 8;
          }
          if (((c << 6) & 0xffff) != 0) {
            return retSetBit + 9;
          }
          if (((c << 5) & 0xffff) != 0) {
            return retSetBit + 10;
          }
          if (((c << 4) & 0xffff) != 0) {
            return retSetBit + 11;
          }
          if (((c << 3) & 0xffff) != 0) {
            return retSetBit + 12;
          }
          if (((c << 2) & 0xffff) != 0) {
            return retSetBit + 13;
          }
          if (((c << 1) & 0xffff) != 0) {
            return retSetBit + 14;
          }
          return retSetBit + 15;
        }
      }
      return 0;
    }

    /// <summary>Returns the greatest common divisor of two integers.</summary>
    /// <returns>A BigInteger object.</returns>
    /// <remarks>The greatest common divisor (GCD) is also known as the greatest
    /// common factor (GCF).</remarks>
    /// <param name='bigintSecond'>A BigInteger object. (2).</param>
    /// <exception cref='System.ArgumentNullException'>The parameter
    /// <paramref name='bigintSecond'/> is null.</exception>
    public BigInteger gcd(BigInteger bigintSecond) {
      if (bigintSecond == null) {
        throw new ArgumentNullException("bigintSecond");
      }
      if (this.IsZero) {
        return BigInteger.Abs(bigintSecond);
      }
      if (bigintSecond.IsZero) {
        return BigInteger.Abs(this);
      }
      BigInteger thisValue = this.abs();
      bigintSecond = bigintSecond.abs();
      if (bigintSecond.Equals(BigInteger.One) ||
          thisValue.Equals(bigintSecond)) {
        return bigintSecond;
      }
      if (thisValue.Equals(BigInteger.One)) {
        return thisValue;
      }
      int expOfTwo = Math.Min(
        this.getLowestSetBit(),
        bigintSecond.getLowestSetBit());
      if (thisValue.wordCount <= 10 && bigintSecond.wordCount <= 10) {
        while (true) {
          BigInteger bigintA = (thisValue - (BigInteger)bigintSecond).abs();
          if (bigintA.IsZero) {
            if (expOfTwo != 0) {
              thisValue <<= expOfTwo;
            }
            return thisValue;
          }
          int setbit = bigintA.getLowestSetBit();
          bigintA >>= setbit;
          bigintSecond = (thisValue.CompareTo(bigintSecond) < 0) ? thisValue : bigintSecond;
          thisValue = bigintA;
        }
      } else {
        BigInteger temp;
        while (!thisValue.IsZero) {
          if (thisValue.CompareTo(bigintSecond) < 0) {
            temp = thisValue;
            thisValue = bigintSecond;
            bigintSecond = temp;
          }
          thisValue %= (BigInteger)bigintSecond;
        }
        return bigintSecond;
      }
    }

    /// <summary>Calculates the remainder when a BigInteger raised to a
    /// certain power is divided by another BigInteger.</summary>
    /// <param name='pow'>A BigInteger object. (2).</param>
    /// <param name='mod'>A BigInteger object. (3).</param>
    /// <returns>A BigInteger object.</returns>
    /// <exception cref='System.ArgumentNullException'>The parameter
    /// <paramref name='pow'/> is null.</exception>
    public BigInteger ModPow(BigInteger pow, BigInteger mod) {
      if (pow == null) {
        throw new ArgumentNullException("pow");
      }
      if (pow.Sign < 0) {
        throw new ArgumentException("pow (" + pow + ") is less than 0");
      }
      if (mod.Sign <= 0) {
        throw new ArgumentException("mod (" + mod + ") is not greater than 0");
      }
      BigInteger r = BigInteger.One;
      BigInteger v = this;
      while (!pow.IsZero) {
        if (!pow.IsEven) {
          r = (r * (BigInteger)v).mod(mod);
        }
        pow >>= 1;
        if (!pow.IsZero) {
          v = (v * (BigInteger)v).mod(mod);
        }
      }
      return r;
    }

    private static void PositiveSubtract(
      BigInteger bigintDiff,
      BigInteger minuend,
      BigInteger subtrahend) {
      int words1Size = minuend.wordCount;
      words1Size += words1Size & 1;
      int words2Size = subtrahend.wordCount;
      words2Size += words2Size & 1;
      if (words1Size == words2Size) {
        if (Compare(minuend.reg, 0, subtrahend.reg, 0, (int)words1Size) >= 0) {
          // words1 is at least as high as words2
          Subtract(bigintDiff.reg, 0, minuend.reg, 0, subtrahend.reg, 0, (int)words1Size);
          bigintDiff.negative = false;  // difference will not be negative at this point
        } else {
          // words1 is less than words2
          Subtract(bigintDiff.reg, 0, subtrahend.reg, 0, minuend.reg, 0, (int)words1Size);
          bigintDiff.negative = true;  // difference will be negative
        }
      } else if (words1Size > words2Size) {
        // words1 is greater than words2
        short borrow = (short)Subtract(bigintDiff.reg, 0, minuend.reg, 0, subtrahend.reg, 0, (int)words2Size);
        Array.Copy(minuend.reg, words2Size, bigintDiff.reg, words2Size, words1Size - words2Size);
        borrow = (short)Decrement(bigintDiff.reg, words2Size, (int)(words1Size - words2Size), borrow);
        // DebugAssert.IsTrue(borrow==0,"{0} line {1}: !borrow","integer.cpp",3524);
        bigintDiff.negative = false;
      } else {
        // words1 is less than words2
        short borrow = (short)Subtract(bigintDiff.reg, 0, subtrahend.reg, 0, minuend.reg, 0, (int)words1Size);
        Array.Copy(subtrahend.reg, words1Size, bigintDiff.reg, words1Size, words2Size - words1Size);
        borrow = (short)Decrement(bigintDiff.reg, words1Size, (int)(words2Size - words1Size), borrow);
        // DebugAssert.IsTrue(borrow==0,"{0} line {1}: !borrow","integer.cpp",3532);
        bigintDiff.negative = true;
      }
      bigintDiff.wordCount = bigintDiff.CalcWordCount();
      bigintDiff.ShortenArray();
      if (bigintDiff.wordCount == 0) {
        bigintDiff.negative = false;
      }
    }

    #region Equals and GetHashCode implementation
    /// <inheritdoc/><summary>Determines whether this object and another object are
    /// equal.</summary>
    /// <returns>True if the objects are equal; otherwise, false.</returns>
    /// <param name='obj'>An arbitrary object.</param>
    public override bool Equals(object obj) {
      BigInteger other = obj as BigInteger;
      if (other == null) {
        return false;
      }
      if (this.wordCount == other.wordCount) {
        if (this.negative != other.negative) {
          return false;
        }
        for (int i = 0; i < this.wordCount; ++i) {
          if (this.reg[i] != other.reg[i]) {
            return false;
          }
        }
        return true;
      }
      return false;
    }

    /// <summary>Returns the hash code for this instance.</summary>
    /// <returns>A 32-bit hash code.</returns>
    public override int GetHashCode() {
      int hashCodeValue = 0;
      unchecked {
        hashCodeValue += 1000000007 * this.Sign.GetHashCode();
        if (this.reg != null) {
          for (int i = 0; i < this.wordCount; ++i) {
            hashCodeValue += 1000000013 * this.reg[i];
          }
        }
      }
      return hashCodeValue;
    }
    #endregion

    /// <summary>Adds this object and another object.</summary>
    /// <returns>The sum of the two objects.</returns>
    /// <param name='bigintAugend'>A BigInteger object.</param>
    /// <exception cref='System.ArgumentNullException'>The parameter
    /// <paramref name='bigintAugend'/> is null.</exception>
    public BigInteger add(BigInteger bigintAugend) {
      if (bigintAugend == null) {
        throw new ArgumentNullException("bigintAugend");
      }
      BigInteger sum;
      if (this.wordCount == 0) {
        return bigintAugend;
      }
      if (bigintAugend.wordCount == 0) {
        return this;
      }
      if (bigintAugend.wordCount == 1 && this.wordCount == 1) {
        if (this.negative == bigintAugend.negative) {
          int intSum = (((int)this.reg[0]) & 0xffff) + (((int)bigintAugend.reg[0]) & 0xffff);
          sum = new BigInteger();
          sum.reg = new short[2];
          sum.reg[0] = unchecked((short)intSum);
          sum.reg[1] = unchecked((short)(intSum >> 16));
          sum.wordCount = ((intSum >> 16) == 0) ? 1 : 2;
          sum.negative = this.negative;
          return sum;
        } else {
          int a = ((int)this.reg[0]) & 0xffff;
          int b = ((int)bigintAugend.reg[0]) & 0xffff;
          if (a == b) {
            return BigInteger.Zero;
          }
          if (a > b) {
            a -= b;
            sum = new BigInteger();
            sum.reg = new short[2];
            sum.reg[0] = unchecked((short)a);
            sum.wordCount = 1;
            sum.negative = this.negative;
            return sum;
          } else {
            b -= a;
            sum = new BigInteger();
            sum.reg = new short[2];
            sum.reg[0] = unchecked((short)b);
            sum.wordCount = 1;
            sum.negative = !this.negative;
            return sum;
          }
        }
      }
      sum = new BigInteger().Allocate((int)Math.Max(this.reg.Length, bigintAugend.reg.Length));
      if ((!this.negative) == (!bigintAugend.negative)) {
        // both nonnegative or both negative
        int carry;
        int addendCount = this.wordCount;
        int augendCount = bigintAugend.wordCount;
        int desiredLength = Math.Max(addendCount, augendCount);
        if (addendCount == augendCount) {
          carry = AddOneByOne(sum.reg, 0, this.reg, 0, bigintAugend.reg, 0, (int)addendCount);
        } else if (addendCount > augendCount) {
          // Addend is bigger
          carry = AddOneByOne(
            sum.reg,
            0,
            this.reg,
            0,
            bigintAugend.reg,
            0,
            augendCount);
          Array.Copy(
            this.reg,
            augendCount,
            sum.reg,
            augendCount,
            addendCount - augendCount);
          if (carry != 0) {
            carry = Increment(
              sum.reg,
              augendCount,
              addendCount - augendCount,
              (short)carry);
          }
        } else {
          // Augend is bigger
          carry = AddOneByOne(
            sum.reg,
            0,
            this.reg,
            0,
            bigintAugend.reg,
            0,
            (int)addendCount);
          Array.Copy(
            bigintAugend.reg,
            addendCount,
            sum.reg,
            addendCount,
            augendCount - addendCount);
          if (carry != 0) {
            carry = Increment(
              sum.reg,
              addendCount,
              (int)(augendCount - addendCount),
              (short)carry);
          }
        }
        bool needShorten = true;
        if (carry != 0) {
          int nextIndex = desiredLength;
          int len = RoundupSize(nextIndex + 1);
          sum.reg = CleanGrow(sum.reg, len);
          sum.reg[nextIndex] = (short)carry;
          needShorten = false;
        }
        sum.negative = false;
        sum.wordCount = sum.CalcWordCount();
        if (needShorten) {
          sum.ShortenArray();
        }
        sum.negative = this.negative && !sum.IsZero;
      } else if (this.negative) {
        PositiveSubtract(sum, bigintAugend, this);  // this is negative, b is nonnegative
      } else {
        PositiveSubtract(sum, this, bigintAugend);  // this is nonnegative, b is negative
      }
      return sum;
    }

    /// <summary>Subtracts a BigInteger from this BigInteger.</summary>
    /// <param name='subtrahend'>A BigInteger object.</param>
    /// <returns>The difference of the two objects.</returns>
    /// <exception cref='System.ArgumentNullException'>The parameter
    /// <paramref name='subtrahend'/> is null.</exception>
    public BigInteger subtract(BigInteger subtrahend) {
      if (subtrahend == null) {
        throw new ArgumentNullException("subtrahend");
      }
      if (this.wordCount == 0) {
        return subtrahend.negate();
      }
      if (subtrahend.wordCount == 0) {
        return this;
      }
      return this.add(subtrahend.negate());
    }

    private void ShortenArray() {
      if (this.reg.Length > 32) {
        int newLength = RoundupSize(this.wordCount);
        if (newLength < this.reg.Length &&
            (this.reg.Length - newLength) >= 16) {
          // Reallocate the array if the rounded length
          // is much smaller than the current length
          short[] newreg = new short[newLength];
          Array.Copy(this.reg, newreg, Math.Min(newLength, this.reg.Length));
          this.reg = newreg;
        }
      }
    }

    /// <summary>Multiplies this instance by the value of a BigInteger object.</summary>
    /// <param name='bigintMult'>A BigInteger object.</param>
    /// <returns>The product of the two objects.</returns>
    /// <exception cref='System.ArgumentNullException'>The parameter
    /// <paramref name='bigintMult'/> is null.</exception>
    public BigInteger multiply(BigInteger bigintMult) {
      if (bigintMult == null) {
        throw new ArgumentNullException("bigintMult");
      }
      if (this.wordCount == 0 || bigintMult.wordCount == 0) {
        return BigInteger.Zero;
      }
      if (this.wordCount == 1 && this.reg[0] == 1) {
        return this.negative ? bigintMult.negate() : bigintMult;
      }
      if (bigintMult.wordCount == 1 && bigintMult.reg[0] == 1) {
        return bigintMult.negative ? this.negate() : this;
      }
      BigInteger product = new BigInteger();
      bool needShorten = true;
      if (this.wordCount == 1) {
        int wc = bigintMult.wordCount;
        int regLength = RoundupSize(wc + 1);
        product.reg = new short[regLength];
        product.reg[wc] = LinearMultiply(product.reg, 0, bigintMult.reg, 0, this.reg[0], wc);
        product.negative = false;
        product.wordCount = product.reg.Length;
        needShorten = false;
      } else if (bigintMult.wordCount == 1) {
        int wc = this.wordCount;
        int regLength = RoundupSize(wc + 1);
        product.reg = new short[regLength];
        product.reg[wc] = LinearMultiply(product.reg, 0, this.reg, 0, bigintMult.reg[0], wc);
        product.negative = false;
        product.wordCount = product.reg.Length;
        needShorten = false;
      } else if (this.Equals(bigintMult)) {
        int words1Size = RoundupSize(this.wordCount);
        product.reg = new short[words1Size + words1Size];
        product.wordCount = product.reg.Length;
        product.negative = false;
        short[] workspace = new short[words1Size + words1Size];
        RecursiveSquare(
          product.reg,
          0,
          workspace,
          0,
          this.reg,
          0,
          words1Size);
      } else if (this.wordCount <= 10 && bigintMult.wordCount <= 10) {
        int wc = this.wordCount + bigintMult.wordCount;
        wc = RoundupSize(wc);
        product.reg = new short[wc];
        product.negative = false;
        product.wordCount = product.reg.Length;
        SchoolbookMultiply(
          product.reg,
          0,
          this.reg,
          0,
          this.wordCount,
          bigintMult.reg,
          0,
          bigintMult.wordCount);
        needShorten = false;
      } else {
        int words1Size = this.wordCount;
        int words2Size = bigintMult.wordCount;
        words1Size = RoundupSize(words1Size);
        words2Size = RoundupSize(words2Size);
        product.reg = new short[RoundupSize(words1Size + words2Size)];
        product.negative = false;
        short[] workspace = new short[words1Size + words2Size];
        product.wordCount = product.reg.Length;
        AsymmetricMultiply(
          product.reg,
          0,
          workspace,
          0,
          this.reg,
          0,
          words1Size,
          bigintMult.reg,
          0,
          words2Size);
      }
      // Recalculate word count
      while (product.wordCount != 0 && product.reg[product.wordCount - 1] == 0) {
        --product.wordCount;
      }
      if (needShorten) {
        product.ShortenArray();
      }
      if (this.negative != bigintMult.negative) {
        product.NegateInternal();
      }
      return product;
    }

    private static int BitsToWords(int bitCount) {
      return (bitCount + 15) >> 4;
    }

    private static short FastRemainder(short[] dividendReg, int count, short divisorSmall) {
      int i = count;
      short remainder = 0;
      while ((i--) > 0) {
        remainder = RemainderUnsigned(
          MakeUint(dividendReg[i], remainder),
          divisorSmall);
      }
      return remainder;
    }

    private static void FastDivide(short[] quotientReg, short[] dividendReg, int count, short divisorSmall) {
      int i = count;
      short remainderShort = 0;
      int idivisor = ((int)divisorSmall) & 0xffff;
      int quo, rem;
      while ((i--) > 0) {
        int currentDividend = unchecked((int)((((int)dividendReg[i]) & 0xffff) |
                                              ((int)remainderShort << 16)));
        if ((currentDividend >> 31) == 0) {
          quo = currentDividend / idivisor;
          quotientReg[i] = unchecked((short)quo);
          if (i > 0) {
            rem = currentDividend - (idivisor * quo);
            remainderShort = unchecked((short)rem);
          }
        } else {
          quotientReg[i] = DivideUnsigned(currentDividend, divisorSmall);
          if (i > 0) {
            remainderShort = RemainderUnsigned(currentDividend, divisorSmall);
          }
        }
      }
    }

    private static short FastDivideAndRemainder(
      short[] quotientReg,
      int quotientStart,
      short[] dividendReg,
      int dividendStart,
      int count,
      short divisorSmall) {
      int i = count;
      short remainderShort = 0;
      int idivisor = ((int)divisorSmall) & 0xffff;
      int quo, rem;
      while ((i--) > 0) {
        int currentDividend = unchecked((int)((((int)dividendReg[dividendStart + i]) & 0xffff) |
                                              ((int)remainderShort << 16)));
        if ((currentDividend >> 31) == 0) {
          quo = currentDividend / idivisor;
          quotientReg[quotientStart + i] = unchecked((short)quo);
          rem = currentDividend - (idivisor * quo);
          remainderShort = unchecked((short)rem);
        } else {
          quotientReg[quotientStart + i] = DivideUnsigned(currentDividend, divisorSmall);
          remainderShort = RemainderUnsigned(currentDividend, divisorSmall);
        }
      }
      return remainderShort;
    }

    /// <summary>Divides this instance by the value of a BigInteger object.
    /// The result is rounded down (the fractional part is discarded). Except
    /// if the result is 0, it will be negative if this object is positive and
    /// the other is negative, or vice versa, and will be positive if both are
    /// positive or both are negative.</summary>
    /// <returns>The quotient of the two objects.</returns>
    /// <exception cref='DivideByZeroException'>The divisor is zero.</exception>
    /// <param name='bigintDivisor'>A BigInteger object.</param>
    /// <exception cref='System.ArgumentNullException'>The parameter
    /// <paramref name='bigintDivisor'/> is null.</exception>
    /// <exception cref='System.DivideByZeroException'>Attempted
    /// to divide by zero.</exception>
    public BigInteger divide(BigInteger bigintDivisor) {
      if (bigintDivisor == null) {
        throw new ArgumentNullException("bigintDivisor");
      }
      int words1Size = this.wordCount;
      int words2Size = bigintDivisor.wordCount;
      // ---- Special cases
      if (words2Size == 0) {
        throw new DivideByZeroException();
      }
      if (words1Size < words2Size) {
        // dividend is less than divisor (includes case
        // where dividend is 0)
        return BigInteger.Zero;
      }
      if (words1Size <= 2 && words2Size <= 2 && this.canFitInInt() && bigintDivisor.canFitInInt()) {
        int valueASmall = this.intValue();
        int valueBSmall = bigintDivisor.intValue();
        if (valueASmall != Int32.MinValue || valueBSmall != -1) {
          int result = valueASmall / valueBSmall;
          return new BigInteger().InitializeInt(result);
        }
      }
      BigInteger quotient;
      if (words2Size == 1) {
        // divisor is small, use a fast path
        quotient = new BigInteger();
        quotient.reg = new short[this.reg.Length];
        quotient.wordCount = this.wordCount;
        quotient.negative = this.negative;
        FastDivide(quotient.reg, this.reg, words1Size, bigintDivisor.reg[0]);
        while (quotient.wordCount != 0 &&
               quotient.reg[quotient.wordCount - 1] == 0) {
          --quotient.wordCount;
        }
        if (quotient.wordCount != 0) {
          quotient.negative = this.negative ^ bigintDivisor.negative;
          return quotient;
        } else {
          return BigInteger.Zero;
        }
      }
      // ---- General case
      quotient = new BigInteger();
      words1Size += words1Size & 1;
      words2Size += words2Size & 1;
      quotient.reg = new short[RoundupSize((int)(words1Size - words2Size + 2))];
      quotient.negative = false;
      short[] tempbuf = new short[words1Size + (3 * (words2Size + 2))];
      Divide(
        null,
        0,
        quotient.reg,
        0,
        tempbuf,
        0,
        this.reg,
        0,
        words1Size,
        bigintDivisor.reg,
        0,
        words2Size);
      quotient.wordCount = quotient.CalcWordCount();
      quotient.ShortenArray();
      if ((this.Sign < 0) ^ (bigintDivisor.Sign < 0)) {
        quotient.NegateInternal();
      }
      return quotient;
    }

    /// <summary>Divides this object by another big integer and returns
    /// the quotient and remainder.</summary>
    /// <param name='divisor'>The divisor.</param>
    /// <returns>An array with two big integers: the first is the quotient,
    /// and the second is the remainder.</returns>
    /// <exception cref='System.ArgumentNullException'>The parameter
    /// divisor is null.</exception>
    /// <exception cref='DivideByZeroException'>The parameter divisor
    /// is 0.</exception>
    /// <exception cref='System.DivideByZeroException'>Attempted
    /// to divide by zero.</exception>
    public BigInteger[] divideAndRemainder(BigInteger divisor) {
      if (divisor == null) {
        throw new ArgumentNullException("divisor");
      }
      BigInteger quotient;
      int words1Size = this.wordCount;
      int words2Size = divisor.wordCount;
      if (words2Size == 0) {
        throw new DivideByZeroException();
      }

      if (words1Size < words2Size) {
        // dividend is less than divisor (includes case
        // where dividend is 0)
        return new BigInteger[] { BigInteger.Zero, this };
      }
      if (words2Size == 1) {
        // divisor is small, use a fast path
        quotient = new BigInteger();
        quotient.reg = new short[this.reg.Length];
        quotient.wordCount = this.wordCount;
        quotient.negative = this.negative;
        int smallRemainder = ((int)FastDivideAndRemainder(
          quotient.reg,
          0,
          this.reg,
          0,
          words1Size,
          divisor.reg[0])) & 0xffff;
        while (quotient.wordCount != 0 &&
               quotient.reg[quotient.wordCount - 1] == 0) {
          --quotient.wordCount;
        }
        quotient.ShortenArray();
        if (quotient.wordCount != 0) {
          quotient.negative = this.negative ^ divisor.negative;
        } else {
          quotient = BigInteger.Zero;
        }
        if (this.negative) {
          smallRemainder = -smallRemainder;
        }
        return new BigInteger[] { quotient, new BigInteger().InitializeInt(smallRemainder) };
      }
      if (this.wordCount == 2 && divisor.wordCount == 2 &&
          (this.reg[1] >> 15) != 0 &&
          (divisor.reg[1] >> 15) != 0) {
        int a = ((int)this.reg[0]) & 0xffff;
        int b = ((int)divisor.reg[0]) & 0xffff;
        unchecked {
          a |= (((int)this.reg[1]) & 0xffff) << 16;
          b |= (((int)divisor.reg[1]) & 0xffff) << 16;
          int quo = a / b;
          if (this.negative) {
            quo = -quo;
          }
          int rem = a - (b * quo);
          return new BigInteger[] {
            new BigInteger().InitializeInt(quo),
            new BigInteger().InitializeInt(rem)
          };
        }
      }
      BigInteger remainder = new BigInteger();
      quotient = new BigInteger();
      words1Size += words1Size & 1;
      words2Size += words2Size & 1;
      remainder.reg = new short[RoundupSize((int)words2Size)];
      remainder.negative = false;
      quotient.reg = new short[RoundupSize((int)(words1Size - words2Size + 2))];
      quotient.negative = false;
      short[] tempbuf = new short[words1Size + (3 * (words2Size + 2))];
      Divide(
        remainder.reg,
        0,
        quotient.reg,
        0,
        tempbuf,
        0,
        this.reg,
        0,
        words1Size,
        divisor.reg,
        0,
        words2Size);
      remainder.wordCount = remainder.CalcWordCount();
      quotient.wordCount = quotient.CalcWordCount();
      // Console.WriteLine("Divd=" + this.wordCount + " divs=" + divisor.wordCount + " quo=" + quotient.wordCount + " rem=" + (remainder.wordCount));
      remainder.ShortenArray();
      quotient.ShortenArray();
      if (this.Sign < 0) {
        quotient.NegateInternal();
        if (!remainder.IsZero) {
          remainder.NegateInternal();
        }
      }
      if (divisor.Sign < 0) {
        quotient.NegateInternal();
      }
      return new BigInteger[] { quotient, remainder };
    }

    /// <summary>Finds the modulus remainder that results when this instance
    /// is divided by the value of a BigInteger object. The modulus remainder
    /// is the same as the normal remainder if the normal remainder is positive,
    /// and equals divisor plus normal remainder if the normal remainder
    /// is negative.</summary>
    /// <param name='divisor'>A divisor greater than 0 (the modulus).</param>
    /// <returns>A BigInteger object.</returns>
    /// <exception cref='ArithmeticException'>The parameter <paramref
    /// name='divisor'/> is negative.</exception>
    /// <exception cref='System.ArgumentNullException'>The parameter
    /// <paramref name='divisor'/> is null.</exception>
    public BigInteger mod(BigInteger divisor) {
      if (divisor == null) {
        throw new ArgumentNullException("divisor");
      }
      if (divisor.Sign < 0) {
        throw new ArithmeticException("Divisor is negative");
      }
      BigInteger rem = this.remainder(divisor);
      if (rem.Sign < 0) {
        rem = divisor.add(rem);
      }
      return rem;
    }

    /// <summary>Finds the remainder that results when this instance is
    /// divided by the value of a BigInteger object. The remainder is the value
    /// that remains when the absolute value of this object is divided by the
    /// absolute value of the other object; the remainder has the same sign
    /// (positive or negative) as this object.</summary>
    /// <param name='divisor'>A BigInteger object.</param>
    /// <returns>The remainder of the two objects.</returns>
    /// <exception cref='System.ArgumentNullException'>The parameter
    /// <paramref name='divisor'/> is null.</exception>
    /// <exception cref='System.DivideByZeroException'>Attempted
    /// to divide by zero.</exception>
    public BigInteger remainder(BigInteger divisor) {
      if (divisor == null) {
        throw new ArgumentNullException("divisor");
      }
      int words1Size = this.wordCount;
      int words2Size = divisor.wordCount;
      if (words2Size == 0) {
        throw new DivideByZeroException();
      }
      if (words1Size < words2Size) {
        // dividend is less than divisor
        return this;
      }
      if (words2Size == 1) {
        short shortRemainder = FastRemainder(this.reg, this.wordCount, divisor.reg[0]);
        int smallRemainder = ((int)shortRemainder) & 0xffff;
        if (this.negative) {
          smallRemainder = -smallRemainder;
        }
        return new BigInteger().InitializeInt(smallRemainder);
      }
      if (this.PositiveCompare(divisor) < 0) {
        return this;
      }
      BigInteger remainder = new BigInteger();
      words1Size += words1Size & 1;
      words2Size += words2Size & 1;
      remainder.reg = new short[RoundupSize((int)words2Size)];
      remainder.negative = false;
      short[] tempbuf = new short[words1Size + (3 * (words2Size + 2))];
      Divide(
        remainder.reg,
        0,
        null,
        0,
        tempbuf,
        0,
        this.reg,
        0,
        words1Size,
        divisor.reg,
        0,
        words2Size);
      remainder.wordCount = remainder.CalcWordCount();
      remainder.ShortenArray();
      if (this.Sign < 0 && !remainder.IsZero) {
        remainder.NegateInternal();
      }
      return remainder;
    }

    private void NegateInternal() {
      if (this.wordCount != 0) {
        this.negative = this.Sign > 0;
      }
    }

    private int PositiveCompare(BigInteger t) {
      int size = this.wordCount, tempSize = t.wordCount;
      if (size == tempSize) {
        return Compare(this.reg, 0, t.reg, 0, (int)size);
      } else {
        return size > tempSize ? 1 : -1;
      }
    }

    /// <summary>Compares a BigInteger object with this instance.</summary>
    /// <returns>Zero if the values are equal; a negative number if this instance
    /// is less, or a positive number if this instance is greater.</returns>
    /// <param name='other'>A BigInteger object.</param>
    public int CompareTo(BigInteger other) {
      if (other == null) {
        return 1;
      }
      if (this == other) {
        return 0;
      }
      int size = this.wordCount, tempSize = other.wordCount;
      int sa = size == 0 ? 0 : (this.negative ? -1 : 1);
      int sb = tempSize == 0 ? 0 : (other.negative ? -1 : 1);
      if (sa != sb) {
        return (sa < sb) ? -1 : 1;
      }
      if (sa == 0) {
        return 0;
      }
      if (size == tempSize) {
        if (size == 1 && this.reg[0] == other.reg[0]) {
          return 0;
        } else {
          short[] words1 = this.reg;
          short[] words2 = other.reg;
          while (unchecked(size--) != 0) {
            int an = ((int)words1[size]) & 0xffff;
            int bn = ((int)words2[size]) & 0xffff;
            if (an > bn) {
              return (sa > 0) ? 1 : -1;
            } else if (an < bn) {
              return (sa > 0) ? -1 : 1;
            }
          }
          return 0;
        }
      } else {
        return ((size > tempSize) ^ (sa <= 0)) ? 1 : -1;
      }
    }

    /// <summary>Gets the sign of this object's value.</summary>
    /// <value>0 if this value is zero; -1 if this value is negative, or 1 if
    /// this value is positive.</value>
    public int Sign {
      get {
        if (this.wordCount == 0) {
          return 0;
        }
        return this.negative ? -1 : 1;
      }
    }

    /// <summary>Gets a value indicating whether this value is 0.</summary>
    /// <value>True if this value is 0; otherwise, false.</value>
    public bool IsZero {
      get {
        return this.wordCount == 0;
      }
    }

    /// <summary>Finds the square root of this instance&apos;s value, rounded
    /// down.</summary>
    /// <returns>The square root of this object's value. Returns 0 if this
    /// value is 0 or less.</returns>
    public BigInteger sqrt() {
      BigInteger[] srrem = this.sqrtWithRemainder();
      return srrem[0];
    }

    /// <summary>Calculates the square root and the remainder.</summary>
    /// <returns>An array of two big integers: the first integer is the square
    /// root, and the second is the difference between this value and the square
    /// of the first integer. Returns two zeros if this value is 0 or less, or
    /// one and zero if this value equals 1.</returns>
    public BigInteger[] sqrtWithRemainder() {
      if (this.Sign <= 0) {
        return new BigInteger[] { BigInteger.Zero, BigInteger.Zero };
      }
      if (this.Equals(BigInteger.One)) {
        return new BigInteger[] { BigInteger.One, BigInteger.Zero };
      }
      BigInteger bigintX;
      BigInteger bigintY;
      BigInteger thisValue = this;
      int powerBits = (thisValue.getUnsignedBitLength() + 1) / 2;
      if (thisValue.canFitInInt()) {
        int smallValue = thisValue.intValue();
        // No need to check for zero; already done above
        int smallintX = 0;
        int smallintY = 1 << powerBits;
        do {
          smallintX = smallintY;
          smallintY = smallValue / smallintX;
          smallintY += smallintX;
          smallintY >>= 1;
        } while (smallintY < smallintX);
        smallintY = smallintX * smallintX;
        smallintY = smallValue - smallintY;
        return new BigInteger[] {
          (BigInteger)smallintX, (BigInteger)smallintY
        };
      } else {
        bigintX = null;
        bigintY = Power2(powerBits);
        do {
          bigintX = bigintY;
          bigintY = thisValue / (BigInteger)bigintX;
          bigintY += bigintX;
          bigintY >>= 1;
        } while (bigintY.CompareTo(bigintX) < 0);
        bigintY = bigintX * (BigInteger)bigintX;
        bigintY = thisValue - (BigInteger)bigintY;
        return new BigInteger[] {
          bigintX, bigintY
        };
      }
      /*
      // Use Johnson's bisection algorithm to find the square root
      int bitSet = this.getUnsignedBitLength();
      --bitSet;
      int lastBit = bitSet >> 1;
      int count = ((lastBit + 15) >> 4) + 1;
      short[] result = new short[RoundupSize(count)];
      short[] dataTmp2 = new short[RoundupSize((count * 2) + 2)];
      short[] dataTmp = new short[RoundupSize((count * 2) + 2)];
      int lastVshiftBit = lastBit << 1;
      BigInteger bid = BigInteger.One << lastVshiftBit;
      result[lastBit >> 4] |= unchecked((short)(1 << (lastBit & 15)));
      lastVshiftBit = 0;
      for (int i = lastBit - 1; i >= 0; --i) {
        int valueVShift;
        Array.Clear((short[])dataTmp, 0, dataTmp.Length);
        // Left shift by i + 1
        valueVShift = checked(i + 1);
        // Note: Copying the result in this way also shifts left, due
        // to the way the number is stored
        Array.Copy(result, 0, dataTmp, valueVShift >> 4, count);
        if ((valueVShift & 15) != 0) {
          ShiftWordsLeftByBits(dataTmp, 0, dataTmp.Length, valueVShift & 15);
        }
        // Add bid (do this first since it's often what
        // affects the comparison the most)
        if (dataTmp.Length >= bid.wordCount) {
          AddUnevenSize(dataTmp, 0, dataTmp, 0, dataTmp.Length, bid.reg, 0, bid.wordCount);
        } else {
          AddUnevenSize(dataTmp, 0, bid.reg, 0, bid.wordCount, dataTmp, 0, dataTmp.Length);
        }
        if (CompareUnevenSize(dataTmp, 0, dataTmp.Length, this.reg, 0, this.wordCount) > 0) {
          continue;
        }
        // Add 1<<(i << 1)
        valueVShift = checked(i << 1);
        if ((((int)dataTmp[valueVShift >> 4]) & (1 << (valueVShift & 15))) == 0) {
          // Add bit directly, the augend has just one bit
          dataTmp[valueVShift >> 4] |= unchecked((short)(1 << (valueVShift & 15)));
        } else {
          dataTmp2[lastVshiftBit] = (short)0;
          dataTmp2[valueVShift >> 4] |= unchecked((short)(1 << (valueVShift & 15)));
          lastVshiftBit = valueVShift >> 4;
          AddOneByOne(dataTmp, 0, dataTmp, 0, dataTmp2, 0, dataTmp.Length);
        }
        // Console.WriteLine("3. " + (WordsToBigInt(dataTmp, 0, dataTmp.Length)) + " cmp " + (this));
        if (CompareUnevenSize(dataTmp, 0, dataTmp.Length, this.reg, 0, this.wordCount) > 0) {
          continue;
        }
        bid = WordsToBigInt(dataTmp, 0, dataTmp.Length);
        result[i >> 4] |= unchecked((short)(1 << (i & 15)));
      }
      bigintX = new BigInteger();
      bigintX.reg = result;
      bigintX.wordCount = bigintX.CalcWordCount();
      bigintY = bigintX * (BigInteger)bigintX;
      bigintY = this - (BigInteger)bigintY;
      return new BigInteger[] {
        bigintX, bigintY
      };
       */
    }

    /// <summary>Gets a value indicating whether this value is even.</summary>
    /// <value>True if this value is even; otherwise, false.</value>
    public bool IsEven {
      get {
        return !this.GetUnsignedBit(0);
      }
    }

    /// <summary>BigInteger object for the number zero.</summary>
    #if CODE_ANALYSIS
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
      "Microsoft.Security",
      "CA2104",
      Justification = "BigInteger is immutable")]
    #endif
    public static readonly BigInteger ZERO = new BigInteger().InitializeInt(0);

    /// <summary>BigInteger object for the number one.</summary>
    #if CODE_ANALYSIS
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
      "Microsoft.Security",
      "CA2104",
      Justification = "BigInteger is immutable")]
    #endif

    public static readonly BigInteger ONE = new BigInteger().InitializeInt(1);

    /// <summary>BigInteger object for the number ten.</summary>
    #if CODE_ANALYSIS
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
      "Microsoft.Security",
      "CA2104",
      Justification = "BigInteger is immutable")]
    #endif

    public static readonly BigInteger TEN = new BigInteger().InitializeInt(10);
  }
}
