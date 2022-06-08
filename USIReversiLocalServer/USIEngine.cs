using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using USIReversiGameServer.Reversi;

namespace USIReversiGameServer
{
    internal enum USIEngineState
    {
        StartUp, 
        WaitUSIOK, 
        IsReady, 
        WaitReadyOK, 
        GameStart, 
        GameOver
    }

    /// <summary>
    ///USI(Universal Standard game Interface)に準拠した思考エンジンを管理するクラス.
    /// このクラスを介して着手などの情報を得たり, サーバーから盤面の情報を送信したりする.
    /// </summary>
    internal class USIEngine
    {
        readonly string ENGINE_PATH;
        readonly string THINK_CMD;
        EngineProcess process;
        readonly ReadOnlyCollection<Action> ON_IDLE;    // OnIdleメソッドが呼ばれたときに用いるコールバック.

        /// <summary>
        /// id name コマンドから受け取ったエンジンの名前.
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// 思考エンジンの状態.
        /// </summary>
        public USIEngineState State { get; private set; }

        public bool IsGameStarted => State == USIEngineState.GameStart;

        /// <summary>
        /// 思考エンジンがコマンド受理状態(USIEngineState.IsReady)になったら一斉に送るコマンド.
        /// </summary>
        public List<string> InitialCommands { get; private set; } = new();

        public USIEngine(string path, string thinkCmd) 
        { 
            this.ENGINE_PATH = path;
            this.THINK_CMD = thinkCmd;
            var onIdle = new Action[] { this.OnStartUp, this.OnWaitUSIOK, this.OnIsReady, 
                                        this.OnWaitReadyOK, () => { }, this.OnGameOver };
            this.ON_IDLE = new ReadOnlyCollection<Action>(onIdle);
        }

        /// <summary>
        /// 思考エンジンのプロセスを開始する.
        /// </summary>
        /// <returns>プロセスの生成に成功したか.</returns>
        public bool Run()
        {
            var process = EngineProcess.Start(this.ENGINE_PATH);
            if (process is null)
                return false;
            this.process = process;
            this.State = USIEngineState.StartUp;
            return true;
        }

        /// <summary>
        /// 思考エンジンの状態(USIEngine.State)に応じた処理を行った後, 次の状態へ遷移する.
        /// </summary>
        // テーブル参照にしている理由は高速化のためではなく, 単純に長いswitch文を書きたくなかったから.
        public void OnIdle() => this.ON_IDLE[(int)this.State]();

        /// <summary>
        /// ゲーム開始状態(USIEngineState.GameStart)に遷移させる. 
        /// </summary>
        /// <param name="timeoutMs">タイムアウトする時間(ms)</param>
        /// <returns>ゲーム開始状態に遷移できたかどうか.</returns>
        public bool TransitionToGameStartState(int timeoutMs)
        {
            var successFlag = true;
            var startTime = Environment.TickCount;
            while (!this.IsGameStarted && (successFlag = Environment.TickCount - startTime < timeoutMs))
                OnIdle();
            return successFlag;
        }

        /// <summary>
        /// 思考エンジンに思考を依頼する.
        /// </summary>
        /// <param name="rootBoard">初期盤面</param>
        /// <param name="board">現在の盤面.</param>
        /// <param name="thinkCmd">思考コマンド.</param>
        /// <returns></returns>
        public BoardCoordinate Think(Board rootBoard, Board board)
        {
            var sfen = $"position {USI.BoardToSfenString(rootBoard)} moves {USI.MovesToUSIMovesString(rootBoard.EnumerateMoveHistory())}";
            this.process.SendCommand(sfen);
            this.process.SendCommand(this.THINK_CMD);

            string bestMove;
            do
                bestMove = this.process.ReadOutput();
            while (!bestMove.Contains("bestmove"));

            var uoiMove = bestMove.AsSpan(Math.Min("bestmove ".Length, bestMove.Length - 1));   // String.Substringは遅いのでSpanで着手のみを切り取る.
            var move = USI.ParseUSIMove(uoiMove);
            if (move == BoardCoordinate.Null)
                Console.WriteLine($"Error : bestmove = {uoiMove}\n{board}");    
            return move;
        }

        public void GameOver() => this.State = USIEngineState.GameOver;

        void OnStartUp()
        {
            this.process.SendCommand("uoi");
            this.State = USIEngineState.WaitUSIOK;
        }

        void OnWaitUSIOK()
        {
            var output = this.process.ReadOutput();
            if (output == "uoiok")
                this.State = USIEngineState.IsReady;
            else if (output.Substring(0, Math.Min(output.Length, 8)) == "id name ")
                this.Name = output.Substring(8, output.Length - 8);
        }

        void OnIsReady()
        {
            foreach (var cmd in this.InitialCommands)
                this.process.SendCommand(cmd);

            this.process.SendCommand("isready");
            this.State = USIEngineState.WaitReadyOK;
        }

        void OnWaitReadyOK()
        {
            if(this.process.ReadOutput() == "readyok")
            {
                this.process.SendCommand("uoinewgame");
                this.State = USIEngineState.GameStart;
            }
        }

        void OnGameOver()
        {
            this.process.SendCommand("gameover");
            this.State = USIEngineState.StartUp;
        }
    }
}
