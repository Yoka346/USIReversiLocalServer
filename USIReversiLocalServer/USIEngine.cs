using System.Collections.ObjectModel;

using USIReversiLocalServer.Reversi;

namespace USIReversiLocalServer
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
    /// USI(Universal Standard game Interface)に準拠した思考エンジンを管理するクラス.
    /// このクラスを介して着手などの情報を得たり, サーバーから盤面の情報を送信したりする.
    /// </summary>
    internal class USIEngine
    {
        readonly string ENGINE_PATH;
        readonly string ENGINE_ARGUMENTS;
        readonly string ENGINE_WORKDIR;
        EngineProcess process;
        readonly ReadOnlyCollection<Action> ON_IDLE;    

        /// <summary>
        /// id name コマンドから受け取ったエンジンの名前.
        /// </summary>
        public string? Name { get; private set; }

        /// <summary>
        /// id author コマンドから受け取ったエンジンの作者の名前.
        /// </summary>
        public string? Author { get; private set; }

        /// <summary>
        /// 思考エンジンの状態.
        /// </summary>
        public USIEngineState State { get; private set; }

        public bool IsGameStarted => State == USIEngineState.GameStart;

        /// <summary>
        /// Ponder(相手の手番中に思考)が有効かどうか.
        /// </summary>
        public bool PonderIsEnabled => this.PonderMove != BoardCoordinate.Null;

        public BoardCoordinate PonderMove { get; private set; } = BoardCoordinate.Null;

        public bool QuitCommandWasSended { get; private set; }

        /// <summary>
        /// エンジンが正常終了したかどうか.
        /// </summary>
        public bool HasQuitSuccessfully { get; private set; }

        /// <summary>
        /// エンジンが予期せず終了したかどうか. 予期しない終了とは, QuitメソッドまたはKillメソッド以外での終了のこと.
        /// </summary>
        public bool HasQuitUnexpectedly { get; private set; }

        /// <summary>
        /// エンジンが強制終了されたかどうか.
        /// </summary>
        public bool WasKilled { get; private set; }

        public event EventHandler Terminated;

        /// <summary>
        /// 秒読みオーバーの許容値.
        /// </summary>
        public uint ByoyomiToleranceMs { get; set; }

        /// <summary>
        /// 思考エンジンがコマンド受理状態(USIEngineState.IsReady)になったら一斉に送るコマンド.
        /// </summary>
        public List<string> InitialCommands { get; private set; } = new();

        public USIEngine(string path, string args, string workDir) 
        { 
            this.ENGINE_PATH = path;
            this.ENGINE_ARGUMENTS = args;
            this.ENGINE_WORKDIR = workDir;
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
            var process = EngineProcess.Start(this.ENGINE_PATH, this.ENGINE_ARGUMENTS, this.ENGINE_WORKDIR);
            if (process is null)
                return false;

            this.process = process;
            this.process.Exited += Process_Exited;
            if (process.HasExited)
                return false;

            this.State = USIEngineState.StartUp;
            return true;
        }

        /// <summary>
        /// 思考エンジンを終了させる.
        /// </summary>
        /// <param name="timeoutMs">タイムアウトのミリ秒</param>
        /// <returns>quitコマンド送信後, 時間内に終了したか否か.</returns>
        public bool Quit(int timeoutMs = 10000)
        {
            this.process.SendCommand("quit");
            this.QuitCommandWasSended = true;
            this.process.WaitForExit(timeoutMs);
            return this.HasQuitSuccessfully = this.process.HasExited;
        }

        /// <summary>
        /// 思考エンジンを強制終了させる.
        /// </summary>
        /// <returns></returns>
        public void Kill()
        {
            this.WasKilled = true;
            this.process.Kill();
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
        /// <param name="timeLimitMilliSec">1手の時間制限</param>
        /// <returns></returns>
        public BoardCoordinate Think(Board rootBoard, Board board, BoardCoordinate lastMove, int timeLimitMilliSec)
        {
            var ponderhit = false;
            if (this.PonderIsEnabled)
            {
                if (ponderhit = this.PonderMove == lastMove)
                    this.process.SendCommand($"ponderhit byoyomi {timeLimitMilliSec}");
                else if (StopThinking() == BoardCoordinate.Null)
                    return this.PonderMove = BoardCoordinate.Null;
                this.PonderMove = BoardCoordinate.Null;
            }

            if (!ponderhit)
            {
                var cmd = $"position sfen {USI.BoardToSfenString(rootBoard)} moves {USI.MovesToUSIMovesString(board.EnumerateMoveHistory())}";
                this.process.SendCommand(cmd);
                this.process.SendCommand($"go byoyomi {timeLimitMilliSec}");
            }

            IgnoreSpaceStringReader bestMove;
            var startTime = Environment.TickCount;
            do
            {
                if (Environment.TickCount - startTime > timeLimitMilliSec + this.ByoyomiToleranceMs)
                {
                    Console.WriteLine($"Error : Timeout!! {this.Name} did not return the best move within {timeLimitMilliSec}[ms]");
                    StopThinking();
                    return BoardCoordinate.Null;
                }
                bestMove = this.process.ReadOutput();
            } while (bestMove.Read() != "bestmove");

            var usiMove = bestMove.Read();
            var move = USI.ParseUSIMove(usiMove);
            if (move == BoardCoordinate.Null)
            {
                Console.WriteLine($"Error : Cannot parse \"{usiMove}\". \n");
                return BoardCoordinate.Null;
            }

            if(bestMove.Read() == "ponder")
            {
                var usiPonderMove = bestMove.Read();
                this.PonderMove = USI.ParseUSIMove(usiPonderMove);
                if (this.PonderMove == BoardCoordinate.Null)
                {
                    Console.WriteLine($"Error : Cannnot parse \"{usiPonderMove}\".");
                    return BoardCoordinate.Null;
                }

                if (board.Update(this.PonderMove))  // ponderで非合法手を受け取っても対局に影響を及ぼさないのでエラーにはしない.
                {
                    var cmd = $"position sfen {USI.BoardToSfenString(rootBoard)} moves {USI.MovesToUSIMovesString(board.EnumerateMoveHistory())}";
                    this.process.SendCommand(cmd);
                    this.process.SendCommand("go ponder");
                    board.Undo();
                }
            }
            return move;
        }

        /// <summary>
        /// 思考エンジンの思考を停止させ, 暫定の最善手を得る.
        /// </summary>
        public BoardCoordinate StopThinking(int timeoutMs = 10000)
        {
            this.process.SendCommand("stop");

            IgnoreSpaceStringReader output;
            var start = Environment.TickCount;
            var timeout = false;
            do
                output = this.process.ReadOutput();
            while (output.Read() != "bestmove" && !(timeout = Environment.TickCount - start >= timeoutMs));

            if (timeout)
            {
                Console.WriteLine($"Error : Timeout!! {this.Name} did not respond to \"stop\" command.");
                return BoardCoordinate.Null;
            }

            var usiMove = output.Read(); 
            var move = USI.ParseUSIMove(usiMove);
            if (move == BoardCoordinate.Null)
            {
                Console.WriteLine($"Error : Cannot parse \"{usiMove}\". \n");
                return BoardCoordinate.Null;
            }

            return move;
        }

        public void GameOver(GameResult result) 
        {
            this.process.SendCommand($"gameover {result.ToString().ToLower()}");
            this.State = USIEngineState.GameOver; 
        }

        void OnStartUp()
        {
            this.process.SendCommand("usi");
            this.State = USIEngineState.WaitUSIOK;
        }

        void OnWaitUSIOK()
        {
            var output = this.process.ReadOutput();
            var header = output.Read();
            if (header == "usiok")
                this.State = USIEngineState.IsReady;
            else if (header == "id")
            {
                var token = output.Read();
                if (token == "name")
                    this.Name = output.ReadToEnd().ToString();
                else if (token == "author")
                    this.Author = output.ReadToEnd().ToString();
            }
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
            if(this.process.ReadOutput().Read() == "readyok")
            {
                this.process.SendCommand("usinewgame");
                this.State = USIEngineState.GameStart;
            }
        }

        void OnGameOver()
        {
            this.process.SendCommand("gameover");
            this.State = USIEngineState.StartUp;
        }

        void Process_Exited(object? sender, EventArgs e)
        {
            if(!this.WasKilled)
                this.HasQuitUnexpectedly = !this.QuitCommandWasSended;
            this.Terminated.Invoke(this, e);
        }
    }
}
