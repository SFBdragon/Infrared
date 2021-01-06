using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using static InfraredEngine.Chessboard.BitboardMoveTables;
using static InfraredEngine.Engine.InfraredBitOps;

namespace InfraredEngine.Chessboard {
   public static class BitBoardUtils {

      #region Piece Movement Functions

      [MethodImpl(MethodImplOptions.AggressiveOptimization)]
      public static ulong Straight(ulong allPieces, ulong allPiecesFlippedA1H8, int index) {
         int x = index & 7;
         int y = index >> 3;
         int y8 = y << 3;

         ulong rankMask = ((ulong)StraightRankMoveTable[x, (byte)(allPieces >> y8)]) << y8;
         ulong fileMask = StraightFileMoveTable[y, (byte)(allPiecesFlippedA1H8 >> (x << 3))] << x;
         return rankMask | fileMask;
      }
      [MethodImpl(MethodImplOptions.AggressiveOptimization)]
      public static ulong Diagonal(ulong allPiecesRot45C, ulong allPiecesRot45AC, int index) {
         ulong a1h8Mask, a8h1Mask;
         int diag, shl, mask;

         int x = index & 7;
         int y = index >> 3;

         diag = y - x;
         shl = diag * 8;
         if (diag < 0) {
            shl = shl - diag + 64;
            mask = (1 << (8 + diag)) - 1;
            a1h8Mask = DiagonalA1H8MoveTableFlippedA1H8[-diag][((int)(allPiecesRot45C >> shl)) & mask, y];
         } else {
            mask = (1 << (8 - diag)) - 1;
            a1h8Mask = DiagonalA1H8MoveTable[diag][((int)(allPiecesRot45C >> shl)) & mask, x];
         }

         diag = -(7 - y - x);
         shl = diag * 8;
         if (diag < 0) {
            shl += 64;
            mask = (1 << (8 + diag)) - 1;
            a8h1Mask = DiagonalA8H1MoveTableFlippedA8H1[-diag][((int)(allPiecesRot45AC >> shl)) & mask, x];
         } else {
            shl += diag;
            mask = (1 << (8 - diag)) - 1;
            a8h1Mask = DiagonalA8H1MoveTable[diag][((int)(allPiecesRot45AC >> shl)) & mask, x - diag];
         }

         return a1h8Mask | a8h1Mask;
      }
      [MethodImpl(MethodImplOptions.AggressiveOptimization)]
      public static ulong WhitePawn(in BitBoard board, ulong loc) {
         ulong result = 0;
         result |= ((loc & 0xFEFEFEFEFEFEFEFEul) << 7) & (board.BlackPieces | board.EnPassant);
         result |= ((loc & 0x7F7F7F7F7F7F7F7Ful) << 9) & (board.BlackPieces | board.EnPassant);
         result |= ((loc & 0x000000000000FF00ul) << 16) & ~board.AllPieces & ~(board.AllPieces << 8);
         result |= (loc << 8) & ~board.AllPieces;
         return result;
      }
      [MethodImpl(MethodImplOptions.AggressiveOptimization)]
      public static ulong BlackPawn(in BitBoard board, ulong loc) {
         ulong result = 0;
         result |= ((loc & 0xFEFEFEFEFEFEFEFEul) >> 9) & (board.WhitePieces | board.EnPassant);
         result |= ((loc & 0x7F7F7F7F7F7F7F7Ful) >> 7) & (board.WhitePieces | board.EnPassant);
         result |= ((loc & 0x00FF000000000000ul) >> 16) & ~board.AllPieces & ~(board.AllPieces >> 8);
         result |= (loc >> 8) & ~board.AllPieces;
         return result;
      }

      #endregion

      #region Moveset Retrieval Functions

      [MethodImpl(MethodImplOptions.AggressiveOptimization)]
      public static ulong GetMovesetMask(in BitBoard board, int index, out ChessPiece piece) {
         ulong loc = 1ul << index;
         if (board.Flags.HasFlag(BitBoardFlags.WhitesTurn)) {
            if ((loc & board.Pawns) != 0) {
               piece = ChessPiece.Pawn;
               return WhitePawn(board, loc);
            } else if ((loc & board.Knights) != 0) {
               piece = ChessPiece.Knight;
               return KnightMoves[index] & ~board.WhitePieces;
            } else if ((loc & board.Bishops) != 0) {
               piece = ChessPiece.Bishop;
               return Diagonal(board.AllPiecesRot45C, board.AllPiecesRot45A, index) & ~board.WhitePieces;
            } else if ((loc & board.Rooks) != 0) {
               piece = ChessPiece.Rook;
               return Straight(board.AllPieces, board.AllPiecesFlippedA1H8, index) & ~board.WhitePieces;
            } else if ((loc & board.Kings) != 0) {
               var flags = board.Flags;
               piece = ChessPiece.King;
               // don't mask out covered tiles so that the AI doesn't have to handle that case
               return (KingMoves[index] & ~board.BlackCoveredTiles & ~board.WhitePieces)
                  | ((flags & (BitBoardFlags.E1MovedOrTaken | BitBoardFlags.A1MovedOrTaken)) == BitBoardFlags.None && (board.AllPieces & 0xEul) == 0 && (board.BlackCoveredTiles & 0x1Cul) == 0
                  ? 0x4ul : 0x0ul)
                  | ((flags & (BitBoardFlags.E1MovedOrTaken | BitBoardFlags.H1MovedOrTaken)) == BitBoardFlags.None && (board.AllPieces & 0x60ul) == 0 && (board.BlackCoveredTiles & 0x70ul) == 0
                  ? 0x40ul : 0x0ul);
            } else { // queen
               piece = ChessPiece.Queen;
               return (Diagonal(board.AllPiecesRot45C, board.AllPiecesRot45A, index) | Straight(board.AllPieces, board.AllPiecesFlippedA1H8, index)) & ~board.WhitePieces;
            }
         } else {
            if ((loc & board.Pawns) != 0) {
               piece = ChessPiece.Pawn;
               return BlackPawn(board, loc);
            } else if ((loc & board.Knights) != 0) {
               piece = ChessPiece.Knight;
               return KnightMoves[index] & ~board.BlackPieces;
            } else if ((loc & board.Bishops) != 0) {
               piece = ChessPiece.Bishop;
               return Diagonal(board.AllPiecesRot45C, board.AllPiecesRot45A, index) & ~board.BlackPieces;
            } else if ((loc & board.Rooks) != 0) {
               piece = ChessPiece.Rook;
               return Straight(board.AllPieces, board.AllPiecesFlippedA1H8, index) & ~board.BlackPieces;
            } else if ((loc & board.Kings) != 0) {
               var flags = board.Flags;
               piece = ChessPiece.King;
               // don't mask out covered tiles so that the AI doesn't have to handle that case
               return (KingMoves[index] & ~board.BlackPieces)
                  | ((flags & (BitBoardFlags.E8MovedOrTaken | BitBoardFlags.A8MovedOrTaken)) == BitBoardFlags.None && (board.AllPieces & 0x0E0000000000000ul) == 0
                  && (board.WhiteCoveredTiles & 0x1C00000000000000ul) == 0 ? 0x040000000000000ul : 0x0ul)
                  | ((flags & (BitBoardFlags.E8MovedOrTaken | BitBoardFlags.H8MovedOrTaken)) == BitBoardFlags.None && (board.AllPieces & 0x600000000000000ul) == 0
                  && (board.WhiteCoveredTiles & 0x7000000000000000ul) == 0 ? 0x400000000000000ul : 0x0ul);
            } else { // queen
               piece = ChessPiece.Queen;
               return (Diagonal(board.AllPiecesRot45C, board.AllPiecesRot45A, index) | Straight(board.AllPieces, board.AllPiecesFlippedA1H8, index)) & ~board.BlackPieces;
            }
         }
      }
      [MethodImpl(MethodImplOptions.AggressiveOptimization)]
      public static int[] GetMovesetIndecies(in BitBoard board, int index, out ChessPiece piece) {
         ulong bitmap = GetMovesetMask(board, index, out piece);

         int count = BitOperations.PopCount(bitmap);
         int[] result = new int[count];

         for (int i = 0; i < count; i++) {
            int lsb = BitOperations.TrailingZeroCount(bitmap);
            bitmap = ResetLsb(bitmap);
            result[i] = lsb;
         }

         return result;
      }
      /// <returns>int[] moveIndeciesArray is ordered [from {0}, to {0}, from {1}, to {1}...]</returns>
      [MethodImpl(MethodImplOptions.AggressiveOptimization)]
		public unsafe static (int[] moveIndeciesArray, int halfOccupationLen) GetAllMovesetIndecies(in BitBoard board, bool isWhitesTurn, out ChessPiece[] ids) {
			const int ArrayInitSize = 40 * 2;

         ulong mask, bitmap;
         int index;

			int[] moveIndeciesArray = new int[ArrayInitSize];
			int moveIndeciesLen = 0;
			ids = new ChessPiece[64];

         fixed (ChessPiece* idsptr = ids) {
            if (isWhitesTurn) {
               ulong wp = board.WhitePieces;
               while (wp != 0) {
                  index = BitOperations.TrailingZeroCount(wp);
                  mask = ExtractLsb(wp);

                  #region Set bitmap
                  if ((mask & board.Pawns) != 0) {
                     idsptr[index] = ChessPiece.Pawn;
                     bitmap = WhitePawn(board, mask);
                  } else if ((mask & board.Knights) != 0) {
                     idsptr[index] = ChessPiece.Knight;
                     bitmap = KnightMoves[index] & ~board.WhitePieces;
                  } else if ((mask & board.Bishops) != 0) {
                     idsptr[index] = ChessPiece.Bishop;
                     bitmap = Diagonal(board.AllPiecesRot45C, board.AllPiecesRot45A, index) & ~board.WhitePieces;
                  } else if ((mask & board.Rooks) != 0) {
                     idsptr[index] = ChessPiece.Rook;
                     bitmap = Straight(board.AllPieces, board.AllPiecesFlippedA1H8, index) & ~board.WhitePieces;
                  } else if ((mask & board.Kings) != 0) {
                     var flags = board.Flags;
                     idsptr[index] = ChessPiece.King;
                     // don't mask out covered tiles so that the AI doesn't have to handle that case
                     bitmap = (KingMoves[index] & ~board.WhitePieces)
                        | ((flags & (BitBoardFlags.E1MovedOrTaken | BitBoardFlags.A1MovedOrTaken)) == BitBoardFlags.None && (board.AllPieces & 0xEul) == 0
                        && (board.BlackCoveredTiles & 0x1Cul) == 0 ? 0x4ul : 0x0ul)
                        | ((flags & (BitBoardFlags.E1MovedOrTaken | BitBoardFlags.H1MovedOrTaken)) == BitBoardFlags.None && (board.AllPieces & 0x60ul) == 0
                        && (board.BlackCoveredTiles & 0x70ul) == 0 ? 0x40ul : 0x0ul);
                  } else { // queen
                     idsptr[index] = ChessPiece.Queen;
                     bitmap = (Diagonal(board.AllPiecesRot45C, board.AllPiecesRot45A, index) | Straight(board.AllPieces, board.AllPiecesFlippedA1H8, index)) & ~board.WhitePieces;
                  }
                  #endregion

                  while (bitmap != 0) {
                     int lsb = BitOperations.TrailingZeroCount(bitmap);

                     // increase movelist length, while doing *2 in O(n), and +c is O(pow(n,2))
                     // this should never have to be resized more than once, which means it's basically O(1) anyways
                     if (moveIndeciesLen >= moveIndeciesArray.Length - 1) { // ensure at least 2 open slots
                        int[] temp = moveIndeciesArray;
                        moveIndeciesArray = new int[moveIndeciesArray.Length + ArrayInitSize / 2];
                        Array.Copy(temp, moveIndeciesArray, temp.Length);
                     }
                     // add move to movelist
                     moveIndeciesArray[moveIndeciesLen] = index;
                     moveIndeciesArray[moveIndeciesLen + 1] = lsb;
                     // increment movelistcount
                     moveIndeciesLen += 2;

                     bitmap = ResetLsb(bitmap);
                  }
                  wp = ResetLsb(wp);
               }
            } else {
               ulong bp = board.BlackPieces;
               while (bp != 0) {
                  index = BitOperations.TrailingZeroCount(bp);
                  mask = ExtractLsb(bp);

                  #region Set bitmap
                  if ((mask & board.Pawns) != 0) {
                     idsptr[index] = ChessPiece.Pawn;
                     bitmap = BlackPawn(board, mask);
                  } else if ((mask & board.Knights) != 0) {
                     idsptr[index] = ChessPiece.Knight;
                     bitmap = KnightMoves[index] & ~board.BlackPieces;
                  } else if ((mask & board.Bishops) != 0) {
                     idsptr[index] = ChessPiece.Bishop;
                     bitmap = Diagonal(board.AllPiecesRot45C, board.AllPiecesRot45A, index) & ~board.BlackPieces;
                  } else if ((mask & board.Rooks) != 0) {
                     idsptr[index] = ChessPiece.Rook;
                     bitmap = Straight(board.AllPieces, board.AllPiecesFlippedA1H8, index) & ~board.BlackPieces;
                  } else if ((mask & board.Kings) != 0) {
                     var flags = board.Flags;
                     idsptr[index] = ChessPiece.King;
                     // don't mask out covered tiles so that the AI doesn't have to handle that case
                     bitmap = (KingMoves[index] & ~board.BlackPieces)
                        | ((flags & (BitBoardFlags.E8MovedOrTaken | BitBoardFlags.A8MovedOrTaken)) == BitBoardFlags.None && (board.AllPieces & 0x0E00000000000000ul) == 0
                        && (board.WhiteCoveredTiles & 0x1C00000000000000ul) == 0 ? 0x0400000000000000ul : 0x0ul)
                        | ((flags & (BitBoardFlags.E8MovedOrTaken | BitBoardFlags.H8MovedOrTaken)) == BitBoardFlags.None && (board.AllPieces & 0x6000000000000000ul) == 0
                        && (board.WhiteCoveredTiles & 0x7000000000000000ul) == 0 ? 0x4000000000000000ul : 0x0ul);
                  } else { // queen
                     idsptr[index] = ChessPiece.Queen;
                     bitmap = (Diagonal(board.AllPiecesRot45C, board.AllPiecesRot45A, index) | Straight(board.AllPieces, board.AllPiecesFlippedA1H8, index)) & ~board.BlackPieces;
                  }
                  #endregion

                  while (bitmap != 0) {
                     int lsb = BitOperations.TrailingZeroCount(bitmap);

                     // increase movelist length, while doing *2 in O(n), and +c is O(pow(n,2))
                     // this should never have to be resized more than once, which means it's basically O(1) anyways
                     if (moveIndeciesLen >= moveIndeciesArray.Length - 1) { // ensure at least 2 open slots
                        int[] temp = moveIndeciesArray;
                        moveIndeciesArray = new int[moveIndeciesArray.Length + ArrayInitSize / 2];
                        Array.Copy(temp, moveIndeciesArray, temp.Length);
                     }
                     // add move to movelist
                     moveIndeciesArray[moveIndeciesLen] = index;
                     moveIndeciesArray[moveIndeciesLen + 1] = lsb;
                     // increment movelistcount
                     moveIndeciesLen += 2;

                     bitmap = ResetLsb(bitmap);
                  }
                  bp = ResetLsb(bp);
               }
            }
         }
			return (moveIndeciesArray, moveIndeciesLen / 2);
		}
      [MethodImpl(MethodImplOptions.AggressiveOptimization)]
      public unsafe static (int[] moveIndeciesArray, int halfOccupationLen) GetAllCapturesIndecies(in BitBoard board, bool isWhitesTurn, out ChessPiece[] ids) {
         const int ArrayInitSize = 40 * 2;

         ulong mask, bitmap;
         int index;

         int[] moveIndeciesArray = new int[ArrayInitSize];
         int moveIndeciesLen = 0;
         ids = new ChessPiece[64];

         fixed (ChessPiece* idsptr = ids) {
            if (isWhitesTurn) {
               ulong wp = board.WhitePieces;
               while (wp != 0) {
                  index = BitOperations.TrailingZeroCount(wp);
                  mask = ExtractLsb(wp);

                  #region Set bitmap
                  if ((mask & board.Pawns) != 0) {
                     idsptr[index] = ChessPiece.Pawn;
                     bitmap = WhitePawn(board, mask);
                  } else if ((mask & board.Knights) != 0) {
                     idsptr[index] = ChessPiece.Knight;
                     bitmap = KnightMoves[index];
                  } else if ((mask & board.Bishops) != 0) {
                     idsptr[index] = ChessPiece.Bishop;
                     bitmap = Diagonal(board.AllPiecesRot45C, board.AllPiecesRot45A, index);
                  } else if ((mask & board.Rooks) != 0) {
                     idsptr[index] = ChessPiece.Rook;
                     bitmap = Straight(board.AllPieces, board.AllPiecesFlippedA1H8, index);
                  } else if ((mask & board.Kings) != 0) {
                     var flags = board.Flags;
                     idsptr[index] = ChessPiece.King;
                     bitmap = KingMoves[index];
                  } else { // queen
                     idsptr[index] = ChessPiece.Queen;
                     bitmap = Diagonal(board.AllPiecesRot45C, board.AllPiecesRot45A, index) | Straight(board.AllPieces, board.AllPiecesFlippedA1H8, index);
                  }
                  #endregion

                  bitmap &= board.BlackPieces;
                  while (bitmap != 0) {
                     int lsb = BitOperations.TrailingZeroCount(bitmap);

                     // increase movelist length, while doing *2 in O(n), and +c is O(pow(n,2))
                     // this should never have to be resized more than once, which means it's basically O(1) anyways
                     if (moveIndeciesLen >= moveIndeciesArray.Length - 1) { // ensure at least 2 open slots
                        int[] temp = moveIndeciesArray;
                        moveIndeciesArray = new int[moveIndeciesArray.Length + ArrayInitSize / 2];
                        Array.Copy(temp, moveIndeciesArray, temp.Length);
                     }
                     // add move to movelist
                     moveIndeciesArray[moveIndeciesLen] = index;
                     moveIndeciesArray[moveIndeciesLen + 1] = lsb;
                     // increment movelistcount
                     moveIndeciesLen += 2;

                     bitmap = ResetLsb(bitmap);
                  }
                  wp = ResetLsb(wp);
               }
            } else {
               ulong bp = board.BlackPieces;
               while (bp != 0) {
                  index = BitOperations.TrailingZeroCount(bp);
                  mask = ExtractLsb(bp);

                  #region Set bitmap
                  if ((mask & board.Pawns) != 0) {
                     idsptr[index] = ChessPiece.Pawn;
                     bitmap = BlackPawn(board, mask);
                  } else if ((mask & board.Knights) != 0) {
                     idsptr[index] = ChessPiece.Knight;
                     bitmap = KnightMoves[index];
                  } else if ((mask & board.Bishops) != 0) {
                     idsptr[index] = ChessPiece.Bishop;
                     bitmap = Diagonal(board.AllPiecesRot45C, board.AllPiecesRot45A, index);
                  } else if ((mask & board.Rooks) != 0) {
                     idsptr[index] = ChessPiece.Rook;
                     bitmap = Straight(board.AllPieces, board.AllPiecesFlippedA1H8, index);
                  } else if ((mask & board.Kings) != 0) {
                     var flags = board.Flags;
                     idsptr[index] = ChessPiece.King;
                     bitmap = KingMoves[index];
                  } else { // queen
                     idsptr[index] = ChessPiece.Queen;
                     bitmap = Diagonal(board.AllPiecesRot45C, board.AllPiecesRot45A, index) | Straight(board.AllPieces, board.AllPiecesFlippedA1H8, index);
                  }
                  #endregion

                  bitmap &= board.WhitePieces;
                  while (bitmap != 0) {
                     int lsb = BitOperations.TrailingZeroCount(bitmap);

                     // increase movelist length, while doing *2 in O(n), and +c is O(pow(n,2))
                     // this should never have to be resized more than once, which means it's basically O(1) anyways
                     if (moveIndeciesLen >= moveIndeciesArray.Length - 1) { // ensure at least 2 open slots
                        int[] temp = moveIndeciesArray;
                        moveIndeciesArray = new int[moveIndeciesArray.Length + ArrayInitSize / 2];
                        Array.Copy(temp, moveIndeciesArray, temp.Length);
                     }
                     // add move to movelist
                     moveIndeciesArray[moveIndeciesLen] = index;
                     moveIndeciesArray[moveIndeciesLen + 1] = lsb;
                     // increment movelistcount
                     moveIndeciesLen += 2;

                     bitmap = ResetLsb(bitmap);
                  }
                  bp = ResetLsb(bp);
               }
            }
         }
         return (moveIndeciesArray, moveIndeciesLen / 2);
      }

      #endregion

      #region BitBoard Transformations

      [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.AggressiveInlining)]
      public static ulong FlipA1H8(ulong board) {
         ulong t;
         t = 0x0f0f0f0f00000000 & (board ^ (board << 28));
         board ^= t ^ (t >> 28);
         t = 0x3333000033330000 & (board ^ (board << 14));
         board ^= t ^ (t >> 14);
         t = 0x5500550055005500 & (board ^ (board << 7));
         board ^= t ^ (t >> 7);
         return board;
      }
      [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.AggressiveInlining)]
      public static ulong FlipA8H1(ulong board) {
         ulong t;
         t = board ^ (board << 36);
         board ^= 0xF0F0F0F00F0F0F0Ful & (t ^ (board >> 36));
         t = 0xCCCC0000CCCC0000ul & (board ^ (board << 18));
         board ^= t ^ (t >> 18);
         t = 0xAA00AA00AA00AA00ul & (board ^ (board << 9));
         board ^= t ^ (t >> 9);
         return board;
      }
      [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.AggressiveInlining)]
      public static ulong Rot45Clockwise(ulong board) {
         /*
            9 A B C D E F 0     9 1 1 1 1 1 1 1
            A B C D E F 0 1     A A 2 2 2 2 2 2
            B C D E F 0 1 2     B B B 3 3 3 3 3
            C D E F 0 1 2 3     C C C C 4 4 4 4
            D E F 0 1 2 3 4     D D D D D 5 5 5
            E F 0 1 2 3 4 5     E E E E E E 6 6
            F 0 1 2 3 4 5 6     F F F F F F F 7
            0 1 2 3 4 5 6 7     0 0 0 0 0 0 0 0
         */
         board ^= 0xAAAAAAAAAAAAAAAAul & (board ^ BitOperations.RotateRight(board, 8));
         board ^= 0xCCCCCCCCCCCCCCCCul & (board ^ BitOperations.RotateRight(board, 16));
         board ^= 0xF0F0F0F0F0F0F0F0ul & (board ^ BitOperations.RotateRight(board, 32));
         return board;
      }
      [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.AggressiveInlining)]
      public static ulong Rot45Anticlockwise(ulong board) {
         /*
            0 F E D C B A 9     1 1 1 1 1 1 1 9
            1 0 F E D C B A     2 2 2 2 2 2 A A
            2 1 0 F E D C B     3 3 3 3 3 B B B
            3 2 1 0 F E D C     4 4 4 4 C C C C
            4 3 2 1 0 F E D     5 5 5 D D D D D
            5 4 3 2 1 0 F E     6 6 E E E E E E
            6 5 4 3 2 1 0 F     7 F F F F F F F
            7 6 5 4 3 2 1 0     0 0 0 0 0 0 0 0
         */
         board ^= 0x5555555555555555ul & (board ^ BitOperations.RotateRight(board, 8));
         board ^= 0x3333333333333333ul & (board ^ BitOperations.RotateRight(board, 16));
         board ^= 0x0F0F0F0F0F0F0F0Ful & (board ^ BitOperations.RotateRight(board, 32));
         return board;
      }

		#endregion

		#region Misc

		/// <summary>
		/// Utility function, should not be used in extremely performant code.
		/// </summary>
		/// <param name="board">Board to retrieve piece from.</param>
		/// <param name="index">Zero-based index of position to get piece at.</param>
		/// <returns></returns>
		public static ChessPiece GetPieceAtIndex(in BitBoard board, int index) {
			ulong loc = 1ul << index;
			if ((board.AllPieces & loc) == 0ul) {
				return ChessPiece.None;
			} else {
				if ((board.Pawns & loc) != 0) {
					return ChessPiece.Pawn;
				} else if ((board.Knights & loc) != 0) {
					return ChessPiece.Knight;
				} else if ((board.Bishops & loc) != 0) {
					return ChessPiece.Bishop;
				} else if ((board.Rooks & loc) != 0) {
					return ChessPiece.Rook;
				} else if ((board.Queens & loc) != 0) {
					return ChessPiece.Queen;
				} else if ((board.Kings & loc) != 0) {
					return ChessPiece.King;
				}
			}
			throw new Exception("Invalid board configuration.");
		}

		#endregion

	}
}

