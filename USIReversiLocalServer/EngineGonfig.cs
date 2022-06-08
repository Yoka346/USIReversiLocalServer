using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace USIReversiGameServer
{
    internal class EngineConfig
    {
        public string Path { get; private set; }
        public string ThinkCommand { get; private set; }
        public ReadOnlyCollection<string> InitialCommands { get; private set; }

        public EngineConfig(string path, string thinkCmd, IEnumerable<string> initialCmds)
        {
            this.Path = path;
            this.ThinkCommand = thinkCmd;
            this.InitialCommands = new ReadOnlyCollection<string>(initialCmds.ToArray());
        }

        public static EngineConfig? Load(string path) 
            => JsonSerializer.Deserialize(File.ReadAllText(path), typeof(EngineConfig)) as EngineConfig;

        public void Save(string path) 
            => File.WriteAllText(path, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));   
    }
}
