// -------------------- LevelElement --------------------
using DungeonCrawler;

public abstract class LevelElement
{
    public int X { get; set; }
    public int Y { get; set; }
    public char Glyph { get; protected set; }
    public ConsoleColor Color { get; protected set; }

    public virtual void Draw()
    {
        Console.ForegroundColor = Color;
        Console.SetCursorPosition(X, Y);
        Console.Write(Glyph);
        Console.ResetColor();
    }
}

public sealed class Wall : LevelElement
{
    public Wall(int x, int y)
    {
        X = x; Y = y;
        Glyph = '#';
        Color = ConsoleColor.DarkGray;
    }
}

public sealed class HealthPotion : LevelElement
{
    public int HealAmount { get; } = 10;

    public HealthPotion(int x, int y)
    {
        X = x;
        Y = y;
        Glyph = 'K';
        Color = ConsoleColor.Magenta;
    }


    // -------------------- Actor base --------------------
    public abstract class Actor : LevelElement
    {
        public int HP { get; set; }
        public Dice AttackDice { get; protected set; } = null!;
        public Dice DefenceDice { get; protected set; } = null!;
        public string Name { get; protected set; } = string.Empty;
    }

    public abstract class Enemy : Actor
    {
        public abstract void Update(Game game);
    }

    public sealed class Rat : Enemy
    {
        public Rat(int x, int y, Random rng)
        {
            X = x; Y = y;
            Glyph = 'r';
            Color = ConsoleColor.Red;
            Name = "Rat";
            HP = 5;
            AttackDice = new Dice(1, 6, 3, rng);
            DefenceDice = new Dice(1, 6, 1, rng);
        }

        public override void Update(Game game)
        {
            var dirs = new (int dx, int dy)[] { (0, -1), (0, 1), (-1, 0), (1, 0) };
            var choice = game.Rng.Next(0, dirs.Length);
            var (dx, dy) = dirs[choice];
            game.TryMoveEnemy(this, X + dx, Y + dy);
        }
    }

    public sealed class Snake : Enemy
    {
        private Random rng; 

        public Snake(int x, int y, Random rng)
        {
            X = x;
            Y = y;
            Glyph = 's';
            Color = ConsoleColor.Green;
            Name = "Snake";
            HP = 5;

            this.rng = rng; 

            AttackDice = new Dice(3, 4, 2, rng);
            DefenceDice = new Dice(1, 8, 5, rng);
        }

        public override void Update(Game game)
        {
           
            if (rng.NextDouble() < 0.15)
                return;

            var p = game.Player;
            int dist2 = (p.X - X) * (p.X - X) + (p.Y - Y) * (p.Y - Y);
            if (dist2 > 2 * 2) return;

            var candidates = new (int dx, int dy)[] { (0, -1), (0, 1), (-1, 0), (1, 0) };
            int bestX = X, bestY = Y, best = dist2;

            foreach (var (dx, dy) in candidates)
            {
                int nx = X + dx;
                int ny = Y + dy;
                if (game.IsBlocked(nx, ny)) continue;

                int d2 = (p.X - nx) * (p.X - nx) + (p.Y - ny) * (p.Y - ny);
                if (d2 > best)
                {
                    best = d2;
                    bestX = nx;
                    bestY = ny;
                }
            }

            game.TryMoveEnemy(this, bestX, bestY);
        }
    }

    public sealed class Player : Actor
    {
        public Player(int x, int y, Random rng)
        {
            X = x; Y = y;
            Glyph = '@';
            Color = ConsoleColor.Yellow;
            Name = "Player";
            HP = 100;
            AttackDice = new Dice(2, 6, 2, rng);
            DefenceDice = new Dice(2, 6, 0, rng);
        }
    }
}