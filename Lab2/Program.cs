using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Media;
using System.Threading;


namespace DungeonCrawler
{ 
    
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
