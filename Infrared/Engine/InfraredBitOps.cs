using System.Runtime.CompilerServices;
using static System.Numerics.BitOperations;

namespace InfraredEngine.Engine {
	public static class InfraredBitOps {
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static uint PopCountU32(ulong val) {
         if (System.Runtime.Intrinsics.X86.Popcnt.X64.IsSupported) {
            // ulong to uint conversion is much faster than int to uint (mov jit asm instruction performs very differently)
            return (uint)System.Runtime.Intrinsics.X86.Popcnt.X64.PopCount(val);
         } else {
            return (uint)PopCount(val);
         }
      }
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static uint TrailingZeroCountU32(ulong val) {
         if (System.Runtime.Intrinsics.X86.Bmi1.X64.IsSupported) {
            // ulong to uint conversion is much faster than int to uint (mov jit asm instruction performs very differently)
            return (uint)System.Runtime.Intrinsics.X86.Bmi1.X64.TrailingZeroCount(val);
         } else {
            return (uint)TrailingZeroCount(val);
         }
      }
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static ulong ResetLsb(ulong val) {
         if (System.Runtime.Intrinsics.X86.Bmi1.X64.IsSupported) {
            return System.Runtime.Intrinsics.X86.Bmi1.X64.ResetLowestSetBit(val);
         } else {
            return val ^ (1ul << TrailingZeroCount(val));
         }
      }
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static ulong ExtractLsb(ulong val) {
         if (System.Runtime.Intrinsics.X86.Bmi1.X64.IsSupported) {
            return System.Runtime.Intrinsics.X86.Bmi1.X64.ExtractLowestSetBit(val);
         } else {
            return 1ul << TrailingZeroCount(val);
         }
      }
   }
}
