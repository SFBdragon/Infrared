using System;
using System.Runtime.CompilerServices;
using static System.Numerics.BitOperations;
using InfraredEngine.Chessboard;

namespace InfraredEngine.Engine {
	public class Search {

		[MethodImpl(MethodImplOptions.AggressiveOptimization)]
		public static void Search_PVS_IDF(in BitBoard board, PersistentSearchInfo info) {
			bool colour = board.Flags.HasFlag(BitBoardFlags.WhitesTurn);
			var (moves, len) = BitBoardUtils.GetAllMovesetIndecies(board, colour, out ChessPiece[] ids);

			// only relevant in extremely low-on-time scenarios
			info.PrincipalVariation = (moves[0], moves[1]);

			int pvIndex = 0;
			float currPawnValue = Evaluator.GetCurrentPawnValue(board);
			var stopwatch = new System.Diagnostics.Stopwatch();

			for (int depth = 2; depth < info.MaxIdfDepth; depth++) {
				int oldSPC = info.SearchedPositionsCount;
				stopwatch.Restart();

				int alpha = -int.MaxValue;
				int ppvi = pvIndex;
				var pv = (from: -1, to: -1);
				for (int m = 0, pvi = ppvi; m < len; m++, pvi = (ppvi + m) % len) {
					if (info.Stopped)
						break;

					int from = moves[pvi * 2];
					int to = moves[pvi * 2 + 1];

					var play = new BitBoard(board, from, to, ids[from]);
					int score;
					if (m == 0) {
						score = -NegaScout(play, depth - 1, !colour, -int.MaxValue, -alpha, true, info);
					} else {
						score = -NegaScout(play, depth - 1, !colour, -alpha - 1, -alpha, true, info); // search with a zero-window
						if (score > alpha)
							score = -NegaScout(play, depth - 1, !colour, -int.MaxValue, -alpha, true, info); // do a full re-search if failed high
					}

					if (score > alpha) {
						alpha = score;
						pv = (from, to);
						pvIndex = pvi;
					}
				}

				// update timing info
				stopwatch.Stop();
				info.SearchDuration += stopwatch.Elapsed;

				// check if the iteration has been prematurely stopped
				if (info.Stopped)
					break;

				// update info
				info.Evaluation = (int)(alpha / currPawnValue * 100f);
				info.PrincipalVariation = pv;
				// /2 due to ply -> move conversion, -1 due to taken -> mated conversion
				int absAlpha = Math.Abs(alpha);
				info.ForcedMate = absAlpha >= Evaluator.CheckmateValue - 64 ? (depth - (absAlpha - Evaluator.CheckmateValue)) / 2 - 1 : null;


				// output UCI-compliant info
				Console.WriteLine(string.Format("info depth {0} nodes {1} nps {2} score cp {3} {4} time {5}, pv {6}",
					depth,
					info.SearchedPositionsCount - oldSPC,
					(int)((info.SearchedPositionsCount - oldSPC) / (float)stopwatch.Elapsed.TotalSeconds),
					info.Evaluation,
					info.ForcedMate.HasValue ? "mate " + info.ForcedMate.Value.ToString() + " " : "",
					stopwatch.ElapsedMilliseconds,
					ChessStringCoding.EncodeMove(pv.from, pv.to, (pv.to > 55 || pv.to < 8) && BitBoardUtils.GetPieceAtIndex(board, pv.from) == ChessPiece.Pawn
					? ChessPiece.Queen : ChessPiece.None)));

				// reset evaluation hashtable
				//info.EvaluationHashtable = new ();
			}
		}


		[MethodImpl(MethodImplOptions.AggressiveOptimization)]
		static int NegaScout(in BitBoard board, int depth, bool colour, int alpha, int beta, bool allowNullMove, PersistentSearchInfo info) {

			info.SearchedPositionsCount++;
			if (info.Stopped)
				return 0;

			ulong selfPieces = colour ? board.WhitePieces : board.BlackPieces;

			if (InfraredBitOps.PopCountU32(board.Kings) < 2u) { // king killed breakout
				int eval = Evaluator.CheckmateValue + depth;
				return (selfPieces & board.Kings) != 0ul ? eval : -eval;
			}

			// evaluation
			if (depth <= 0)
				return Queince(board, depth, colour, alpha, beta, info);

			ulong oppCoveredTiles = colour ? board.BlackCoveredTiles : board.WhiteCoveredTiles;
			bool isInCheck = (board.Kings & selfPieces & oppCoveredTiles) != 0;

			//if (isInCheck)
			//	// check extension
			//	depth++;

			//// todo: check whether this is actually improving quality of play, not just depth
			//// null move heuristic (do not attempt in check, after a previous null move, or if the player has to few or only pawn and king pieces)
			//;const int R_NmhDepthSave = 2;7
			//if (depth > R_NmhDepthSave + 2 && !isInCheck && allowNullMove && (selfPieces & ~(board.Kings | board.Pawns)) != 0ul) {
			//	var nullplay = new BitBoard(board);
			//	int eval = -NegaScout(nullplay, depth - 1 - R_NmhDepthSave, !colour, -beta, -beta - 1, pvHashtable, false);
			//	if (eval >= beta)
			//		return beta;
			//}

			// null move reduction heuristic http://elidavid.com/pubs/nmr.pdf
			const int R_MAX = 4;
			const int R_MIN = 3;
			const int DEPTH_R = 3;
			if (depth > R_MIN && !isInCheck && allowNullMove && (selfPieces & ~(board.Kings | board.Pawns)) != 0ul) {
				int R = depth > 6 ? R_MAX : R_MIN;

				var nullplay = new BitBoard {
					Flags = board.Flags ^ BitBoardFlags.WhitesTurn,
					EnPassant = 0ul,
					ZobristId = board.ZobristId,

					AllPieces = board.AllPieces,
					AllPiecesFlippedA1H8 = board.AllPiecesFlippedA1H8,
					AllPiecesRot45C = board.AllPiecesRot45C,
					AllPiecesRot45A = board.AllPiecesRot45A,

					WhitePieces = board.WhitePieces,
					BlackPieces = board.BlackPieces,
					WhiteCoveredTiles = board.WhiteCoveredTiles,
					BlackCoveredTiles = board.BlackCoveredTiles,

					Pawns = board.Pawns,
					Knights = board.Knights,
					Bishops = board.Bishops,
					Rooks = board.Rooks,
					Queens = board.Queens,
					Kings = board.Kings,
				};
				int eval = -NegaScout(nullplay, depth - R - 1, !colour, -beta, -beta - 1, false, info);
				if (eval >= beta) {
					depth -= DEPTH_R;
					if (depth <= 0)
						return Queince(board, depth, colour, alpha, beta, info);
				}
			}

			// get all moves for this position
			var (moves, len) = BitBoardUtils.GetAllMovesetIndecies(board, colour, out ChessPiece[] ids);

			// check if a principal variation is already known for this position
			ulong tempoDependantHash = colour ? board.ZobristId : ~board.ZobristId;
			if (!info.PrincipalVariationHashtable.TryGetValue(tempoDependantHash, out (int pvMoveIndex, int pvDepth) pv))
				pv = (0, int.MinValue);

			int pvIndex = pv.pvMoveIndex;
			if (pv.pvDepth == int.MinValue || pv.pvMoveIndex >= len) {
				// if no previous principal variation has been found, do Negamax instead
				for (int m = 0, score; m < len; m++) {
					int from = moves[m * 2];
					int to = moves[m * 2 + 1];

					score = -NegaScout(new BitBoard(board, from, to, ids[from]), depth - 1, !colour, -beta, -alpha, true, info);
					if (score > alpha) {
						alpha = score;
						pvIndex = m;
					}
					if (alpha >= beta)
						break;
				}
			} else {
				// do PVS as there is a potentially valid previously found principal variation (due to hash collisions, it may not be accurate, but shouldn't cause issues)
				int ppvi = pv.pvMoveIndex;
				for (int m = 0, pvi = ppvi, score; m < len; m++, pvi = (ppvi + m) % len) {
					int from = moves[pvi * 2];
					int to = moves[pvi * 2 + 1];

					var play = new BitBoard(board, from, to, ids[from]);
					if (m == 0) {
						score = -NegaScout(play, depth - 1, !colour, -beta, -alpha, true, info);
					} else {
						score = -NegaScout(play, depth - 1, !colour, -alpha - 1, -alpha, true, info); // search with a zero-window
						if (alpha < score && score < beta)
							score = -NegaScout(play, depth - 1, !colour, -beta, -score, true, info); // do a full re-search if failed high
					}

					if (score > alpha) {
						alpha = score;
						pvIndex = pvi;
					}
					if (alpha >= beta)
						break;
				}
			}

			// store bestIndex in PrincipalVariationHashtable
			if (!info.PrincipalVariationHashtable.TryGetValue(tempoDependantHash, out pv) || pv.pvDepth < depth)
				info.PrincipalVariationHashtable[board.ZobristId] = (pvIndex, depth);

			return alpha;
		}

		[MethodImpl(MethodImplOptions.AggressiveOptimization)]
		static int Queince(in BitBoard board, int depth, bool colour, int alpha, int beta, PersistentSearchInfo info) {
			info.SearchedPositionsCount++;
			if (info.Stopped)
				return 0;

			int eval = Evaluator.Evaluate(board, depth);
			eval = colour ? eval : -eval;
			if (eval >= beta)
				return beta;
			if (eval > alpha)
				alpha = eval;

			var (captures, len) = BitBoardUtils.GetAllCapturesIndecies(board, colour, out ChessPiece[] ids);
			for (int c = 0; c < len; c++) {
				int from = captures[c * 2];
				int to = captures[c * 2 + 1];

				var play = new BitBoard(board, from, to, ids[from]);
				int score = -Queince(play, depth - 1, !colour, -beta, -alpha, info);

				if (score >= beta)
					return beta;
				if (score > alpha)
					alpha = score;
			}

			return alpha;
		}


		[MethodImpl(MethodImplOptions.AggressiveOptimization)]
		static int NegaMax(in BitBoard board, int depth, bool colour, int alpha, int beta) {
			if (depth <= 0 || PopCount(board.Kings) != 2) {
				int eval = Evaluator.Evaluate(board, depth);
				return colour ? eval : -eval;
			}

			var (moves, len) = BitBoardUtils.GetAllMovesetIndecies(board, colour, out ChessPiece[] ids);
			int best = -int.MaxValue;
			for (int m = 0; m < len; m++) {
				int from = moves[m * 2];
				int to = moves[m * 2 + 1];

				int score = -NegaMax(new BitBoard(board, from, to, ids[from]), depth - 1, !colour, -beta, -alpha);
				if (score > best)
					best = score;
				if (best > alpha)
					alpha = best;
				if (alpha >= beta)
					break;
			}

			return best;
		}
	}
}

//int ispBest = -int.MaxValue;
//var indexScorePairs = new (int index, int score)[len];
//for (int i = 0; i < len; i++) {
//	ulong movepack = movelist[i];
//	int from = (int)(movepack >> 32);
//	int to = (int)(uint)movepack;

//	int score = -NegaMax(new BitBoard(in board, from, to, ids[from], ids[to]), 4, !colour, ispBest, int.MaxValue);
//	if (score > ispBest)
//		ispBest = score;
//	indexScorePairs[i] = (i, score);
//}

//{  // fast insertion sort algorithm
//	int i = 1;
//	while (i < indexScorePairs.Length) {
//		var pair = indexScorePairs[i];
//		int s = pair.score;
//		int j = i - 1;

//		while (j >= 0 && indexScorePairs[j].score > s) {
//			indexScorePairs[j + 1] = indexScorePairs[j];
//			j--;
//		}

//		indexScorePairs[j + 1] = pair;
//		i++;
//	}
//}
//int best = -int.MaxValue;
//for (int m = 0; m < len; m++) {
//	ulong movepack = movelist[indexScorePairs[m].index];
//	int from = (int)(movepack >> 32);
//	int to = (int)(uint)movepack;

//	int score = -NegaScout(new BitBoard(board, from, to, ids[from], ids[to]), PlySearchDepth - 1, !colour, best, int.MaxValue, pvht);
//	if (score > best) {
//		best = score;
//		bestMove = (from, to);
//	}
//}


//[MethodImpl(MethodImplOptions.AggressiveOptimization)]
//static int NegaMaxWithHashtable(in BitBoard board, int depth, int alpha, int beta, bool colour, 
//	Dictionary<ulong, (int score, HashtableFlag flag, int depth)> hashtable) {
//	if (depth == 0 || PopCount(board.Kings) != 2) {
//		int eval = Evaluator.Evaluate(board);
//		return colour ? eval : -eval;
//	}

//	int designatedAlpha = alpha;
//	ulong hash = board.ZobristId;
//	if (hashtable.TryGetValue(hash, out (int value, HashtableFlag flag, int depth) entry1)) {
//		// should be >=, but odd/even variance is fucking annoying
//		if (/*entry1.depth == depth*/ (entry1.depth & 1) != 1 && entry1.depth >= depth) {
//			switch (entry1.flag) {
//				case HashtableFlag.Exact:
//					return entry1.value;
//				case HashtableFlag.LowerBound:
//					if (alpha < entry1.value)
//						alpha = entry1.value;
//					break;
//				case HashtableFlag.UpperBound:
//					if (beta > entry1.value)
//						beta = entry1.value;
//					break;
//			}
//			if (alpha >= beta) {
//				return entry1.value;
//			}
//		}
//	}

//	var (movelist, len) = BitBoardUtils.GetAllMovesetIndecies(board, colour, out ChessPiece[] ids);
//	int best = -int.MaxValue;
//	for (int m = 0; m < len; m++) {
//		ulong movepack = movelist[m];
//		int from = (int)(uint)(movepack >> 32);
//		int to = (int)(uint)movepack;

//		int score = -NegaMax(new BitBoard(board, from, to, ids[from], ids[to]), 
//			depth + 1, -beta, -alpha, !colour, hashtable, finalDepth);
//		if (score > best)
//			best = score;
//		if (best > alpha)
//			alpha = best;
//		if (alpha >= beta)
//			break;
//	}

//	if (hashtable.TryGetValue(hash, out (int value, HashtableFlag flag, int depth) entry2)) {
//		if (best <= designatedAlpha) {
//			if (entry2.depth < depth)
//				hashtable[hash] = (best, HashtableFlag.UpperBound, depth);
//		} else if (best >= beta) {
//			if (entry2.depth < depth)
//				hashtable[hash] = (best, HashtableFlag.LowerBound, depth);
//		} else {
//			if (entry2.depth < depth)
//				hashtable[hash] = (best, HashtableFlag.Exact, depth);
//		}
//	} else {
//		if (best <= designatedAlpha) {
//			hashtable.Add(hash, (best, HashtableFlag.UpperBound, depth));
//		} else if (best >= beta) {
//			hashtable.Add(hash, (best, HashtableFlag.LowerBound, depth));
//		} else {
//			hashtable.Add(hash, (best, HashtableFlag.Exact, depth));
//		}
//	}

//	return best;
//}

//static int NegaMaxEval(in BitBoard board, bool colour) {
//	var (movelist, len) = BitBoardUtils.GetAllMovesetIndecies(board, colour, out ChessPiece[] ids);
//	int best = -int.MaxValue;
//	for (int m = 0; m < len; m++) {
//		ulong movepack = movelist[m];
//		int from = (int)(uint)(movepack >> 32);
//		int to = (int)(uint)movepack;

//		int score = -NegaMax(new BitBoard(board, from, to, taken: ids[from]), depth - 1, -beta, -alpha, !colour);
//		if (score > best)
//			best = score;
//		if (best > alpha)
//			alpha = best;
//		if (alpha >= beta)
//			break;
//	}
//}