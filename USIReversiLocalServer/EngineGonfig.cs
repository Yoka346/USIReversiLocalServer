using System.Collections.ObjectModel;
using System.Text.Json;

namespace USIReversiLocalServer
{
    internal class EngineConfig
    {
        public string Path { get; }
        public ReadOnlyCollection<string> InitialCommands { get; }
        public int MilliSecondsPerMove { get; }

        public EngineConfig(string path, IEnumerable<string> initialCmds, int milliSecPerMove)
        {
            this.Path = path;
            this.InitialCommands = new ReadOnlyCollection<string>(initialCmds.ToArray());
            this.MilliSecondsPerMove = milliSecPerMove;
        }

        public static EngineConfig? Load(string path) 
            => JsonSerializer.Deserialize<EngineConfig>(File.ReadAllText(path));

        public void Save(string path) 
            => File.WriteAllText(path, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));   
    }
}
