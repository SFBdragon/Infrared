using System;
using InfraredEngine.Chessboard;

namespace InfraredEngine {
	public static class ChessStringCoding {
		public const string StartingPosFEN = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";

		public static BitBoard DecodeFEN(string fen) {
			string[] parts = fen.Trim().Split(' ');
			BitBoard board = new BitBoard();

			if (parts[1] == "w")
				board.Flags |= BitBoardFlags.WhitesTurn;
			if (parts[2] != "-") {
				if (!parts[2].Contains('Q'))
					board.Flags |= BitBoardFlags.A1MovedOrTaken;
				if (!parts[2].Contains('K'))
					board.Flags |= BitBoardFlags.A8MovedOrTaken;
				if (!parts[2].Contains('q'))
					board.Flags |= BitBoardFlags.H1MovedOrTaken;
				if (!parts[2].Contains('k'))
					board.Flags |= BitBoardFlags.H8MovedOrTaken;
			}
			if (parts[3] != "-")
				board.EnPassant = 1ul << (parts[3][0] - 'a' + (int.Parse(parts[3][1].ToString()) - 1) * 8);

			string pieces = parts[0].Replace("/", null);
			System.Diagnostics.Debug.Assert(pieces.Length == 64);
			for (int i = 63, skip = 0; i >= 0; i--) {
				if (skip > 0) {
					skip--;
					continue;
				}

				ulong bitboardLoc = 1ul << i;
				char chr = pieces[i];

				if (char.IsDigit(chr)) {
					skip = int.Parse(chr.ToString()) - 1;
					continue;
				} else if (char.IsLetter(chr)) {
					if (char.IsUpper(chr)) {
						switch (chr) {
							case 'P':
								board.Pawns |= bitboardLoc;
								board.WhitePieces |= bitboardLoc;
								break;
							case 'N':
								board.Knights |= bitboardLoc;
								board.WhitePieces |= bitboardLoc;
								break;
							case 'B':
								board.Bishops |= bitboardLoc;
								board.WhitePieces |= bitboardLoc;
								break;
							case 'R':
								board.Rooks |= bitboardLoc;
								board.WhitePieces |= bitboardLoc;
								break;
							case 'Q':
								board.Queens |= bitboardLoc;
								board.WhitePieces |= bitboardLoc;
								break;
							case 'K':
								board.Kings |= bitboardLoc;
								board.WhitePieces |= bitboardLoc;
								break;
							default: throw new Exception();
						}
					} else {
						switch (chr) {
							case 'P':
								board.Pawns |= bitboardLoc;
								board.BlackPieces |= bitboardLoc;
								break;
							case 'N':
								board.Knights |= bitboardLoc;
								board.BlackPieces |= bitboardLoc;
								break;
							case 'B':
								board.Bishops |= bitboardLoc;
								board.BlackPieces |= bitboardLoc;
								break;
							case 'R':
								board.Rooks |= bitboardLoc;
								board.BlackPieces |= bitboardLoc;
								break;
							case 'Q':
								board.Queens |= bitboardLoc;
								board.BlackPieces |= bitboardLoc;
								break;
							case 'K':
								board.Kings |= bitboardLoc;
								board.BlackPieces |= bitboardLoc;
								break;
							default:
								throw new Exception();
						}
					}
				}
			}

			board.SetDerivitiveValues();
			board.CalcFullZobristId();
			return board;
		}

		public static string EncodeMove(int from, int to, ChessPiece promotion) {
			return ((char)('a' + from % 8)).ToString() + (from / 8 + 1).ToString() + ((char)('a' + to % 8)).ToString() + (to / 8 + 1).ToString()
				+ promotion switch {
					ChessPiece.None => "",
					ChessPiece.Queen => "q",
					ChessPiece.Rook => "r",
					ChessPiece.Bishop => "b",
					ChessPiece.Knight => "n",
					_ => throw new Exception(),
				};
		}
		public static (int from, int to) DecodeMove(string move, out ChessPiece promotion) {
			move = move.Trim().ToLower();

			promotion = ChessPiece.None;
			if (move.Length > 4 && char.IsLetter(move[4])) {
				promotion = move[4] switch {
					'q' => ChessPiece.Queen,
					'r' => ChessPiece.Rook,
					'n' => ChessPiece.Knight,
					'b' => ChessPiece.Bishop,
					_ => throw new Exception($"Invalid promotion information: {move[4]}."),
				};
			}

			return (move[0] - 'a' + (int.Parse(move[1].ToString()) - 1) * 8,
					  move[2] - 'a' + (int.Parse(move[3].ToString()) - 1) * 8);
		}
	}
}
