using System;
using System.Collections.Generic;

namespace InfraredEngine {
	public class PersistentSearchInfo {
		public int MaxIdfDepth = 50;

		public System.Timers.Timer? Timer = null;
		public TimeSpan SearchDuration = TimeSpan.Zero;
		public int SearchedPositionsCount = 0;
		public bool Stopped = false;
		
		public (int from, int to) PrincipalVariation = (-1, -1);
		public int? ForcedMate = null;
		public int Evaluation = int.MinValue;

		internal Dictionary<ulong, (int pvMoveIndex, int depth)> PrincipalVariationHashtable = new ();
		internal Dictionary<ulong, (int eval, int depth)> EvaluationHashtable = new ();

		public void Reset() {
			MaxIdfDepth = 50;
			Timer = null;
			SearchDuration = TimeSpan.Zero;
			SearchedPositionsCount = 0;
			Stopped = false;

			PrincipalVariation = (-1, -1);
			ForcedMate = null;
			Evaluation = int.MinValue;

			PrincipalVariationHashtable = new();
			EvaluationHashtable = new();
		}
	}
}
