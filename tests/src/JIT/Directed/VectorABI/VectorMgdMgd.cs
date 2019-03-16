// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.Intrinsics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

// Test passing and returning HVAs (homogeneous vector aggregates) to/from managed code.
// Test various sizes (including ones that exceed the limit for being treated as HVAs),
// as well as passing various numbers of them (so that we exceed the available registers),
// and mixing the HVA parameters with non-HVA parameters.

// This is a skeleton version. Remaining to do:
// - Add more HVA types: using Vector64 and with varying numbers of vectors.
// - Add types that are *not* HVA types (e.g. too many vectors, or using Vector256).
// - Add diagnostic info so that it is easier to see what has failed.

public static class VectorMgdMgd
{
    private const int PASS = 100;
    private const int FAIL = 0;

    static Random random = new Random(12345);

    public static T GetValueFromInt<T>(int value)
    {
        if (typeof(T) == typeof(float))
        {
            float floatValue = (float)value;
            return (T)(object)floatValue;
        }
        if (typeof(T) == typeof(double))
        {
            double doubleValue = (double)value;
            return (T)(object)doubleValue;
        }
        if (typeof(T) == typeof(int))
        {
            return (T)(object)value;
        }
        if (typeof(T) == typeof(uint))
        {
            uint uintValue = (uint)value;
            return (T)(object)uintValue;
        }
        if (typeof(T) == typeof(long))
        {
            long longValue = (long)value;
            return (T)(object)longValue;
        }
        if (typeof(T) == typeof(ulong))
        {
            ulong longValue = (ulong)value;
            return (T)(object)longValue;
        }
        if (typeof(T) == typeof(ushort))
        {
            return (T)(object)(ushort)value;
        }
        if (typeof(T) == typeof(byte))
        {
            return (T)(object)(byte)value;
        }
        if (typeof(T) == typeof(short))
        {
            return (T)(object)(short)value;
        }
        if (typeof(T) == typeof(sbyte))
        {
            return (T)(object)(sbyte)value;
        }
        else
        {
            throw new ArgumentException();
        }
    }
    public static bool CheckValue<T>(T value, T expectedValue)
    {
        bool returnVal;
        if (typeof(T) == typeof(float))
        {
            returnVal = Math.Abs(((float)(object)value) - ((float)(object)expectedValue)) <= Single.Epsilon;
        }
        if (typeof(T) == typeof(double))
        {
            returnVal = Math.Abs(((double)(object)value) - ((double)(object)expectedValue)) <= Double.Epsilon;
        }
        else
        {
            returnVal = value.Equals(expectedValue);
        }
        if (returnVal == false)
        {
            if ((typeof(T) == typeof(double)) || (typeof(T) == typeof(float)))
            {
                Console.WriteLine("CheckValue failed for type " + typeof(T).ToString() + ". Expected: {0} , Got: {1}", expectedValue, value);
            }
            else
            {
                Console.WriteLine("CheckValue failed for type " + typeof(T).ToString() + ". Expected: {0} (0x{0:X}), Got: {1} (0x{1:X})", expectedValue, value);
            }
        }
        return returnVal;
    }

    public unsafe class HVATests<T> where T : struct
    {
        // We can have up to 4 vectors in an HVA, and we'll test structs with up to 5 of them
        // (to ensure that even those that are too large are handled consistently).
        // So we need 5 * the element count in the largest (128-bit) vector with the smallest
        // element type (byte).
        static T[] values;
        static T[] check;
        private int ElementCount = (Unsafe.SizeOf<Vector128<T>>() / sizeof(byte)) * 5;
        public bool isPassing = true;

        Type[] reflectionParameterTypes;
        System.Reflection.MethodInfo reflectionMethodInfo;
        object[] reflectionInvokeArgs;

        public struct HVA64_02
        {
            public Vector64<T> v0;
            public Vector64<T> v1;
        }

        public struct HVA128_02
        {
            public Vector128<T> v0;
            public Vector128<T> v1;
        }

        private HVA64_02  hva64_02;
        private HVA128_02 hva128_02;

        public void Init_HVAs()
        {
            int i;

            i = 0;
            hva64_02.v0 = Unsafe.As<T, Vector64<T>>(ref values[i]);
	    i += Vector64<T>.Count;
            hva64_02.v1 = Unsafe.As<T, Vector64<T>>(ref values[i]);
	    i += Vector64<T>.Count;

            i = 0;
            hva128_02.v0 = Unsafe.As<T, Vector128<T>>(ref values[i]);
	    i += Vector128<T>.Count;
            hva128_02.v1 = Unsafe.As<T, Vector128<T>>(ref values[i]);
	    i += Vector128<T>.Count;
        }

        public HVATests()
        {
            values = new T[ElementCount];
            for (int i = 0; i < values.Length; i++)
            {
                int data = random.Next(100);
                values[i] = GetValueFromInt<T>(data);
            }

            Init_HVAs();
        }

        //////  Vector64<T> tests

        // Checks that the values in v correspond to those in the values array starting
        // with values[index]
        private void checkValues(Vector64<T> v, int index)
        {
            bool printedMsg = false;  // Print at most one message

            for (int i = 0; i < Vector64<T>.Count; i++)
            {
                if (!CheckValue<T>(v.GetElement(i), values[index]))
                {
                    if (!printedMsg)
                    {
                        Console.WriteLine("FAILED: checkValues(index = {1}, i = {2})", index, i);
                        printedMsg = true;
                    }

                    isPassing = false;
                }
                index++;
            }
        }

        // Test the case where we've passed in a single argument HVA of 2 vectors.
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void test1Argument_HVA64_02(HVA64_02 arg1)
        {
            checkValues(arg1.v0, 0);
            checkValues(arg1.v1, Vector64<T>.Count);
        }

        // Return an HVA of 2 vectors, with values from the 'values' array.
        [MethodImpl(MethodImplOptions.NoInlining)]
        public HVA64_02 returnTest_HVA64_02()
        {
            return hva64_02;
        }

        public void testReturn_HFA64_02()
        {
            HVA64_02 result = returnTest_HVA64_02();
            checkValues(result.v0, 0);
            checkValues(result.v1, Vector64<T>.Count);
        }

        public void Init_Reflection_Args_HVA64_02()
        {
            reflectionParameterTypes = new Type[] { typeof(HVA64_02) };
            reflectionMethodInfo = typeof(HVATests<T>).GetMethod(nameof(HVATests<T>.test1Argument_HVA64_02), reflectionParameterTypes);
            reflectionInvokeArgs = new object[] { hva64_02 };
        }

        public void Init_Reflection_Return_HVA64_02()
        {
            reflectionParameterTypes = new Type[] { };
            reflectionMethodInfo = typeof(HVATests<T>).GetMethod(nameof(HVATests<T>.returnTest_HVA64_02), reflectionParameterTypes);
            reflectionInvokeArgs = new object[] { };
        }

        public void testReflectionReturn_HFA64_02()
        {
            Init_Reflection_Return_HVA64_02();

            object objResult = reflectionMethodInfo.Invoke(this, reflectionInvokeArgs);

            HVA64_02 result = (HVA64_02)objResult;

            checkValues(result.v0, 0);
            checkValues(result.v1, Vector64<T>.Count);
        }

        //////  Vector128<T> tests

        // Checks that the values in v correspond to those in the values array starting
        // with values[index]
        private void checkValues(Vector128<T> v, int index)
        {
            bool printedMsg = false;  // Print at most one message

            for (int i = 0; i < Vector128<T>.Count; i++)
            {
                if (!CheckValue<T>(v.GetElement(i), values[index]))
                {
                    if (!printedMsg)
                    {
                        Console.WriteLine("FAILED: checkValues(index = {1}, i = {2})", index, i);
                        printedMsg = true;
                    }

                    isPassing = false;
                }
                index++;
            }
        }

        // Test the case where we've passed in a single argument HVA of 2 vectors.
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void test1Argument_HVA128_02(HVA128_02 arg1)
        {
            checkValues(arg1.v0, 0);
            checkValues(arg1.v1, Vector128<T>.Count);
        }

        // Return an HVA of 2 vectors, with values from the 'values' array.
        [MethodImpl(MethodImplOptions.NoInlining)]
        public HVA128_02 returnTest_HVA128_02()
        {
            return hva128_02;
        }

        public void testReturn_HFA128_02()
        {
            HVA128_02 result = returnTest_HVA128_02();
            checkValues(result.v0, 0);
            checkValues(result.v1, Vector128<T>.Count);
        }

        public void Init_Reflection_Args_HVA128_02()
        {
            reflectionParameterTypes = new Type[] { typeof(HVA128_02) };
            reflectionMethodInfo = typeof(HVATests<T>).GetMethod(nameof(HVATests<T>.test1Argument_HVA128_02), reflectionParameterTypes);
            reflectionInvokeArgs = new object[] { hva128_02 };
        }

        public void Init_Reflection_Return_HVA128_02()
        {
            reflectionParameterTypes = new Type[] { };
            reflectionMethodInfo = typeof(HVATests<T>).GetMethod(nameof(HVATests<T>.returnTest_HVA128_02), reflectionParameterTypes);
            reflectionInvokeArgs = new object[] { };
        }

        public void testReflectionReturn_HFA128_02()
        {
            Init_Reflection_Return_HVA128_02();

            object objResult = reflectionMethodInfo.Invoke(this, reflectionInvokeArgs);

            HVA128_02 result = (HVA128_02)objResult;

            checkValues(result.v0, 0);
            checkValues(result.v1, Vector128<T>.Count);
        }

        public void doTests()
        {
            //////  Vector64<T> tests

            // Test HVA Argumnets

            test1Argument_HVA64_02(hva64_02);

            Init_Reflection_Args_HVA64_02();
            reflectionMethodInfo.Invoke(this, reflectionInvokeArgs);


            // Test HVA Return values

            testReturn_HFA64_02();

            testReflectionReturn_HFA64_02();


            //////  Vector128<T> tests

            // Test HVA Argumnets

            test1Argument_HVA128_02(hva128_02);

            Init_Reflection_Args_HVA128_02();
            reflectionMethodInfo.Invoke(this, reflectionInvokeArgs);


            // Test HVA Return values

            testReturn_HFA128_02();

            testReflectionReturn_HFA128_02();
        }
    }

    public static int Main(string[] args)
    {
        var isPassing = true;

        HVATests<byte> byteTests = new HVATests<byte>();
        byteTests.doTests();

        return byteTests.isPassing ? PASS : FAIL;
    }
}
