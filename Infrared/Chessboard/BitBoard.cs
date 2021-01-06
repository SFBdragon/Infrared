using System;
using System.Numerics;
using static InfraredEngine.Chessboard.BitBoardUtils;
using static InfraredEngine.Chessboard.BitboardMoveTables;
using static InfraredEngine.Chessboard.ZobristHashes;

namespace InfraredEngine.Chessboard {
	public struct BitBoard {
		public ulong ZobristId;

		public BitBoardFlags Flags;
		public ulong EnPassant;

		public ulong AllPieces;
		public ulong AllPiecesFlippedA1H8;
		public ulong AllPiecesRot45C;
		public ulong AllPiecesRot45A;

		public ulong WhitePieces;
		public ulong BlackPieces;
		public ulong WhiteCoveredTiles;
		public ulong BlackCoveredTiles;

		public ulong Pawns;
		public ulong Knights;
		public ulong Bishops;
		public ulong Rooks;
		public ulong Queens;
		public ulong Kings;

		/// <summary>
		/// Setup a new chessboard.
		/// </summary>
		/// <param name="variant">Constructor differentiator.</param>
		public BitBoard(BoardVariant variant) {
			_ = variant;
			Flags = BitBoardFlags.WhitesTurn;
			EnPassant = 0ul;

			WhitePieces = 0x000000000000FFFFul;
			BlackPieces = 0xFFFF000000000000ul;

			Pawns = 0x00FF00000000FF00ul;
			Knights = 0x4200000000000042ul;
			Bishops = 0x2400000000000024ul;
			Rooks = 0x8100000000000081ul;
			Queens = 0x0800000000000008ul;
			Kings = 0x1000000000000010ul;

			AllPieces = 0xFFFF00000000FFFFul;
			AllPiecesFlippedA1H8 = 0xC3C3C3C3C3C3C3C3ul;
			AllPiecesRot45C = 0x70F1E3C78F0E1C3ul;
			AllPiecesRot45A = 0xE0F0783C1E0F87C3ul;

			WhiteCoveredTiles = 0x0000000000FFFF7Eul;
			BlackCoveredTiles = 0x7EFFFF0000000000ul;

			ZobristId = 0;
			CalcFullZobristId();
		}

		/// <summary>
		/// Plays a turn given the previous board state and move locations.
		/// </summary>
		/// <param name="board">Board from which to play the turn.</param>
		/// <param name="from">Zero-based index of piece to move.</param>
		/// <param name="to">Zero-based index of piece destination.</param>
		/// <param name="moved">Piece type being moved.</param>
		[System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
		public BitBoard(in BitBoard board, int from, int to, ChessPiece moved) {

			ulong lfrom = 1ul << from;
			ulong lto = 1ul << to;
			ulong lfromN = ~lfrom;
			ulong ltoN = ~lto;

			ChessPiece taken
				= (board.AllPieces & lto) == 0
				? ChessPiece.None
				: (board.Pawns & lto) != 0
				? ChessPiece.Pawn
				: (board.Knights & lto) != 0
				? ChessPiece.Knight
				: (board.Bishops & lto) != 0
				? ChessPiece.Bishop
				: (board.Rooks & lto) != 0
				? ChessPiece.Rook
				: (board.Queens & lto) != 0
				? ChessPiece.Queen
				: ChessPiece.King;

			bool isWhitesPlay = board.Flags.HasFlag(BitBoardFlags.WhitesTurn);

			// Flags & EnPassant
			Flags = (board.Flags ^ BitBoardFlags.WhitesTurn) | (BitBoardFlags)((lfrom | lto) & 0x9100000000000091ul);
			EnPassant = moved == ChessPiece.Pawn && (lfrom & 0x00FF00000000FF00ul) != 0 && (lto & 0x000000FFFF000000ul) != 0 ? 1ul << ((from + to) / 2) : 0ul;

			// ZobristId
			int zIdColourOffset = GetColourOffset(isWhitesPlay);
			int takenId = (int)taken + (zIdColourOffset ^ 7);
			int pieceId = (int)moved + zIdColourOffset;
			ZobristId = board.ZobristId
				^ Hashes[takenId, to]   // xor out existing piece
				^ Hashes[pieceId, to]   // xor in moved piece destination
				^ Hashes[pieceId, from] // xor out moved piece source tile
				^ Hashes[0, from];      // xor in blank source tile

			// **Pieces
			if (isWhitesPlay) {
				WhitePieces = (board.WhitePieces & lfromN) | lto;
				BlackPieces = board.BlackPieces & ltoN;
			} else {
				WhitePieces = board.WhitePieces & ltoN;
				BlackPieces = (board.BlackPieces & lfromN) | lto;
			}

			// [ChessPiece]
			Pawns = board.Pawns;
			Knights = board.Knights;
			Bishops = board.Bishops;
			Rooks = board.Rooks;
			Queens = board.Queens;
			Kings = board.Kings;

			switch (taken) {
				case ChessPiece.Pawn:
					Pawns &= ltoN;
					break;
				case ChessPiece.Knight:
					Knights &= ltoN;
					break;
				case ChessPiece.Bishop:
					Bishops &= ltoN;
					break;
				case ChessPiece.Rook:
					Rooks &= ltoN;
					break;
				case ChessPiece.Queen:
					Queens &= ltoN;
					break;
				case ChessPiece.King:
					Kings &= ltoN;
					break;
			}
			switch (moved) {
				case ChessPiece.Pawn:
					if ((lto & 0xFF000000000000FFul) != 0) {
						// promotion
						Pawns &= lfromN;
						Queens |= lto;

						ZobristId ^= Hashes[(int)ChessPiece.Pawn + zIdColourOffset, to];
						ZobristId ^= Hashes[(int)ChessPiece.Queen + zIdColourOffset, to];
					} else if (lto == board.EnPassant) {
						// capture by en passant

						int takenIndex = from / 8 * 8 + to & 7; // &7 is cheaper than %8
						ulong ltakenN = ~(1ul << takenIndex);

						Pawns = (board.Pawns & lfromN & ltakenN) | lto;
						WhitePieces &= ltakenN;
						WhitePieces &= ltakenN;

						ZobristId ^= Hashes[(int)ChessPiece.Pawn + (zIdColourOffset ^ 7), takenIndex];
						ZobristId ^= Hashes[0, takenIndex];
					} else {
						Pawns = (board.Pawns & lfromN) | lto;
					}
					break;
				case ChessPiece.Knight:
					Knights = (board.Knights & lfromN) | lto;
					break;
				case ChessPiece.Bishop:
					Bishops = (board.Bishops & lfromN) | lto;
					break;
				case ChessPiece.Rook:
					Rooks = (board.Rooks & lfromN) | lto;
					break;
				case ChessPiece.Queen:
					Queens = (board.Queens & lfromN) | lto;
					break;
				case ChessPiece.King:
					Kings = (board.Kings & lfromN) | lto;
					// if king is castling, move the rook accordingly
					if (lfrom << 2 == lto) { // kingside
						Rooks &= ~(lto << 1);
						Rooks |= lto >> 1;
						ZobristId ^= Hashes[(int)ChessPiece.Rook + zIdColourOffset, to + 1]; // xor out rook
						ZobristId ^= Hashes[0, to + 1];                                      // xor in blank
						ZobristId ^= Hashes[0, to - 1];                                      // xor out blank
						ZobristId ^= Hashes[(int)ChessPiece.Rook + zIdColourOffset, to - 1]; // xor in rook
						if ((board.Flags & BitBoardFlags.WhitesTurn) == BitBoardFlags.WhitesTurn) {
							WhitePieces &= ~(lto << 1);
							WhitePieces |= lto >> 1;
						} else {
							BlackPieces &= ~(lto << 1);
							BlackPieces |= lto >> 1;
						}
					} else if (lfrom >> 2 == lto) { // queenside
						Rooks &= ~(lto >> 2);
						Rooks |= lto << 1;
						ZobristId ^= Hashes[(int)ChessPiece.Rook + zIdColourOffset, to - 2]; // xor out rook
						ZobristId ^= Hashes[0, to - 2];                                      // xor in blank
						ZobristId ^= Hashes[0, to + 1];                                      // xor out blank
						ZobristId ^= Hashes[(int)ChessPiece.Rook + zIdColourOffset, to + 1]; // xor in rook
						if ((board.Flags & BitBoardFlags.WhitesTurn) == BitBoardFlags.WhitesTurn) {
							WhitePieces &= ~(lto >> 2);
							WhitePieces |= lto << 1;
						} else {
							BlackPieces &= ~(lto >> 2);
							BlackPieces |= lto << 1;
						}
					}
					break;
			}

			#region Set Derivative Values

			AllPieces = WhitePieces | BlackPieces;
			AllPiecesFlippedA1H8 = FlipA1H8(AllPieces);
			AllPiecesRot45C = Rot45Clockwise(AllPieces);
			AllPiecesRot45A = Rot45Anticlockwise(AllPieces);

			WhiteCoveredTiles = 0ul;
			ulong wp = WhitePieces & ~Pawns;
			while (wp != 0) {
				int i = BitOperations.TrailingZeroCount(wp);
				ulong loc = 1ul << i;
				if ((loc & Knights) != 0) {
					WhiteCoveredTiles |= KnightMoves[i];
				} else if ((loc & Bishops) != 0) {
					WhiteCoveredTiles |= Diagonal(AllPiecesRot45C, AllPiecesRot45A, i);
				} else if ((loc & Rooks) != 0) {
					WhiteCoveredTiles |= Straight(AllPieces, AllPiecesFlippedA1H8, i);
				} else if ((loc & Kings) != 0) {
					WhiteCoveredTiles |= KingMoves[i];
				} else {
					WhiteCoveredTiles |= Diagonal(AllPiecesRot45C, AllPiecesRot45A, i) | Straight(AllPieces, AllPiecesFlippedA1H8, i);
				}
				wp ^= loc;
			}
			WhiteCoveredTiles |= (WhitePieces & Pawns & 0x7F7F7F7F7F7F7F7Ful) << 9;
			WhiteCoveredTiles |= (WhitePieces & Pawns & 0xFEFEFEFEFEFEFEFEul) << 7;

			BlackCoveredTiles = 0ul;
			ulong bp = BlackPieces & ~Pawns;
			while (bp != 0) {
				int i = BitOperations.TrailingZeroCount(bp);
				ulong loc = 1ul << i;
				if ((loc & Knights) != 0) {
					BlackCoveredTiles |= KnightMoves[i];
				} else if ((loc & Bishops) != 0) {
					BlackCoveredTiles |= Diagonal(AllPiecesRot45C, AllPiecesRot45A, i);
				} else if ((loc & Rooks) != 0) {
					BlackCoveredTiles |= Straight(AllPieces, AllPiecesFlippedA1H8, i);
				} else if ((loc & Kings) != 0) {
					BlackCoveredTiles |= KingMoves[i];
				} else {
					BlackCoveredTiles |= Diagonal(AllPiecesRot45C, AllPiecesRot45A, i) | Straight(AllPieces, AllPiecesFlippedA1H8, i);
				}
				bp ^= loc;
			}
			BlackCoveredTiles |= (BlackPieces & Pawns & 0x7F7F7F7F7F7F7F7Ful) >> 7;
			BlackCoveredTiles |= (BlackPieces & Pawns & 0xFEFEFEFEFEFEFEFEul) >> 9;

			#endregion
		}

		/// <summary>
		/// Sets AllPieces, AllPiecesFlippedA1H8, AllPiecesRot45C, AllPiecesRot45A, WhiteCoveredPieces, and BlackCoveredPieces.
		/// </summary>
		public void SetDerivitiveValues() {
			AllPieces = WhitePieces | BlackPieces;
			AllPiecesFlippedA1H8 = FlipA1H8(AllPieces);
			AllPiecesRot45C = Rot45Clockwise(AllPieces);
			AllPiecesRot45A = Rot45Anticlockwise(AllPieces);

			WhiteCoveredTiles = 0ul;
			ulong wp = WhitePieces & ~Pawns;
			while (wp != 0) {
				int i = BitOperations.TrailingZeroCount(wp);
				ulong loc = 1ul << i;
				if ((loc & Knights) != 0) {
					WhiteCoveredTiles |= KnightMoves[i];
				} else if ((loc & Bishops) != 0) {
					WhiteCoveredTiles |= Diagonal(AllPiecesRot45C, AllPiecesRot45A, i);
				} else if ((loc & Rooks) != 0) {
					WhiteCoveredTiles |= Straight(AllPieces, AllPiecesFlippedA1H8, i);
				} else if ((loc & Kings) != 0) {
					WhiteCoveredTiles |= KingMoves[i];
				} else {
					WhiteCoveredTiles |= Diagonal(AllPiecesRot45C, AllPiecesRot45A, i) | Straight(AllPieces, AllPiecesFlippedA1H8, i);
				}
				wp ^= loc;
			}
			WhiteCoveredTiles |= (WhitePieces & Pawns & 0x7F7F7F7F7F7F7F7Ful) << 9;
			WhiteCoveredTiles |= (WhitePieces & Pawns & 0xFEFEFEFEFEFEFEFEul) << 7;

			BlackCoveredTiles = 0ul;
			ulong bp = BlackPieces & ~Pawns;
			while (bp != 0) {
				int i = BitOperations.TrailingZeroCount(bp);
				ulong loc = 1ul << i;
				if ((loc & Knights) != 0) {
					BlackCoveredTiles |= KnightMoves[i];
				} else if ((loc & Bishops) != 0) {
					BlackCoveredTiles |= Diagonal(AllPiecesRot45C, AllPiecesRot45A, i);
				} else if ((loc & Rooks) != 0) {
					BlackCoveredTiles |= Straight(AllPieces, AllPiecesFlippedA1H8, i);
				} else if ((loc & Kings) != 0) {
					BlackCoveredTiles |= KingMoves[i];
				} else {
					BlackCoveredTiles |= Diagonal(AllPiecesRot45C, AllPiecesRot45A, i) | Straight(AllPieces, AllPiecesFlippedA1H8, i);
				}
				bp ^= loc;
			}
			BlackCoveredTiles |= (BlackPieces & Pawns & 0x7F7F7F7F7F7F7F7Ful) >> 7;
			BlackCoveredTiles |= (BlackPieces & Pawns & 0xFEFEFEFEFEFEFEFEul) >> 9;
		}

		public void CalcFullZobristId() {
			ZobristId = 0ul;
			ulong loc = 1ul;
			for (int i = 0; i < 64; i++, loc <<= 1) {
				if ((AllPieces & loc) == 0) {
					ZobristId ^= Hashes[0, i];
				} else {
					int isBlack = (WhitePieces & loc) == 0 ? 6 : 0;
					if ((Pawns & loc) != 0) {
						ZobristId ^= Hashes[1 + isBlack, i];
					} else if ((Knights & loc) != 0) {
						ZobristId ^= Hashes[2 + isBlack, i];
					} else if ((Bishops & loc) != 0) {
						ZobristId ^= Hashes[3 + isBlack, i];
					} else if ((Rooks & loc) != 0) {
						ZobristId ^= Hashes[4 + isBlack, i];
					} else if ((Queens & loc) != 0) {
						ZobristId ^= Hashes[5 + isBlack, i];
					} else if ((Kings & loc) != 0) {
						ZobristId ^= Hashes[6 + isBlack, i];
					}
				}
			}
		}

		public override bool Equals(object? obj) {
			if (obj is null)
				throw new Exception();
			var bitBoard = (BitBoard)obj;
			return
				bitBoard.Flags == Flags &&
				bitBoard.AllPieces == AllPieces &&
				bitBoard.WhitePieces == WhitePieces &&
				bitBoard.BlackPieces == BlackPieces &&
				bitBoard.Pawns == Pawns &&
				bitBoard.Knights == Knights &&
				bitBoard.Bishops == Bishops &&
				bitBoard.Rooks == Rooks &&
				bitBoard.Queens == Queens &&
				bitBoard.Kings == Kings;
		}
		public override int GetHashCode() {
			return HashCode.Combine(Pawns, Knights, Bishops, Rooks, Queens, Kings, Flags, EnPassant);
		}
		public static bool operator ==(BitBoard left, BitBoard right) => left.Equals(right);
		public static bool operator !=(BitBoard left, BitBoard right) => !left.Equals(right);
	}
}
