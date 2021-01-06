using System;

namespace InfraredEngine.Chessboard {
   public enum BoardVariant {
      Default,
   }
   public enum ChessPiece : byte {
      None = 0,
      Pawn = 1,
      Knight = 2,
      Bishop = 3,
      Rook = 4,
      Queen = 5,
      King = 6,
   }

   [Flags]
   public enum BitBoardFlags : ulong {
      None = 0ul,
      WhitesTurn = 1ul << 1,

      A1MovedOrTaken = 1ul << 0,
      E1MovedOrTaken = 1ul << 4,
      H1MovedOrTaken = 1ul << 7,
      A8MovedOrTaken = 1ul << 56,
      E8MovedOrTaken = 1ul << 60,
      H8MovedOrTaken = 1ul << 63,
   }
}
