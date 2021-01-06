using System.Runtime.CompilerServices;
using static System.Numerics.BitOperations;
using InfraredEngine.Chessboard;
using static InfraredEngine.Engine.InfraredBitOps;

namespace InfraredEngine.Engine {
   public static class Evaluator {
      // https://github.com/official-stockfish/Stockfish/blob/master/src/types.h
      public const uint
         // values ripped from Stockfish's code
         PawnValueOp = 124, PawnValueEg = 206,
         KnightValueOp = 781, KnightValueEg = 854,
         BishopValueOp = 825, BishopValueEg = 915,
         RookValueOp = 1276, RookValueEg = 1380,
         QueenValueOp = 2538, QueenValueEg = 2682,
         MidgameLimit = 15258, EndgameLimit = 3915;

      // https://en.wikipedia.org/wiki/Chess_piece_relative_value#Hans_Berliner's_system
      public readonly static uint[] WhitePawns_OP_PSE_Berliner = new uint[64] {
         0, 0, 0, 0, 0, 0, 0, 0,
         // edited early values (100, 130) to get the ai to jump their center pawns
         112, 118, 130, 100, 100, 130, 118, 112,
         112, 118, 130, 130, 130, 130, 118, 112,
         112, 118, 136, 148, 148, 136, 118, 112,
         120, 128, 145, 156, 156, 145, 128, 120,
         132, 138, 155, 174, 174, 155, 138, 132,
         132, 138, 155, 174, 174, 155, 138, 132,
         0, 0, 0, 0, 0, 0, 0, 0,
      };
      public readonly static uint[] BlackPawns_OP_PSE_Berliner = new uint[64] {
         0, 0, 0, 0, 0, 0, 0, 0,
         132, 138, 155, 174, 174, 155, 138, 132,
         132, 138, 155, 174, 174, 155, 138, 132,
         120, 128, 145, 156, 156, 145, 128, 120,
         112, 118, 136, 148, 148, 136, 118, 112,
         // edited early values (100, 130) to get the ai to jump their center pawns
         112, 118, 130, 130, 130, 130, 118, 112,
         112, 118, 130, 100, 100, 130, 118, 112,
         0, 0, 0, 0, 0, 0, 0, 0,
      };
      public readonly static uint[] WhitePawns_EG_PSE_Berliner = new uint[64] {
         0, 0, 0, 0, 0, 0, 0, 0,
         // outer files are scaled back by .05 after beliner scaling for reasonability
         236, 206, 196, 186, 186, 196, 206, 236,
         236, 206, 196, 186, 186, 196, 206, 236,
         248, 216, 206, 186, 186, 206, 216, 248,
         262, 231, 218, 206, 206, 218, 231, 262,
         288, 256, 238, 216, 216, 238, 256, 288,
         288, 256, 238, 216, 216, 238, 256, 288,
         0, 0, 0, 0, 0, 0, 0, 0,
      };
      public readonly static uint[] BlackPawns_EG_PSE_Berliner = new uint[64] {
         0, 0, 0, 0, 0, 0, 0, 0,
         // outer files are scaled back by .05 after beliner scaling for reasonability
         288, 256, 238, 216, 216, 238, 256, 288,
         288, 256, 238, 216, 216, 238, 256, 288,
         262, 231, 218, 206, 206, 218, 231, 262,
         248, 216, 206, 186, 186, 206, 216, 248,
         236, 206, 196, 186, 186, 196, 206, 236,
         236, 206, 196, 186, 186, 196, 206, 236,
         0, 0, 0, 0, 0, 0, 0, 0,
      };

      // my own made up garbage
      public const int
         ConnectedBonusOp = 20, ConnectedBonusEg = 45,
         PassedBonusOp = 30, PassedBonusEg = 75,
         DoubledPenaltyOp = 45, DoubledPenaltyEg = 95,
         CheckmateValue = 1000000;

      [MethodImpl(MethodImplOptions.AggressiveOptimization)]
      public static int Evaluate(in BitBoard board, int depth = 0) {
         // ensure king presence
         if ((board.Kings & board.WhitePieces) == 0) {
            return -CheckmateValue - depth;
         } else if ((board.Kings & board.BlackPieces) == 0) {
            return CheckmateValue + depth;
         }

         uint white = 0, black = 0;
         uint majorMinorCulmunativeMaterial = PopCountU32(board.Knights) * KnightValueOp + PopCountU32(board.Bishops) * BishopValueOp + PopCountU32(board.Rooks) * RookValueOp + PopCountU32(board.Queens) * QueenValueOp;

         bool isOpening = majorMinorCulmunativeMaterial > MidgameLimit;
         bool isEndgame = majorMinorCulmunativeMaterial <= EndgameLimit;
         uint valueLerp256 = isOpening ? 0u : isEndgame ? 1u : ((majorMinorCulmunativeMaterial - EndgameLimit) * 256u / MidgameLimit);

         uint knightValue = FastLerpR256(KnightValueOp, KnightValueEg, valueLerp256);
         uint bishopValue = FastLerpR256(BishopValueOp, BishopValueEg, valueLerp256);
         uint rookValue = FastLerpR256(RookValueOp, RookValueEg, valueLerp256);
         uint queenValue = FastLerpR256(QueenValueOp, QueenValueEg, valueLerp256);

         uint openness = PopCountU32(board.AllPieces & 0x00003C3C3C3C0000ul); // 0 is very open, 16 is very closed, 7 is likely a good divider
         uint openPosBonus = openness < 7u ? openness < 5u ? 45u : 25u : 0u;
         uint closedPosBonus = openness > 7u ? openness > 9u ? 45u : 25u : 0u;

         #region Minor & Major Piece Calcs

         // knights (value + closed bonus + centered bonus)
         ulong whiteKnights = board.Knights & board.WhitePieces;
         ulong blackKnights = board.Knights & board.BlackPieces;
         white += PopCountU32(whiteKnights) * (knightValue + closedPosBonus) - PopCountU32(whiteKnights & 0xFFFFC3C3C3C3FFFFul) * 15;
         black += PopCountU32(blackKnights) * (knightValue + closedPosBonus) - PopCountU32(blackKnights & 0xFFFFC3C3C3C3FFFFul) * 15;

         // bishops (value + open bonus + pair bonus)
         ulong whiteBishops = board.Bishops & board.WhitePieces;
         ulong blackBishops = board.Bishops & board.BlackPieces;
         uint whiteBishopCount = PopCountU32(whiteBishops);
         uint blackBishopCount = PopCountU32(blackBishops);
         white += whiteBishopCount * (bishopValue + openPosBonus) + ((whiteBishopCount >= 2) ? 65u : 0);
         black += blackBishopCount * (bishopValue + openPosBonus) + ((blackBishopCount >= 2) ? 65u : 0);
         // adjacent diagonals bonus (casting allows negatives, then restrains based on magnitude)
         if (whiteBishopCount == 2) {
            int b1 = TrailingZeroCount(whiteBishops), b2 = TrailingZeroCount(ResetLsb(whiteBishops));
            if ((b1 >> 3) - (b1 & 7) - ((b2 >> 3) - (b2 & 7)) == 1 || 7 - (b1 >> 3) - (b1 & 7) - (7 - (b2 >> 3) - (b2 & 7)) == 1)
               white += 25;
         }
         if (blackBishopCount == 2) {
            int b1 = TrailingZeroCount(blackBishops), b2 = TrailingZeroCount(ResetLsb(blackBishops));
            if ((b1 >> 3) - (b1 & 7) - ((b2 >> 3) - (b2 & 7)) == 1 || 7 - (b1 >> 3) - (b1 & 7) - (7 - (b2 >> 3) - (b2 & 7)) == 1)
               black += 25;
         }

         // rooks (value + open bonus + center files bonus)
         white += PopCountU32(board.Rooks & board.WhitePieces) * (rookValue + openPosBonus) - PopCountU32(board.Rooks & board.WhitePieces & 0xC3C3C3C3C3C3C3C3ul) * 30;
         black += PopCountU32(board.Rooks & board.BlackPieces) * (rookValue + openPosBonus) - PopCountU32(board.Rooks & board.BlackPieces & 0xC3C3C3C3C3C3C3C3ul) * 30;

         // queens (value + open bonus)
         white += PopCountU32(board.Queens & board.WhitePieces) * (queenValue + openPosBonus);
         black += PopCountU32(board.Queens & board.BlackPieces) * (queenValue + openPosBonus);

         #endregion

         #region Pawn Calcs

         // ------------ pawn structure ------------- //

         ulong whitePawns = board.WhitePieces & board.Pawns;
         ulong blackPawns = board.BlackPieces & board.Pawns;
         uint doubledWhitePawnCounter = 0;
         uint doubledBlackPawnCounter = 0;
         uint passedWhitePawnCounter = 0;
         uint passedBlackPawnCounter = 0;
         uint connectedWhitePawnCounter = 0;
         uint connectedBlackPawnCounter = 0;

         ulong wpawns = whitePawns;
         while (wpawns != 0) {
            // shl/shr c# ops require int
            int index = TrailingZeroCount(wpawns);

            switch (index & 7) {
               case 0:
                  if ((blackPawns & (0x303030303030100ul << index)) == 0)
                     passedWhitePawnCounter++;
                  if ((whitePawns & (0x20202ul << (index - 8))) != 0)
                     connectedWhitePawnCounter++;
                  break;
               case 7:
                  if ((blackPawns & (0xC0C0C0C0C0C08000ul << (index))) == 0)
                     passedWhitePawnCounter++;
                  if ((whitePawns & (0x404040ul << (index - 8))) != 0)
                     connectedWhitePawnCounter++;
                  break;
               default:
                  if ((blackPawns & (0x707070707070200ul << (index - 1))) == 0)
                     passedWhitePawnCounter++;
                  if ((whitePawns & (0x50505ul << (index - 9))) != 0)
                     connectedWhitePawnCounter++;
                  break;
            }

            // doubled penalty
            if ((whitePawns & (0x0101010101010100ul << index)) != 0)
               doubledWhitePawnCounter++;

            // pawn value
            white += FastLerpR256(WhitePawns_OP_PSE_Berliner[index], WhitePawns_EG_PSE_Berliner[index], valueLerp256);

            wpawns = ResetLsb(wpawns);
         }

         ulong bpawns = blackPawns;
         while (bpawns != 0) {
            int index = TrailingZeroCount(bpawns);
            int rIndex = 63 - index;

            switch (index & 7) {
               case 0:
                  if ((whitePawns & (0x0001030303030303ul >> (rIndex - 7))) == 0)
                     passedBlackPawnCounter++;
                  if ((blackPawns & (0x20202ul << (index - 8))) != 0)
                     connectedBlackPawnCounter++;
                  break;
               case 7:
                  if ((whitePawns & (0x0080C0C0C0C0C0C0ul >> (rIndex))) == 0)
                     passedBlackPawnCounter++;
                  if ((blackPawns & (0x404040ul << (index - 8))) != 0)
                     connectedBlackPawnCounter++;
                  break;
               default:
                  if ((whitePawns & (0x0040E0E0E0E0E0E0ul >> (rIndex - 1))) == 0)
                     passedBlackPawnCounter++;
                  if ((blackPawns & (0x50505ul << (index - 9))) != 0)
                     connectedBlackPawnCounter++;
                  break;
            }

            // doubled penalty
            if ((blackPawns & (0x0080808080808080ul >> rIndex)) != 0)
               doubledBlackPawnCounter++;

            // pawn value
            black += FastLerpR256(BlackPawns_OP_PSE_Berliner[index], BlackPawns_EG_PSE_Berliner[index], valueLerp256);

            bpawns = ResetLsb(bpawns);
         }

         uint doubledPawnValue = FastLerpR256(DoubledPenaltyOp, DoubledPenaltyEg, valueLerp256);
         uint passedPawnValue = FastLerpR256(PassedBonusOp, PassedBonusEg, valueLerp256);
         uint connectedPawnValue = FastLerpR256(ConnectedBonusOp, ConnectedBonusEg, valueLerp256);
         white += doubledPawnValue * doubledBlackPawnCounter;
         black += doubledPawnValue * doubledWhitePawnCounter;
         white += passedPawnValue * passedWhitePawnCounter;
         black += passedPawnValue * passedBlackPawnCounter;
         white += connectedPawnValue * connectedWhitePawnCounter;
         black += connectedPawnValue * connectedBlackPawnCounter;

         #endregion

         #region King Calcs

         // ------------ king safety ------------- //

         ulong whiteKingRing = BitboardMoveTables.KingMoves[TrailingZeroCountU32(board.Kings & board.WhitePieces)];
         ulong blackKingRing = BitboardMoveTables.KingMoves[TrailingZeroCountU32(board.Kings & board.BlackPieces)];

         white += PopCountU32(blackKingRing & board.WhiteCoveredTiles) * 15;
         black += PopCountU32(whiteKingRing & board.BlackCoveredTiles) * 15;
         if (!isOpening) {
            // do not calculate in the opening, at it disincentivises early center control
            white += PopCountU32(whiteKingRing & whitePawns) * 20;
            black += PopCountU32(blackKingRing & blackPawns) * 20;
         }
         if (!isEndgame) {
            white += (board.Kings & board.WhitePieces & 0xC3C7ul) != 0 ? 70u : 0u;
            black += (board.Kings & board.BlackPieces & 0xC7C3000000000000ul) != 0 ? 70u : 0u;
         }

         #endregion

         // ------------- coverage --------------- //
         white += PopCountU32(board.WhiteCoveredTiles) * 3;
         black += PopCountU32(board.BlackCoveredTiles) * 3;
         white += PopCountU32(board.WhiteCoveredTiles & board.AllPieces) * 4;
         black += PopCountU32(board.BlackCoveredTiles & board.AllPieces) * 4;

         //System.Console.WriteLine($"w:{white}, b:{black}, vlerp:{valueLerp256}, " +
         //   $"ppawns:{(int)passedWhitePawnCounter - (int)passedBlackPawnCounter} " +
         //   $"cpawns:{(int)connectedWhitePawnCounter - (int)connectedBlackPawnCounter} " +
         //   $"dpawns:{(int)doubledWhitePawnCounter - (int)doubledBlackPawnCounter} ");

         return (int)white - (int)black;
      }

      /// <summary>Extremely fast <see cref="uint"/> lerp with a resolution of 256.</summary>
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      static uint FastLerpR256(uint v1, uint v2, uint alpha256) {
         return v1 + (((v2 - v1) * alpha256) >> 8);
      }

      /// <summary>
      /// Use this to calculate centipawn-based scores
      /// </summary>
      public static uint GetCurrentPawnValue(in BitBoard board) {
         uint majorMinorCulmunativeMaterial
            = PopCountU32(board.Knights) * KnightValueOp
            + PopCountU32(board.Bishops) * BishopValueOp
            + PopCountU32(board.Rooks) * RookValueOp
            + PopCountU32(board.Queens) * QueenValueOp;
         return FastLerpR256(PawnValueOp, PawnValueEg,
            majorMinorCulmunativeMaterial > MidgameLimit ? 0u :
            majorMinorCulmunativeMaterial <= EndgameLimit ? 1u :
            ((majorMinorCulmunativeMaterial - EndgameLimit) * 256 / MidgameLimit));
      }

      /// <summary>
      /// Material evaluation calculation.
      /// </summary>
      public static int BasicEval(in BitBoard bb) {
         return (PopCount(bb.Pawns & bb.WhitePieces) - PopCount(bb.Pawns & bb.BlackPieces)) * (int)PawnValueOp
            + (PopCount(bb.Knights & bb.WhitePieces) - PopCount(bb.Knights & bb.BlackPieces)) * (int)KnightValueOp
            + (PopCount(bb.Bishops & bb.WhitePieces) - PopCount(bb.Bishops & bb.BlackPieces)) * (int)BishopValueOp
            + (PopCount(bb.Rooks & bb.WhitePieces) - PopCount(bb.Rooks & bb.BlackPieces)) * (int)RookValueOp
            + (PopCount(bb.Queens & bb.WhitePieces) - PopCount(bb.Queens & bb.BlackPieces)) * (int)QueenValueOp
            + (PopCount(bb.Kings & bb.WhitePieces) - PopCount(bb.Kings & bb.BlackPieces)) * (int)CheckmateValue;
      }
   }
}
