
    // -------------------- Dice --------------------
    public class Dice
    {
        private readonly int _num;
        private readonly int _sides;
        private readonly int _mod;
        private readonly Random _rng;

        public Dice(int numberOfDice, int sidesPerDice, int modifier, Random rng)
        {
            _num = Math.Max(0, numberOfDice);
            _sides = Math.Max(1, sidesPerDice);
            _mod = modifier;
            _rng = rng;
        }

        public int Throw()
        {
            int sum = 0;
            for (int i = 0; i < _num; i++)
            {
                sum += _rng.Next(1, _sides + 1);
            }
            return sum + _mod;
        }

        public override string ToString() => $"{_num}d{_sides}{(_mod >= 0 ? "+" : "")}{_mod}";
    }
