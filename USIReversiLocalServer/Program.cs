//#define DEVELOP

using System;
using System.Text.Json;

using USIReversiLocalServer;

namespace USIReversiLocalServer
{
    static class Program
    {
        public static void Main(string[] args)
        {
#if DEVELOP
            DevTest();
#else
            if (!ParseCommand(args, out GameConfig? config, out EngineConfig[] engineConfigs, out int gameNum))
                return;
            if (config is null)
                return;

            var game = new Game(config);
            game.Engine0 = engineConfigs[0];
            game.Engine1 = engineConfigs[1];
            game.Start(gameNum);
#endif
        }

        static void DevTest()
        {
            var engineConfig = new EngineConfig("", "", "",  new string[0], 1000);
            engineConfig.Save("engine0.json");
        }

        static bool ParseCommand(string[] args, out GameConfig? gameConfig, out EngineConfig[] engineConfigs, out int gameNum)
        {
            gameConfig = null;
            engineConfigs = new EngineConfig[2];
            gameNum = 0;
            for(var i = 0; i < args.Length; i++)
            {
                if (args[i][0..2] != "--")
                {
                    Console.Error.WriteLine($"Error: \"{args[i]}\" is invalid string. Put \"--\" before option's name.\ne.g. --gameNum 10");
                    return false;
                }

                switch (args[i][2..].ToLower())
                {
                    case "gamenum":
                        if (!int.TryParse(args[++i], out gameNum))
                        {
                            Console.Error.WriteLine($"Error: Cannot parse \"{args[i]}\" as a integer.");
                            return false;
                        }
                        break;

                    case "gameconfig":
                        {
                            var path = args[++i];
                            var config = GameConfig.Load(path);
                            if (config is null)
                            {
                                Console.Error.WriteLine($"Error: Cannot parse \"{path}\" as game config.");
                                return false;
                            }
                            gameConfig = config;
                        }
                        break;

                    case "engineconfig0":
                        {
                            var path = args[++i];
                            var config = EngineConfig.Load(path);
                            if (config is null)
                            {
                                Console.Error.WriteLine($"Error: Cannot parse \"{path}\" as engine config.");
                                return false;
                            }
                            engineConfigs[0] = config;
                        }
                        break;

                    case "engineconfig1":
                        {
                            var path = args[++i];
                            var config = EngineConfig.Load(path);
                            if (config is null)
                            {
                                Console.Error.WriteLine($"Error: Cannot parse \"{path}\" as engine config.");
                                return false;
                            }
                            engineConfigs[1] = config;
                        }
                        break;
                }
            }
            return true;
        }

        static T? LoadJson<T>(string path) where T:class?
        { 
            if (!File.Exists(path))
            {
                Console.Error.WriteLine($"Error: \"{path}\" does not exist.");
                return null;
            }

            if (File.GetAttributes(path).HasFlag(FileAttributes.Directory))
            {
                Console.Error.WriteLine($"Error: \"{path}\" is a directory.");
                return null;
            }

            try
            {
                return JsonSerializer.Deserialize<T>(File.ReadAllText(path));
            }
            catch (IOException ex)
            {
                Console.Error.WriteLine($"Error: Something was wrong with I/O when reading \"{path}\".");
                Console.Error.WriteLine($"Exception Message: \"{ex.Message}\"");
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                Console.Error.WriteLine($"Error: \"path\": Permission denied.");
                return null;
            }
        }
    }
}