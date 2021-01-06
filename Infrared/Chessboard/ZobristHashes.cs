using System;
using System.Runtime.CompilerServices;

namespace InfraredEngine.Chessboard {
   public static class ZobristHashes {
      /// <summary>
      /// Size:Indexed by - [14:Pieces, 64:Tiles], Pieces[0 and 7] are for blank tiles and are mirrored across those indecies to be colour independant.
      /// </summary>
      public static readonly ulong[,] Hashes;
      static ZobristHashes() {
         Hashes = new ulong[1 + 6 + 1 + 6, 64];
         Random prng = new Random();
         byte[] buffer = new byte[8]; // 64 bits
         prng.NextBytes(buffer);

         for (int piece = 0; piece < 14; piece++) {
            // this ensures that the duplicated blanks don't unnecessarily hog certain hashes
            if (piece == 7)
               continue;

            // generate hashes normally
            for (int loc = 0; loc < 64; loc++) {
            REGEN:
               prng.NextBytes(buffer);
               ulong temp = BitConverter.ToUInt64(buffer);
               for (int p = 0; p < piece; p++) {
                  for (int l = 0; l < loc; l++) {
                     if (temp == Hashes[p, l])
                        goto REGEN;
                  }
               }
               Hashes[piece, loc] = temp;
            }
         }

         // duplicate blank hashes
         for (int loc = 0; loc < 64; loc++) {
            Hashes[7, loc] = Hashes[0, loc];
         }
      }

      /// <summary>
      /// XOR by 7 to reverse value.
      /// </summary>
      /// <param name="colour">Whether it is white's piece.</param>
      /// <returns>The offset to add to the piece ID to get the Hashes (dimension 0) offset.</returns>
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static int GetColourOffset(bool colour) => colour ? 0 : 7;
   }
}
