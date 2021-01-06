using System;
using System.Threading;
using InfraredEngine.Chessboard;

namespace InfraredEngine {
	static class Uci {
		public static void UciLoop() {
			Console.Write($"id name Infrared\n");
			Console.Write($"id author sfbea\n");
			Console.Write($"uciok\n");

			var info = new PersistentSearchInfo();
			var board = new BitBoard(BoardVariant.Default);

			while (true) {
				string stdin = Console.ReadLine() ?? ""; // todo: fix eval display, fix eval + pawn eval, fix black side play, fix your life

				if (string.IsNullOrWhiteSpace(stdin))
					continue;

				

				if (stdin == "isready") {
					Console.Write("readyok\n");
					continue;
				} else if (stdin.Length >= 8 && stdin.Substring(0, 8) == "position") {
					board = ParsePositionUci(info, stdin);
				} else if (stdin.Length >= 10 && stdin.Substring(0, 10) == "ucinewgame") {
					board = ParsePositionUci(info, "position startpos");
				} else if (stdin.Length >= 2 && stdin.Substring(0, 2) == "go") {
					ParseGoUci(board, info, stdin);
				} else if (stdin == "stop") {
					info.Timer?.Stop();
					LogBestmove(info.PrincipalVariation, board);
					info.Stopped = true;
				} else if (stdin == "quit") {
					info.Stopped = true;
					break;
				} else if (stdin == "uci") {
					Console.Write($"id name Infrared\n");
					Console.Write($"id author sfbea\n");
					Console.Write($"uciok\n");
				}

				// NOT UCI; TESTING ONLY
				if (stdin == "eval") {
					Console.WriteLine($"board eval: {Engine.Evaluator.Evaluate(board)}");
				}
			}
		}

		static BitBoard ParsePositionUci(PersistentSearchInfo info, string stdin) {
			info.Reset();

			// position startpos
			// position fen {fen}
			// ... moves e2e4 e7e5 b7b8q
			stdin = stdin.Substring(9);
			BitBoard board;

			if (stdin.Substring(0, 3) == "fen") {
				string fen = stdin.Substring(4).Split(" moves ")[0].Trim();
				board = ChessStringCoding.DecodeFEN(fen);
			} else {
				board = new BitBoard(BoardVariant.Default);
			}

			if (stdin.Contains(" moves ")) {
				string[] moves = stdin.Split(" moves ")[1].Split(' ');
				for (int i = 0; i < moves.Length; i++) {
					var (from, to) = ChessStringCoding.DecodeMove(moves[i], out ChessPiece promotion);
					board = new BitBoard(board, from, to, BitBoardUtils.GetPieceAtIndex(board, from));

					if (promotion != ChessPiece.None && promotion != ChessPiece.Queen) {
						// this code will basically never run, but who knows
						int zIdOffset = ZobristHashes.GetColourOffset(!board.Flags.HasFlag(BitBoardFlags.WhitesTurn));
						ulong lto = 1ul << to;

						board.Queens &= ~lto;
						board.ZobristId ^= ZobristHashes.Hashes[(int)ChessPiece.Queen + zIdOffset, to]; // xor out queen
						board.ZobristId ^= ZobristHashes.Hashes[(int)promotion + zIdOffset, to];        // xor in promoted piece
						switch (promotion) {
							case ChessPiece.Rook:
								board.Rooks |= lto;
								break;
							case ChessPiece.Bishop:
								board.Bishops |= lto;
								break;
							case ChessPiece.Knight:
								board.Knights |= lto;
								break;
							default:
								throw new Exception();
						}

						if (i == moves.Length - 1)
							// if this is the last move instruction, covered tiles must be updated
							board.SetDerivitiveValues();
					}
				}
			}

			return board;
		}

		static void ParseGoUci(BitBoard board, PersistentSearchInfo info, string stdin) {
			// go depth 8 wtime 300000 btime 3000000 winc 1000 binc 1000 movetime 1500 movestogo 35

			bool isWhitesTurn = board.Flags.HasFlag(BitBoardFlags.WhitesTurn);

			int time = int.MinValue;
			int increment = 0;
			int depth = int.MinValue;
			int movestogo = 30;
			int movetime = int.MinValue;

			string[] paramaters = stdin.Split(' ');
			for (int i = 1 /* skip 'go' */; i < paramaters.Length; i++) {
				// 'infinite' param will just skip over everything and continue
				if (paramaters[i] == "wtime" && isWhitesTurn || paramaters[i] == "btime" && !isWhitesTurn) {
					time = int.Parse(paramaters[i + 1]);
					i++;
				} else if (paramaters[i] == "winc" && isWhitesTurn || paramaters[i] == "binc" && !isWhitesTurn) {
					increment = int.Parse(paramaters[i + 1]);
					i++;
				} else if (paramaters[i] == "movetime") {
					movetime = int.Parse(paramaters[i + 1]);
					i++;
				} else if (paramaters[i] == "movestogo") {
					movestogo = int.Parse(paramaters[i + 1]);
					i++;
				} else if (paramaters[i] == "depth") {
					depth = int.Parse(paramaters[i + 1]);
					i++;
				}
			}

			// create search thread
			var thread = new Thread(() => Engine.Search.Search_PVS_IDF(board, info));

			// configure depth limit
			info.MaxIdfDepth = depth == int.MinValue ? 50 : depth;
			// configure engine timer
			if (time != int.MinValue || movetime != int.MinValue) {
				var timer = new System.Timers.Timer {
					AutoReset = false,
					// time controls
					Interval = movetime == int.MinValue ? time / movestogo + increment - 50 : movetime - 50,
				};

				timer.Elapsed += (object sender, System.Timers.ElapsedEventArgs args) => {
					LogBestmove(info.PrincipalVariation, board);
					info.Stopped = true;
					timer.Close();
				};

				timer.Start();
				info.Timer = timer;
			}
			thread.Start();
		}

		static void LogBestmove((int from, int to) pv, in BitBoard board) {
			Console.WriteLine("bestmove " + ChessStringCoding.EncodeMove(pv.from, pv.to, 
				(pv.to > 55 || pv.to < 8) && BitBoardUtils.GetPieceAtIndex(board, pv.from) == ChessPiece.Pawn ? ChessPiece.Queen : ChessPiece.None));
		}
	}
}
