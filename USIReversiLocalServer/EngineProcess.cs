using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;

namespace USIReversiGameServer
{
    /// <summary>
    /// 思考エンジンのプロセスを管理するクラス. このクラスを介してコマンドの送受信を行う.
    /// </summary>
    internal class EngineProcess
    {
        Process process;

        EngineProcess(Process process)
        {
            this.process = process;
        }

        /// <summary>
        /// 与えられたエンジンのパスからプロセスを生成
        /// </summary>
        /// <param name="path">エンジンのパス(Windowsならexeファイルなど)</param>
        public static EngineProcess? Start(string path)
        {
            ProcessStartInfo psi = new ProcessStartInfo();
            psi.FileName = path;
            psi.CreateNoWindow = true;
            psi.UseShellExecute = false;
            psi.RedirectStandardInput = true;
            psi.RedirectStandardOutput = true;
            var process = Process.Start(psi);
            return process is null ? null : new EngineProcess(process);
        }

        public string ReadOutput()　=> this.process.StandardOutput.ReadLine() ?? string.Empty;
        public void SendCommand(string cmd) => this.process.StandardInput.WriteLine(cmd);
    }
}
