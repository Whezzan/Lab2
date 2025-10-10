// -------------------- LevelData --------------------
using static HealthPotion;

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