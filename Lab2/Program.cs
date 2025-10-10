using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Media;
using System.Threading;


namespace DungeonCrawler
{
    // -------------------- Dice --------------------
    public sealed class Dice
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

    // -------------------- LevelElement --------------------
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
        public Snake(int x, int y, Random rng)
        {
            X = x; Y = y;
            Glyph = 's';
            Color = ConsoleColor.Green;
            Name = "Snake";
            HP = 10;
            AttackDice = new Dice(3, 4, 2, rng);
            DefenceDice = new Dice(1, 8, 5, rng);
        }

        public override void Update(Game game)
        {
            var p = game.Player;
            int dist2 = (p.X - X) * (p.X - X) + (p.Y - Y) * (p.Y - Y);
            if (dist2 > 2 * 2) return;

            var candidates = new (int dx, int dy)[] { (0, -1), (0, 1), (-1, 0), (1, 0) };
            int bestX = X, bestY = Y, best = dist2;
            foreach (var (dx, dy) in candidates)
            {
                int nx = X + dx, ny = Y + dy;
                if (game.IsBlocked(nx, ny)) continue;
                int d2 = (p.X - nx) * (p.X - nx) + (p.Y - ny) * (p.Y - ny);
                if (d2 > best)
                {
                    best = d2; bestX = nx; bestY = ny;
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

    // -------------------- LevelData --------------------
    public sealed class LevelData
    {
        private readonly List<LevelElement> _elements = new();
        public IReadOnlyList<LevelElement> Elements => _elements;
        public readonly List<Wall> Walls = new();
        public readonly List<Enemy> Enemies = new();
        public readonly List<HealthPotion> Potions = new();
        public (int x, int y) PlayerStart;
        public int Width { get; private set; }
        public int Height { get; private set; }

        public void Load(string filename, Random rng)
        {
            _elements.Clear(); Walls.Clear(); Enemies.Clear(); Potions.Clear();
            var lines = File.ReadAllLines(filename);
            Height = lines.Length;
            Width = lines.Length == 0 ? 0 : lines.Max(l => l.Length);

            for (int y = 0; y < lines.Length; y++)
            {
                var line = lines[y];
                for (int x = 0; x < line.Length; x++)
                {
                    char c = line[x];
                    switch (c)
                    {
                        case '#':
                            var w = new Wall(x, y);
                            Walls.Add(w); _elements.Add(w);
                            break;
                        case 'r':
                            var r = new Rat(x, y, rng);
                            Enemies.Add(r); _elements.Add(r);
                            break;
                        case 's':
                            var s = new Snake(x, y, rng);
                            Enemies.Add(s); _elements.Add(s);
                            break;
                        case '@':
                            PlayerStart = (x, y);
                            break;
                        case 'K':
                            var h = new HealthPotion(x, y);
                            Potions.Add(h);
                            _elements.Add(h);
                            break;
                    }
                }
            }
        }
    }

    // -------------------- Game --------------------
    
    public sealed class Game
    {
        public readonly Random Rng = new Random();
        public readonly LevelData Level = new LevelData();
        public Player Player { get; private set; } = null!;
        private readonly HashSet<(int x, int y)> _discoveredWalls = new();
        private const int VisionRadius = 5;
        private int _kills = 0;

        private SoundPlayer _musicPlayer;

        public void Run(string levelFile)
        {
            Console.CursorVisible = false;

            ShowTitleScreen();

            Level.Load(levelFile, Rng);
            Player = new Player(Level.PlayerStart.x, Level.PlayerStart.y, Rng);

            try
            {
                var musicPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Sounds", "background.wav");
                if (File.Exists(musicPath))
                {
                    _musicPlayer = new SoundPlayer(musicPath);
                    _musicPlayer.PlayLooping();
                }
            }
            catch { }

            while (true)
            {
                Render();

                if (Player.HP <= 0)
                {
                    MessageCenter($"Game over! Du dödade {_kills} fiender.");
                    break;
                }
                if (Level.Enemies.Count == 0)
                {
                    ShowWinningScreen();
                    break;
                }

                var key = Console.ReadKey(true).Key;
                if (key == ConsoleKey.Escape) break;

                int dx = 0, dy = 0;
                switch (key)
                {
                    case ConsoleKey.W:
                    case ConsoleKey.UpArrow: dy = -1; break;
                    case ConsoleKey.S:
                    case ConsoleKey.DownArrow: dy = 1; break;
                    case ConsoleKey.A:
                    case ConsoleKey.LeftArrow: dx = -1; break;
                    case ConsoleKey.D:
                    case ConsoleKey.RightArrow: dx = 1; break;
                }

                PlayerTurn(dx, dy);
                EnemiesTurn();
            }

            Console.SetCursorPosition(0, Level.Height + 2);
            Console.CursorVisible = true;

            _musicPlayer?.Stop();
            Console.SetCursorPosition(0, Level.Height + 2);
            Console.CursorVisible = true;
        }

        // -------------------------------------------------
        // Titelskärm med fade + blink
        // -------------------------------------------------
        private void ShowTitleScreen()
        {
            Console.Clear();
            Console.ForegroundColor = ConsoleColor.Yellow;

            string title =
"  _______________________________________________________\r\n" +
" |                                                       |\r\n" +
" |        ██████   ███████████████ ███████████           |\r\n" +
" |      ▒▒██████ ▒▒███▒▒███▒▒▒▒▒█▒█▒▒▒███▒▒▒█            |\r\n" +  
" |        ▒███▒███ ▒███ ▒███  █ ▒ ▒   ▒███  ▒            |\r\n" +
" |        ▒███▒▒███▒███ ▒██████       ▒███               |\r\n" +
" |        ▒███ ▒▒██████ ▒███▒▒█       ▒███               |\r\n" +
" |        ▒███  ▒▒█████ ▒███ ▒   █    ▒███               |\r\n" +
" |  ██    █████  ▒▒███████████████    █████              |\r\n" +
" | ▒▒    ▒▒▒▒▒    ▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒    ▒▒▒▒▒               |\r\n" +
" | ██████████  █████  ███████████   █████                |\r\n" +
" | ▒▒███▒▒▒▒███▒▒███  ▒▒███▒▒██████ ▒▒███                |\r\n" +
" |  ▒███   ▒▒███▒███   ▒███ ▒███▒███ ▒███                |\r\n" +
" |  ▒███    ▒███▒███   ▒███ ▒███▒▒███▒███ ██████████     |\r\n" +
" |  ▒███    ▒███▒███   ▒███ ▒███ ▒▒██████▒▒▒▒▒▒▒▒▒▒      |\r\n" +
" |  ▒███    ███ ▒███   ▒███ ▒███  ▒▒█████                |\r\n" +
" |  ██████████  ▒▒████████  █████  ▒▒█████               |\r\n" +
" | ▒▒▒▒▒▒▒▒▒▒    ▒▒▒▒▒▒▒▒  ▒▒▒▒▒    ▒▒▒▒▒                |\r\n" +
" |    █████████  ██████████    ███████   ██████   █████  |\r\n" +
" |   ███▒▒▒▒▒███▒▒███▒▒▒▒▒█  ███▒▒▒▒▒███▒▒██████ ▒▒███   |\r\n" +
" |  ███     ▒▒▒  ▒███  █ ▒  ███     ▒▒███▒███▒███ ▒███   |\r\n" +
" | ▒███          ▒██████   ▒███      ▒███▒███▒▒███▒███   |\r\n" +
" | ▒███    █████ ▒███▒▒█   ▒███      ▒███▒███ ▒▒██████   |\r\n" +
" | ▒▒███  ▒▒███  ▒███ ▒   █▒▒███     ███ ▒███  ▒▒█████   |\r\n" +
" |  ▒▒█████████  ██████████ ▒▒▒███████▒  █████  ▒▒█████  |\r\n" +
" |   ▒▒▒▒▒▒▒▒▒  ▒▒▒▒▒▒▒▒▒▒    ▒▒▒▒▒▒▒   ▒▒▒▒▒    ▒▒▒▒▒   |\r\n" +
" |_______________________________________________________|\r\n" +
"            |                                |\r\n" +
"            |        Desirée Skönneberg      |\r\n" +
"            |              NEU25G            |\r\n" +
"            |________________________________|\r\n" +
"            |                                |\r\n" +
"            |   Vakna Fredrik!!              |\r\n" +
"            |                                |\r\n" +
"            |   Du somnade i .NET Dungeon    |\r\n" +
"            |   när du höll på att koda!     |\r\n" +
"            |                                |\r\n" +
"            |   Nu är det fullt av råttor    |\r\n" +
"            |   och ormar!                   |\r\n" +
"            |                                |\r\n" +
"            |   Åh nej!                      |\r\n" +
"            |   Utplåna dem!                 |\r\n" +
"            |                                |\r\n" +
"            |   Glöm inte att dricka         |\r\n" +
"            |   kaffe! - K                   |\r\n" +
"            |                                |\r\n" +
"            |    W                           |\r\n" +
"            |   ASD - Kontrollerar spelaren  |\r\n" +
"            |                                |\r\n" +
"            |                                |\r\n" +
"            |                                |\r\n" +
"            |________________________________|";

            // Fade in rad för rad
            foreach (var line in title.Split('\n'))
            {
                Console.WriteLine(line);
                Thread.Sleep(25); // kort delay
            }

            // Blinkande "PRESS ANY KEY"
            int cursorLeft = 13;
            int cursorTop = Console.CursorTop - 3; // raden där texten är
            bool visible = true;

            while (!Console.KeyAvailable)
            {
                Console.SetCursorPosition(cursorLeft + 7, cursorTop);
                Console.ForegroundColor = visible ? ConsoleColor.Yellow : ConsoleColor.Black;
                Console.Write("> PRESS ANY KEY <");
                visible = !visible;
                Thread.Sleep(500);
            }

            Console.ReadKey(true);
            Console.ResetColor();
            Console.Clear();
        }

        private void PlayerTurn(int dx, int dy)
        {
            int nx = Player.X + dx;
            int ny = Player.Y + dy;
            if (!InBounds(nx, ny)) return;

            var enemy = Level.Enemies.FirstOrDefault(e => e.X == nx && e.Y == ny);
            if (enemy != null)
            {
                ResolveCombat(Player, enemy);
                if (enemy.HP > 0)
                    ResolveCombat(enemy, Player);
                if (enemy.HP <= 0)
                {
                    Level.Enemies.Remove(enemy);
                    _kills++;
                }
                return;
            }

            if (Level.Walls.Any(w => w.X == nx && w.Y == ny)) return;
            Player.X = nx; Player.Y = ny;
            
            if (Level.Potions.Any(p => p.X == nx && p.Y == ny))
            {
                var potion = Level.Potions.First(p => p.X == nx && p.Y == ny);
                Level.Potions.Remove(potion);
                Player.HP = Math.Min(100, Player.HP + potion.HealAmount);
                StatusLine($"Du dricker en kopp kaffe! +{potion.HealAmount} HP (Totalt: {Player.HP})");
            }
        }

        private void EnemiesTurn()
        {
            var list = Level.Enemies.ToList();
            foreach (var e in list)
            {
                if (e.HP <= 0) continue;
                e.Update(this);
            }
        }

        public bool TryMoveEnemy(Enemy e, int nx, int ny)
        {
            if (!InBounds(nx, ny)) return false;

            if (nx == Player.X && ny == Player.Y)
            {
                ResolveCombat(e, Player);
                if (Player.HP > 0)
                    ResolveCombat(Player, e);
                if (e.HP <= 0)
                {
                    Level.Enemies.Remove(e);
                    _kills++;
                }
                return false;
            }

            if (IsBlocked(nx, ny)) return false;
            e.X = nx; e.Y = ny;
            return true;
        }

        public bool IsBlocked(int x, int y)
        {
            if (!InBounds(x, y)) return true;
            if (Level.Walls.Any(w => w.X == x && w.Y == y)) return true;
            if (Level.Enemies.Any(en => en.X == x && en.Y == y)) return true;
            if (Player.X == x && Player.Y == y) return true;
            return false;
        }

        private void ResolveCombat(Actor attacker, Actor defender)
        {
            int atk = Math.Max(0, attacker.AttackDice.Throw());
            int def = Math.Max(0, defender.DefenceDice.Throw());
            int dmg = atk - def;
            if (dmg > 0) defender.HP -= dmg;
            StatusLine($"{attacker.Name} slår {defender.Name}: {atk} vs {def} => {(dmg > 0 ? $"{dmg} skada" : "blockerat")}. {defender.Name} HP: {Math.Max(0, defender.HP)}");
        }

        private void Render()
        {
            Console.SetCursorPosition(0, 0);
            int r2 = VisionRadius * VisionRadius;

            foreach (var w in Level.Walls)
            {
                int dx = w.X - Player.X;
                int dy = w.Y - Player.Y;
                if (dx * dx + dy * dy <= r2)
                    _discoveredWalls.Add((w.X, w.Y));
            }

            for (int y = 0; y < Level.Height; y++)
            {
                Console.SetCursorPosition(0, y);
                Console.Write(new string(' ', Level.Width));
            }

            foreach (var pos in _discoveredWalls)
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.SetCursorPosition(pos.x, pos.y);
                Console.Write('#');
                Console.ResetColor();
            }

            foreach (var e in Level.Enemies)
            {
                int dx = e.X - Player.X;
                int dy = e.Y - Player.Y;
                if (dx * dx + dy * dy <= r2)
                    e.Draw();
            }
            foreach (var p in Level.Potions)
            {
                int dx = p.X - Player.X;
                int dy = p.Y - Player.Y;
                if (dx * dx + dy * dy <= r2)
                    p.Draw();
            }

            Player.Draw();

            Console.SetCursorPosition(0, Level.Height);
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write($"HP: {Math.Max(0, Player.HP)}  | Attack {Player.AttackDice}  Defence {Player.DefenceDice}  | Kills: {_kills}   ");
            Console.ResetColor();
        }

        private bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Level.Width && y < Level.Height;

        private void StatusLine(string msg)
        {
            Console.SetCursorPosition(0, Level.Height + 1);
            Console.Write(new string(' ', Console.WindowWidth));
            Console.SetCursorPosition(0, Level.Height + 1);
            Console.Write(msg);
        }

        private void MessageCenter(string msg)
        {
            Console.Clear();

            string gameOverArt =
        @"
                        ::+XXXXXX$XXXXXXXXXXXxxxxxxxxx+++++;;;;;;;;;+++++++++;.:                    
                        ;++XXXXXXXXXXXXXXXXXxxxxxxxx++++++++;;;;;;;;;++++++++;;..                   
                       ;+xXXXXXXXXXXXXXXXXXXxxxxxxxxxx++++++;;;;;;;+++++++++++:+;.                  
                       ;+xXXXXXX$$XXXXXXXXXxxxxxxxxx++x++;+++;;;;;;;;+++++++++;;;:                  
                       +xXXXXXXXXXXXXXXXXXxxxxxx+xxx+;+++;;;;;;;;+;++++++++++++;::                  
                       +xxXXXXXXXXXXXXXxXXxxxxx+;xx++:+;;;;;;;;;;;+;++++++++++;+;:;                 
                      ;xxxXXXXXXXXXXXXXXXxxx++++;x++;:;;.;;;;;;;;;;;++++++++++;:+::                 
                       +xxXXXXXXXXXXXXxx::;;;::+;xx++:;:.:::..:;;;;;++++++++++;:;;.                 
                       x++XXXXXXXXX+;;..  .:;;;+xxxx+;::.....   ..:::;;++++++++:.;.                 
                       xx+XXXXXXx+;+;:  ..::;;++xXxx+;:....       .....:;++++++; ;:                 
                       +++XXXXX+x+::;;;:   ...;+xxXxx;:.        ::.:::.:.:;++++;::.                 
                       ++xXXXXXXXx++:;::...   :+xXxx++:.       ..::..;;;;;;++++;:.::                
                       ;+xXXXXXXxxXx..   .:  ;;:xxXx++;. ..  ..      :: ;;;+++++;;:.                
                       :+XXXXXXx+::xx... :::::+xxxXxx+;;..... ..    :  :; +;++++++..:;              
                     ;:+xXXXXXXXXxXxx+;:;;;;;xxxxxXxx++;:.:......::::;;;;;;+++++;;:.                
                     .;xxX$$XXXXXXXXxxx++;;+xxxXXXXx++;;:::::..:::;;;;++;+++++++;::                 
                     ;.x+X$$$XXXXXXXxxx++xxxxXxXXXXxx+;;::::;::::::;;+;++++++++;;:.:                
                      .X+X$$$$$XXXXXxxxxxxxXXXxXXXXxx++;:;::;;;;;:;;;++++++++;;+;:.:                
                      ;X;XXX$$$XXXXXxxxxxxxXXXXXxXXxx++;;::::;;;;;;;;;;++;+;;;;;:; .                
                      ;xxXXXX$$XXXXXXxxxxxxxXxXXXxxxx++++;:.::;;;;;;;;;;;+++;;;;:;:.                
                      ;:Xx$X$X$$XXXXXXxxxxx++xXXXXxx++++++;:.:::;;;;;;;+;;;;;;;;.+::                
                      ++XXX$$$$X$XXXXXxxx++;:XXXXxx+++++;;;;;.::;;;;;;+;;;;;;;;:.+::                
                       xX$x$$$$XXX$XXXXxx+;:XXxxxX+;;;;::::::...:;;;;;++;;++;;:.;;::                
                       xxxxX$$$X$$$$$XXx+;::;x:..++;:::    ..::..:;;;;++;+;;+:::+;:                 
                        xx$XX$$XXX$$$XXx;:+xx++;::::... . ..:::;.::;;+++;;++;:::;;:                 
                        xXXxXX$X$$$XXXx+;xxxx::.            .:;;;:::;+++++;;;:.;;;                  
                         $$X+xX$X$XXXx+;+Xxx;.. .             :;;;::;;+++;;:;:;;;:                  
                          $$;XXXXXXXxx+;Xx;:;:.         .  .   ..:;:;;+++;;:::::;:                  
                          X$+XxXXXXXx;;x++;:::;::...... . ...:..  .;.;;+++;:;;;;;                   
                           XXXXXXXXx+;+;;;;+;:.::::::.::.:... .:. ..::;;+;:+;;:;;                   
                           $$xXXXXX+;;+++;++++++;;:;;;:;;;:::::: :..:.;+;;;++:+:;                   
                            X$XXXXX+:+;;;xXXXx+;:.   :. :::::;;;;:....:;;;;;:;;::                   
                            xXxXxXx+:::+XXXx++;::..     .....::;;;:...:;:;;;::;                     
                             x$x+xX+;;xXXXxx+;:::....     . ..::;;::..::.::::::                     
                            X $X+;+++;+xx+++;:++;;;+:::::::...:::.:..:   .:;;:                      
                             xxXxx;;;+;;;;++++xx+:;;::::::::;;;.::: .:. :.::;;                      
                              xXxx+;+:++;;xxxxxx;:;:::::;:::;;+:;...:.;:::;;;                       
                              ++xx++x;;+x+xxXxx++;;;:::;:;;;:;;++:::..::::;+                        
                              ;.xXxx+++;+XXXXx++++;.;::++x:+;;;+;+:::;..;:::::  .                   
                          ;++;.++xxxX++xXXxX+xx+x+;;;.+:+;;;+;;;++;::::.:::.::::  ..                
                      ;.:.;:;: +++x++xxx$XXxXxx++xx:;;;++;+++++;++:::.:;:;..:::.      .             
                 :::;..:..::::.++++x+x+xXXXxXXXx;xX;;;++;++++;:+;;;::::::::..::.        ...         
        ::::::::::..:..:..::::.++++xxXxXXxXxx+XX+x+;;;;;;;+x;;:+;;:::;::::...:..              .     
     ;;..:::::::::. ...:...:::.;++++++x+Xxxxxxxx++x++;;+x;;;;;;;::;:.:::......:.           .     ...
 ;;;;;::...::...:..........:::..+++++++xxx+xxxxxxXx++;+;;;;:;::;:;:::.:......::.                   .
;;;;;:::.......:. .........:.::.++;++++x+++xx+x+x+xx++;;;+;;:;::::.:.:......::.                     
:::::.:.........  ....::......:..++;;++;;;;;+++;++;+;;;;;:;:;;.:::::........::.                     
::;:::.:........  ...::.....:::..+;;;;;+;;;;;+++;;++;;;;;::;:::..:::.......::.    ..                
:::::..:...... .  ......::.....:. ;;;;;;+++;;;+++++++;;:::::::::::..:....::::     .               . 
:::::...:..... . .......:...:::....;;;;;;;;;;;;;++++;;;;::::::.:::::....::::.  . ..              .. 
.::::..:::...  ........:.......... .:;;;;;;;;;;;;;;;;;::::::::.::::..::::::.  .          ..      ...
.:::::..::.. . ...............:.... .:;;;;;;;;;;;;;;;;;;:::::::::::.::::::. ... .         .      ...
..:::...:.:...  ..........::...:.... .:;;;;;:;;:;:;;;::::::::::::::::::::: ......        ..      ...
:.::::........ .....::........ .......:;;;;;;;;;;::::::::::::::::::::::::.......   .      ...   ....
:.:::::.......  ..........:....::......:;;;;;;;;;;::::::::::;;::::::::::.......         ....   .....
";

            Console.ForegroundColor = ConsoleColor.DarkRed;
            Console.WriteLine(gameOverArt);
            Console.ResetColor();

            // Centrera kill-raden under bilden
            int midX = Math.Max(0, (Console.WindowWidth / 2) - 15);
            Console.SetCursorPosition(midX, Console.CursorTop);
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"GAME OVER — Du dödade {_kills} fiender!");
            Console.ResetColor();

            Console.WriteLine("\nTryck [Enter] för att avsluta...");
            while (Console.ReadKey(true).Key != ConsoleKey.Enter)
            {
                
            }

        }

        private void ShowWinningScreen()
        {
            Console.Clear();

            string winArt =
        @"
                        .:x.++++++++++++++++;;::..........................:......                   
                        .x&&$&&$&&$&&$$&&$$$$$$$$$&$$$X$$XX$XXXXX$XXX$XXXXXXX$&&:.                  
                       +:X&&&&$$$&&&&$&$$$$&$&X$$x&$X$$$$XXXXxXxXXXXXXXXXXX$XX$+&:                  
                       .:&X&$&$$&$$$$$&X$$&$$$$$&;$XXXxXXXXXXX$$&$$$$$$$XXxXXx$&+x:                 
                       ::&&$&$$$&&&&&&&&&&&&&&$$$:&XXxxX$XXX&X&&$&&&&&&&&&$XXX$;&&.                 
                       ::&&&$$&&&&&&&&&&XxXXXxX&X+$Xxxxxx$xXx......::.++$X&&XX&$;&:                 
                       .$&.&&$&&&X....:....;&$$$&XX$$$$X+xXXX+X$....;:;.&;X&&XX$x&;:                
                      .;&$X&&&&&::;&x&&&&&&x&&&$$x&&&$$X&;XxxXX$&&&&&&&xx+;+&$X$x&x:                
                      :;$&+&&&;X;&&&&&&&&x++xx&&&&&&&X$$Xx+XX::;;;x&&&x$&&$XX&X$$X$:                
                      .&&X.&&&x&&&&;&+::x;&x+..&&&&X$XX$+$.+;X.$&$&x...X&.&&&$XX&.&:                
                      .&$;$&$&&&&::.&.$....&..$.&&$$&$$&&&:&...&......&..x.&$X$$&.&::               
                      .&+$&&$&&+..&&&X&&&&&&&&$&&&$&&$X$$&.&&&x&&&&&&&&&&..X$X$X&:+&.               
                      :x+&&&$&.;&&XX&&&$xx..;.X&$$$$&&;XX&;xXX$+....+..x&&&&X$X$$&.&.               
                      ::&:&&$&&&&&&x+xx+X&&&&&&&$$&$&&$$X$XXxx$&&&&&$&&&XX+$$$XX$&X&:               
                      ;:&:&&&$&&&$&&&&$&&+&&&&$$$$$$$&$$xXXXxXXxXXXX&$$$$XX&$X$$X$x&:.              
                     .:&&X&$&$&&$&&&X$$$&&&$$$$$&&$$$&X$$X$&;xXxXxxX$XXX$$XXX&$$X&;&.:              
                     ..&&$&&&&&&&$&&$&$$$x$&&&&&X&&&$&$XX$X$&.XXXXx$$$$$$$$$X$X$X&:&::              
                     $.&&&&&&&$$$$&&$&X$$&&&$:&&&&$$&$X$X$XX&&.xXXxXXX$X$$$XX$X$$$;&.:              
                     x.&+&&$&&&&&$$&$$X$&&&.;&&&&&&&XxXX$X$$$&&x+$xXxXXXXXXXXx$$XXXxx.              
                     :.&X&&&$&&$$&$&X$$$$x;.&&&$..+&&$X$$$$xx&$++xxXXxxXXXXXXXXX&;&X:.              
                     ::X&$X&&&&&&&$X$&XXX..&&$&&&&&&&&&&X.;;:;xX$$.x$xxXXXX$x$$$X+X&::              
                      ++&&x&&&&&&$XX&xxx.x&&&&&&&$+:....:;+$&&$XX&&.x$$XXx$XX$&XXxx&.               
                      :+&$$+&&&$&&X&&&x.&&&&X&&x.+:;.::x+;x;.;&&&$&&$.$$XxXX$$$xX:&&:               
                       :&&$$&&&&$&&&&&.&&+&$X&.++X:x.::;+++....+:&X&&&.&$$$XX$xx&;&&.               
                       .:&;&X&$&&&&&x.&&&$.&+x..x;..:&;++x:&x.$x+:.x;&&x&$$xx$$+&;&&.               
                        ;&x&$&X&&&&&..&&++X&&&&.&....++:.......xx$++$.&.&$Xxx&$$&x&.                
                        :&&$x&$X&$&&.&&$Xx&x.X&;$&+&&&.&&&:&XX&::+::::&.&$+$x$XxX&$.                
                         .&&&x&&$X&&.&&;X$..X&&&&&X&&&X&&&&&&&&&&&..&::;&&:&X$X&&:X:                
                          +&Xx$&X&&&$&.+;x..&&&&.&X&&&.&X&...+&&..x&&.$&&$;&+X:&X+x                 
                          :xX$&&X$X&$+&;;&&...:...............:..&&&&&.&&x+$&&&+&x:                 
                           :&xx&&+&&&x&X::&&&.:.;++++.:::..::...&$&X$&..&X$&$;xXx.                  
                            &&x&&$&+$&;:x;&&&&&......;:.xx+x:.&&X&XX$&..&x&&$;&$:.                  
                            .&&&$&;$$:.xx&&&&&&&&.+x&;+.:+;.x&$x+xXX$$$.$;$$.X+$.                   
                             ;&&$Xx&;&&$;&$$&&xx&&&&&&&&&&&&$XxxXXXX$xxxX.;+&+$x.                   
                              :&&.$&X$xx;&&$&&&&$X&$$XXxXxXx&xxXXXxX+Xx&&.+x&&xX                    
                               .&&$$:$&$.&&$$&&&&$xXX+XxXx&xxx$Xxx$XX+Xx+X+&x$$:                    
                                x&xX&&;&$+&$$$$&Xx+XXXXX++:xx.&XXXXxx&X&;&&x$X.                     
                                .&:&&Xx$$x+&&$$&:+x+$+X$&x+x;.xXX$xx$.xXXX.XX$;                     
                                X&&+&;&x+&$x&$$&..X.xx+;$:...&&Xxx+x+;&.;;x&x&:                     
                                :&+&&.x:X$X+X$&X&X&:.;...;&+&&$xX;$x;$.x:&X+x&:                     
                             :.:&&&xX&&.&x&x&&&$&X&&&&&&X&&&$xXX&X+$&X;$.$&+$x:.:                   
                          ::+X.&$&&Xx+X&x&&&$X&&&$X$$&;&&&XX$x&x$X&X$;$x&.$xX$&&::::                
                      ..:.&xX&.&X+&+;&.&&&&.&&+&x&$&+X$$+X:&$;+&$x&$x&$:&XxX&:xX&.X::..             
             .:.::::&.+xx:&xx&.&xX&&Xx&;+X$+;&x$&XX$+xXx&x&X;:.$X&.&+x;&.&$&&:+x$x.xxx..:::.:.      
      ...:::+Xx$XX..&:+xX.&+x&.&&Xx&$.&&&$&&$&&&$Xx&.:Xx&$Xx:&x$$&XXx&x&+$X&:+xX$x.$X+$++xxxx;:::   
  ..:;.&$&&x&:X$$x+.&.+$$.&xX$:$&XX&&;&$$X;$&$X;$xx&X.+X.;&X;&+$&&$&+&+$++$&;x+X$X:XX;x+x+XX+++x.::.
:;XXx$&.;;+xx+XX+xxxx+XXX:X&+$x.&$xX&&x&X&X&$&x&$&+&&&$&&&x$&Xx$$x&&;&.:x$X;+xxx$.++xxx+++xx+++xx+x.
.xX$$$&$.xx;;x;+X+x+++Xxxx:&;X$.&&X$XxX&X&&$&$X&+&;&;X++&&$+&$X&&.&$:&;Xx;xXXxxX&.xX;&++xxx+++Xxxxx.
.x+xxXXxX+++++++xxXXx+Xxxx:$XX$:.&$$X$X&&$&&x&&;$&&&;$X&+&xx$X&X$&;&&.;x+XxxXxX$x+++:x++x+++x+xXx++.
.$Xxxx$X+;x$X$Xxxxxx+xxxxX+&Xx$&.&&XXxXX&$X&&X&x&x&;++$x&$&xX$XX+&+&X+x$XXx+xx$$.++xX+++x+++++xxx++.
.xXxXX+X$++xXXxxxxx++XxXxXxx$;X&$.&&XXx+&&:x&&&;&$&+&$;x&;&$&x$X&X&&&+x++xxxxX$.x+x+xxxxxx++;+xx+xx.
.XXxX+$XX+;x+xxx++xXx+xxxxx+xxXX&.;&$XXxxx;&$$&$$&&&&&;$xX&XxxX&&Xx+;+xxxxxxx$$.Xxxx+x+x+xxx;xXX+xx.
.XxxXx+X&$x:xxxx+xxxxxxxx+x;$&xX&&.&&XXXXxxX;+xX$:&.&&$&&+&X$&$.X$&++xxxXxxX$$.xX+;x++++++xx++xX+x+.
.X&+Xx+&$Xxxx++xxxxx+Xxxx;x$x.xx&&..XXXXxxXxxxxx&:+X:+;:+:++;Xx;$:X+xxxxxxX$&.XXx+&;xxxx+++xxxxX+xx.
..................:........:...................................:..............................:.....
";

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine(winArt);
            Console.ResetColor();

            int midX = Math.Max(0, (Console.WindowWidth / 2) - 15);
            Console.SetCursorPosition(midX, Console.CursorTop);
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"DU VANN! Du rensade dungeonen med {_kills} dödade fiender!");
            Console.ResetColor();

            Console.WriteLine("\nTryck [Enter] för att avsluta...");
            while (Console.ReadKey(true).Key != ConsoleKey.Enter)
            {
                
            }
        }

        // -------------------- Program --------------------
        public static class Program
        {
            public static void Main(string[] args)
            {
                string levelPath = @".\\Levels\\Level1.txt";
                if (!File.Exists(levelPath))
                {
                    Console.WriteLine($"Hittar inte {levelPath}. Lägg filen bredvid .exe eller skicka sökväg som första argument.");
                    return;
                }

                var game = new Game();
                game.Run(levelPath);
            }
        }
    }
}