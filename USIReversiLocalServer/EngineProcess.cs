using System.Diagnostics;

namespace USIReversiLocalServer
{
    /// <summary>
    /// 思考エンジンのプロセスを管理するクラス. このクラスを介してコマンドの送受信を行う.
    /// </summary>
    internal class EngineProcess
    {
        Process process;
        Queue<string?> recievedLines = new();

        EngineProcess(Process process)
        {
            this.process = process;
            this.process.OutputDataReceived += this.Process_OutputDataReceived;
            this.process.BeginOutputReadLine();
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

        public IgnoreSpaceStringReader ReadOutput()
        {
            if (this.recievedLines.Count == 0)
                return new IgnoreSpaceStringReader(string.Empty);
            return new IgnoreSpaceStringReader(this.recievedLines.Dequeue() ?? string.Empty);
        }

        public void SendCommand(string cmd) => this.process.StandardInput.WriteLine(cmd);

        void Process_OutputDataReceived(object sender, DataReceivedEventArgs e) => this.recievedLines.Enqueue(e.Data);
    }
}
