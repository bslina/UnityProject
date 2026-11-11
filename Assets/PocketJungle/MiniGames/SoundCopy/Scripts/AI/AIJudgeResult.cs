namespace SoundCopy.AI
{
    public sealed class AIJudgeResult
    {
        public float Player1Score;
        public float Player2Score;
        public int Winner;
        public bool Player1TooQuiet;
        public bool Player2TooQuiet;
        public string Error;
        public bool Success => string.IsNullOrEmpty(Error);
    }
}
