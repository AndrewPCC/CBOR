/*
Written by Peter O. in 2014.
Any copyright is dedicated to the Public Domain.
http://creativecommons.org/publicdomain/zero/1.0/
If you like this, you should donate to Peter O.
at: http://upokecenter.com/d/
 */
using System;
using System.Text;
using PeterO;
using PeterO.Cbor;

namespace Test {
    /// <summary>Description of RandomObjects.</summary>
  public static class RandomObjects
  {
    public static CBORObject RandomNumber(FastRandom rand) {
      switch (rand.NextValue(6)) {
        case 0:
          return CBORObject.FromObject(RandomDouble(rand, Int32.MaxValue));
        case 1:
          return CBORObject.FromObject(RandomSingle(rand, Int32.MaxValue));
        case 2:
          return CBORObject.FromObject(RandomObjects.RandomBigInteger(rand));
        case 3:
          return CBORObject.FromObject(RandomExtendedFloat(rand));
        case 4:
          return CBORObject.FromObject(RandomExtendedDecimal(rand));
        case 5:
          return CBORObject.FromObject(RandomInt64(rand));
        default: throw new ArgumentException();
      }
    }

    public static CBORObject RandomNumberOrRational(FastRandom rand) {
      switch (rand.NextValue(7)) {
        case 0:
          return CBORObject.FromObject(RandomDouble(rand, Int32.MaxValue));
        case 1:
          return CBORObject.FromObject(RandomSingle(rand, Int32.MaxValue));
        case 2:
          return CBORObject.FromObject(RandomObjects.RandomBigInteger(rand));
        case 3:
          return CBORObject.FromObject(RandomExtendedFloat(rand));
        case 4:
          return CBORObject.FromObject(RandomExtendedDecimal(rand));
        case 5:
          return CBORObject.FromObject(RandomInt64(rand));
        case 6:
          return CBORObject.FromObject(RandomRational(rand));
        default: throw new ArgumentException();
      }
    }

    public static CBORObject RandomCBORByteString(FastRandom rand) {
      int x = rand.NextValue(0x2000);
      var bytes = new byte[x];
      for (var i = 0; i < x; ++i) {
        bytes[i] = unchecked((byte)rand.NextValue(256));
      }
      return CBORObject.FromObject(bytes);
    }

    public static CBORObject RandomCBORByteStringShort(FastRandom rand) {
      int x = rand.NextValue(50);
      var bytes = new byte[x];
      for (var i = 0; i < x; ++i) {
        bytes[i] = unchecked((byte)rand.NextValue(256));
      }
      return CBORObject.FromObject(bytes);
    }

    public static CBORObject RandomCBORTextString(FastRandom rand) {
      int length = rand.NextValue(0x2000);
      var sb = new StringBuilder();
      for (var i = 0; i < length; ++i) {
        int x = rand.NextValue(100);
        if (x < 95) {
          // ASCII
          sb.Append((char)(0x20 + rand.NextValue(0x60)));
        } else if (x < 98) {
          // Supplementary character
          x = rand.NextValue(0x400) + 0xd800;
          sb.Append((char)x);
          x = rand.NextValue(0x400) + 0xdc00;
          sb.Append((char)x);
        } else {
          // BMP character
          x = 0x20 + rand.NextValue(0xffe0);
          if (x >= 0xd800 && x < 0xe000) {
            // surrogate code unit, generate ASCII instead
            x = 0x20 + rand.NextValue(0x60);
          }
          sb.Append((char)x);
        }
      }
      return CBORObject.FromObject(sb.ToString());
    }

    public static CBORObject RandomCBORMap(FastRandom rand, int depth) {
      int x = rand.NextValue(100);
      int count = 0;
      count = (x < 80) ? 2 : ((x < 93) ? 1 : ((x < 98) ? 0 : 10));
      CBORObject cborRet = CBORObject.NewMap();
      for (var i = 0; i < count; ++i) {
        CBORObject key = RandomCBORObject(rand, depth + 1);
        CBORObject value = RandomCBORObject(rand, depth + 1);
        cborRet[key] = value;
      }
      return cborRet;
    }

    public static CBORObject RandomCBORTaggedObject(
      FastRandom rand,
      int depth) {
      int tag = 0;
      if (rand.NextValue(2) == 0) {
        int[] tagselection = { 2, 2, 2, 3, 3, 3, 4, 4, 4, 5, 5, 5, 30, 30,
          30, 0, 1, 25, 26, 27 };
        tag = tagselection[rand.NextValue(tagselection.Length)];
      } else {
        tag = rand.NextValue(0x1000000);
      }
      if (tag == 25) {
        tag = 0;
      }
      if (tag == 30) {
        return RandomCBORByteString(rand);
      }
      for (var i = 0; i < 15; ++i) {
        CBORObject o;
        // Console.WriteLine("tag "+tag+" "+i);
        if (tag == 0 || tag == 1 || tag == 28 || tag == 29) {
          tag = 999;
        }
        if (tag == 2 || tag == 3) {
          o = RandomCBORByteStringShort(rand);
        } else if (tag == 4 || tag == 5) {
          o = CBORObject.NewArray();
          o.Add(RandomSmallIntegral(rand));
          o.Add(CBORObject.FromObject(RandomObjects.RandomBigInteger(rand)));
        } else if (tag == 30) {
          o = CBORObject.NewArray();
          o.Add(RandomSmallIntegral(rand));
          o.Add(CBORObject.FromObject(RandomObjects.RandomBigInteger(rand)));
        } else {
          o = RandomCBORObject(rand, depth + 1);
        }
        try {
          o = CBORObject.FromObjectAndTag(o, tag);
          // Console.WriteLine("done");
          return o;
        } catch (Exception) {
          continue;
        }
      }
      // Console.WriteLine("Failed "+tag);
      return CBORObject.Null;
    }

    public static CBORObject RandomCBORArray(FastRandom rand, int depth) {
      int x = rand.NextValue(100);
      int count = 0;
      count = (x < 80) ? 2 : ((x < 93) ? 1 : ((x < 98) ? 0 : 10));
      CBORObject cborRet = CBORObject.NewArray();
      for (var i = 0; i < count; ++i) {
        cborRet.Add(RandomCBORObject(rand, depth + 1));
      }
      return cborRet;
    }

    public static ExtendedRational RandomRational(FastRandom rand) {
      BigInteger bigintA = RandomBigInteger(rand);
      BigInteger bigintB = RandomBigInteger(rand);
      if (bigintB.IsZero) {
        bigintB = BigInteger.One;
      }
      return new ExtendedRational(bigintA, bigintB);
    }

    public static CBORObject RandomCBORObject(FastRandom rand) {
      return RandomCBORObject(rand, 0);
    }

    public static CBORObject RandomCBORObject(FastRandom rand, int depth) {
      int nextval = rand.NextValue(11);
      switch (nextval) {
        case 0:
        case 1:
        case 2:
        case 3:
          return RandomNumberOrRational(rand);
        case 4:
          return rand.NextValue(2) == 0 ? CBORObject.True : CBORObject.False;
        case 5:
          return rand.NextValue(2) == 0 ? CBORObject.Null :
            CBORObject.Undefined;
        case 6:
          return RandomCBORTextString(rand);
        case 7:
          return RandomCBORByteString(rand);
        case 8:
          return RandomCBORArray(rand, depth);
        case 9:
          return RandomCBORMap(rand, depth);
        case 10:
          return RandomCBORTaggedObject(rand, depth);
        default: return RandomNumber(rand);
      }
    }

    public static long RandomInt64(FastRandom rand) {
      long r = rand.NextValue(0x10000);
      r |= ((long)rand.NextValue(0x10000)) << 16;
      if (rand.NextValue(2) == 0) {
        r |= ((long)rand.NextValue(0x10000)) << 32;
        if (rand.NextValue(2) == 0) {
          r |= ((long)rand.NextValue(0x10000)) << 48;
        }
      }
      return r;
    }

    public static double RandomDouble(FastRandom rand, int exponent) {
      if (exponent == Int32.MaxValue) {
        exponent = rand.NextValue(2047);
      }
      long r = rand.NextValue(0x10000);
      r |= ((long)rand.NextValue(0x10000)) << 16;
      if (rand.NextValue(2) == 0) {
        r |= ((long)rand.NextValue(0x10000)) << 32;
        if (rand.NextValue(2) == 0) {
          r |= ((long)rand.NextValue(0x10000)) << 48;
        }
      }
      r &= ~0x7FF0000000000000L;  // clear exponent
      r |= ((long)exponent) << 52;  // set exponent
      return BitConverter.ToDouble(BitConverter.GetBytes((long)r), 0);
    }

    public static float RandomSingle(FastRandom rand, int exponent) {
      if (exponent == Int32.MaxValue) {
        exponent = rand.NextValue(255);
      }
      int r = rand.NextValue(0x10000);
      if (rand.NextValue(2) == 0) {
        r |= ((int)rand.NextValue(0x10000)) << 16;
      }
      r &= ~0x7f800000;  // clear exponent
      r |= ((int)exponent) << 23;  // set exponent
      return BitConverter.ToSingle(BitConverter.GetBytes((int)r), 0);
    }

    public static ExtendedDecimal RandomExtendedDecimal(FastRandom r) {
      if (r.NextValue(100) == 0) {
        int x = r.NextValue(3);
        if (x == 0) {
          return ExtendedDecimal.PositiveInfinity;
        }
        if (x == 1) {
          return ExtendedDecimal.NegativeInfinity;
        }
        if (x == 2) {
          return ExtendedDecimal.NaN;
        }
        // Signaling NaN currently not generated because
        // it doesn't round-trip as well
      }
      return ExtendedDecimal.FromString(RandomDecimalString(r));
    }

    public static BigInteger RandomBigInteger(FastRandom r) {
      int count = r.NextValue(60) + 1;
      var bytes = new byte[count];
      for (var i = 0; i < count; ++i) {
        bytes[i] = (byte)((int)r.NextValue(256));
      }
      return BigInteger.fromByteArray(bytes, true);
    }

    public static ExtendedFloat RandomExtendedFloat(FastRandom r) {
      if (r.NextValue(100) == 0) {
        int x = r.NextValue(3);
        if (x == 0) {
          return ExtendedFloat.PositiveInfinity;
        }
        if (x == 1) {
          return ExtendedFloat.NegativeInfinity;
        }
        if (x == 2) {
          return ExtendedFloat.NaN;
        }
      }
      return ExtendedFloat.Create(
RandomBigInteger(r),
(BigInteger)(r.NextValue(400) - 200));
    }

    public static String RandomBigIntString(FastRandom r) {
      int count = r.NextValue(50) + 1;
      var sb = new StringBuilder();
      if (r.NextValue(2) == 0) {
        sb.Append('-');
      }
      for (var i = 0; i < count; ++i) {
        if (i == 0) {
          sb.Append((char)('1' + r.NextValue(9)));
        } else {
          sb.Append((char)('0' + r.NextValue(10)));
        }
      }
      return sb.ToString();
    }

    public static CBORObject RandomSmallIntegral(FastRandom r) {
      int count = r.NextValue(20) + 1;
      var sb = new StringBuilder();
      if (r.NextValue(2) == 0) {
        sb.Append('-');
      }
      for (var i = 0; i < count; ++i) {
        if (i == 0) {
          sb.Append((char)('1' + r.NextValue(9)));
        } else {
          sb.Append((char)('0' + r.NextValue(10)));
        }
      }
      return CBORObject.FromObject(BigInteger.fromString(sb.ToString()));
    }

    public static String RandomDecimalString(FastRandom r) {
      int count = r.NextValue(20) + 1;
      var sb = new StringBuilder();
      if (r.NextValue(2) == 0) {
        sb.Append('-');
      }
      for (var i = 0; i < count; ++i) {
        if (i == 0) {
          sb.Append((char)('1' + r.NextValue(9)));
        } else {
          sb.Append((char)('0' + r.NextValue(10)));
        }
      }
      if (r.NextValue(2) == 0) {
        sb.Append('.');
        count = r.NextValue(20) + 1;
        for (var i = 0; i < count; ++i) {
          sb.Append((char)('0' + r.NextValue(10)));
        }
      }
      if (r.NextValue(2) == 0) {
        sb.Append('E');
        count = r.NextValue(20);
        if (count != 0) {
          sb.Append(r.NextValue(2) == 0 ? '+' : '-');
        }
        sb.Append(Convert.ToString(
          (int)count,
          System.Globalization.CultureInfo.InvariantCulture));
      }
      return sb.ToString();
    }
  }
}