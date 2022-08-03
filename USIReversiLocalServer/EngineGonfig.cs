using System.Collections;
using System.Text.Json;

namespace USIReversiLocalServer
{
    internal class EngineConfig
    {
        public string Path { get; set; }
        public string Arguments { get; set; }
        public string WorkDir { get; set; }
        public string[] InitialCommands { get; set; }
        public int MilliSecondsPerMove { get; set; }

        public EngineConfig() { }

        public EngineConfig(string path, string args, string workDir, IEnumerable<string> initialCmds, int milliSecPerMove)
        {
            this.Path = path;
            this.Arguments = args;
            this.WorkDir = workDir;
            this.InitialCommands = initialCmds.ToArray();
            this.MilliSecondsPerMove = milliSecPerMove;
        }

        public static EngineConfig? Load(string path) 
            => JsonSerializer.Deserialize<EngineConfig>(File.ReadAllText(path));

        public void Save(string path) 
            => File.WriteAllText(path, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));   
    }
}
