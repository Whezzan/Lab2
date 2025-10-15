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
                    Console.WriteLine($"IG!! Programmet hittar inte {levelPath}, jag är fan sämst.");
                    return;
                }

                var game = new Game();
                game.Run(levelPath);
            }
        }
    }
