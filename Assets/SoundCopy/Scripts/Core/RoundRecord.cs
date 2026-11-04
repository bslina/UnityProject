using System;

namespace SoundCopy
{
    // What happened in one round, for the verdict and the final results screen.
    [Serializable]
    public sealed class RoundRecord
    {
        public int roundNumber;
        public string clipId;
        public float player1Percent;
        public float player2Percent;
        public bool player1TooQuiet;
        public bool player2TooQuiet;
        // 0 = tie, 1 or 2 = round winner.
        public int winner;
        public bool player1Joker;
        public bool player2Joker;
        // Points given to the winner this round (1, or 2 with a joker).
        public int points;
    }
}
