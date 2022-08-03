using System;
using System.Diagnostics;

using USIReversiLocalServer.Reversi;

namespace USIReversiLocalServer
{
    /// <summary>
    /// 対局を管理するクラス
    /// </summary>
    internal class Game
    {
        USIEngine[]? engines = new USIEngine[2];
        EngineConfig[] engineConfigs = new EngineConfig[2];
        BookItem[]? book;
        Random rand = new Random(Random.Shared.Next());
        USIEngine[] players;
        EngineConfig[] playerConfigs;
        int[] winCount;
        int drawCount;

        public GameConfig Config { get; }
        public EngineConfig? Engine0 { get; set; } = null;
        public EngineConfig? Engine1 { get; set; } = null;
        public bool IsPlaying { get; private set; } = false;

        public Game(GameConfig gameConfig) => this.Config = gameConfig;

        /// <summary>
        /// 対局を開始する.
        /// </summary>
        /// <param name="gameNum">対局回数.</param>
        /// <param name="swapPlayer">1手ごとに手番を入れ替えるか.</param>
        public void Start(int gameNum)
        {
            if (this.IsPlaying)
                throw new InvalidOperationException("the game has already started.");

            if (this.Engine0 is null || this.Engine1 is null)
                throw new NullReferenceException("All engines' config was not set.");

            this.engineConfigs = new EngineConfig[2] { this.Engine0, this.Engine1 };
            this.engines = RunEngines();
            if (this.engines is null)
                return;

            this.players = (USIEngine[])this.engines.Clone();
            this.playerConfigs = (EngineConfig[])this.engineConfigs.Clone();

            this.book = LoadBook();
            if (this.book is null)
                return;

            this.IsPlaying = true;
            Console.WriteLine($"USI reversi server start : {Path.GetFileName(engineConfigs[0].Path)} v.s. {Path.GetFileName(engineConfigs[1].Path)}");
            if (!Mainloop(gameNum))
                Console.WriteLine($"Game was suspended.");
            this.IsPlaying = false;
        }

        bool Mainloop(int gameNum)
        {
            const int GAME_START_TIMEOUT_MS = 60000;

            this.winCount = new int[2];
            this.drawCount = 0;
            for(var gameID = 0; gameID < gameNum; gameID++)
            {
                for (var i = 0; i < this.engines.Length; i++)
                    if (!this.engines[i].TransitionToGameStartState(GAME_START_TIMEOUT_MS))
                    {
                        Console.Error.WriteLine($"Error : Engine \"{Path.GetFileName(engineConfigs[i].Path)}\" did not transition to game start state.");
                        QuitEngines();
                        return false;
                    }

                var rootBoard = InitBoard();
                var board = new Board(rootBoard);
                var player = this.players[(int)board.SideToMove];
                var opponet = this.players[(int)board.Opponent];
                var playerMoveMs = this.playerConfigs[(int)board.SideToMove].MilliSecondsPerMove;
                var opponentMoveMs = this.playerConfigs[(int)board.Opponent].MilliSecondsPerMove;
                var lastMove = BoardCoordinate.Null;

                Debug.WriteLine($"Root board: \n{rootBoard}");

                while(true)
                {
                    var move = player.Think(rootBoard, board, lastMove, playerMoveMs);
                    if(move != BoardCoordinate.Resign)
                    {
                        if (!board.Update(move))
                        {
                            Console.Error.WriteLine($"Error : Move {move} is illegal.");
                            QuitEngines();
                            return false;
                        }

                        Debug.WriteLine($"\nCurrent board: \n{board}");

                        var result = board.GetGameResult(DiscColor.Black);
                        if (result != GameResult.NotOver)
                        {
                            OnGameOver(DiscColor.Black, result);
                            break;
                        }
                    }
                    else
                    {
                        OnGameOver(board.SideToMove, GameResult.Resigned);
                        break;
                    }

                    (player, opponet) = (opponet, player);
                    (playerMoveMs, opponentMoveMs) = (opponentMoveMs, playerMoveMs);
                }
            }
            QuitEngines();
            return true;
        }

        Board InitBoard()   // Bookからランダムに対局を選択して盤面を生成.
        {
            var bookItem = this.book[this.rand.Next(book.Length)];
            var board = bookItem.GetInitialBoard();
            var min = Math.Min(this.Config.MinBookMoveNum, bookItem.Moves.Length);
            var max = Math.Min(this.Config.MaxBookMoveNum, bookItem.Moves.Length);
            var moves = bookItem.Moves[..this.rand.Next(min, max + 1)];
            foreach (var move in moves)     
                board.Update(move);
            return board;
        }

        void OnGameOver(DiscColor player, GameResult result)
        {
            switch (result)
            {
                case GameResult.Resigned:
                    Console.WriteLine($"Game over : {this.players[(int)player].Name}({player}) resigns.");
                    this.winCount[(int)FastBoard.GetOpponentColor(player)]++;
                    break;

                case GameResult.Win:
                    Console.WriteLine($"Game over : {this.players[(int)player].Name}({player}) wins.");
                    this.winCount[(int)player]++;
                    break;

                case GameResult.Loss:
                    var opponent = FastBoard.GetOpponentColor(player);
                    Console.WriteLine($"Game over : {this.players[(int)opponent].Name}({opponent}) wins.");
                    this.winCount[(int)opponent]++;
                    break;

                case GameResult.Draw:
                    Console.WriteLine($"Game over : Draw.");
                    this.drawCount++;
                    break;
            }

            var colorOfEngine0 = this.players[0] == this.engines[0] ? DiscColor.Black : DiscColor.White;
            var colorOfEngine1 = FastBoard.GetOpponentColor(colorOfEngine0);
            int winCount0 = this.winCount[(int)colorOfEngine0];
            int winCount1 = this.winCount[(int)colorOfEngine1];
            var gameCount = winCount0 + winCount1 + this.drawCount;
            Console.WriteLine($"{this.engines[0].Name} v.s. {this.engines[1].Name} : {winCount0}-{this.drawCount}-{winCount1}");
            Console.WriteLine($"{this.engines[0].Name} winning rate : {(winCount0 + 0.5f * this.drawCount) * 100.0f / gameCount:f2}%");
            Console.WriteLine($"{this.engines[1].Name} winning rate : {(winCount1 + 0.5f * this.drawCount) * 100.0f / gameCount:f2}%\n");

            if (result == GameResult.Draw)
            {
                foreach (var engine in this.engines)
                    engine.GameOver(result);
            }
            else
            {
                var winner = (result == GameResult.Loss || result == GameResult.Resigned)
                            ? FastBoard.GetOpponentColor(player) : player;
                this.players[(int)winner].GameOver(GameResult.Win);
                this.players[(int)FastBoard.GetOpponentColor(winner)].GameOver(GameResult.Loss);
            }

            if (this.Config.SwapPlayer)
                SwapPlayer();
        }

        void SwapPlayer()
        {
            Swap(this.players);
            Swap(this.playerConfigs);
            Swap(this.winCount);
        }

        USIEngine[]? RunEngines()
        {
            var engines = (from config in this.engineConfigs select new USIEngine(config.Path, config.Arguments, config.WorkDir)).ToArray();
            var failFlag = false;
            Parallel.For(0, engines.Length, i =>
            {
                engines[i].InitialCommands.AddRange(this.engineConfigs[i].InitialCommands);
                if (!engines[i].Run())
                {
                    Console.Error.WriteLine($"Error : Engine did not start.\nEngine path is \"{this.engineConfigs[i].Path}\"");
                    failFlag = true;
                    return;     
                }
                engines[i].Terminated += Engine_Terminated;
            });
            return failFlag ? null : engines;
        }

        void QuitEngines()
        {
            Parallel.ForEach(this.engines, engine =>
            {
                if (!engine.HasQuitSuccessfully)
                    if (!engine.Quit())
                    {
                        Console.Error.WriteLine($"Error : {engine.Name} did not quit after sending quit command.");
                        engine.Kill();
                        Console.Error.WriteLine($"Error : {engine.Name} was forcefully terminated.");
                    }
                    else
                        Console.WriteLine($"{engine.Name} was terminated.");
            });
        }

        void Engine_Terminated(object? sender, EventArgs e)
        {
            if (sender is null)
            {
                Console.Error.WriteLine("Error : Unknown engine was terminated.");
                return;
            }

            var engine = (USIEngine)sender;
            if(engine.HasQuitUnexpectedly)
                Console.Error.WriteLine($"Error : {engine.Name} was terminated unexpectedly.");
        }

        /// <summary>
        /// Bookのロード. Bookの形式は[sfen] moves [着手の羅列(f5 d6 c3 .. pass ...)]
        /// </summary>
        /// <returns></returns>
        BookItem[]? LoadBook()
        {
            if (this.Config.OpeningSfenBookPath == string.Empty)    // Bookが指定されていないなら普通に初期盤面だけを返す.
                return new BookItem[1] { BookItem.SfenToBookItem($"position sfen {USI.BoardToSfenString(Board.CreateCrossBoard())}") };

            if (!File.Exists(this.Config.OpeningSfenBookPath))
            {
                Console.Error.WriteLine($"Error : Book was not found.");
                Console.Error.WriteLine($"Book path was \"{this.Config.OpeningSfenBookPath}\".");
                return null;
            }

            Console.WriteLine("Loading opening book.");
            using var sr = new StreamReader(this.Config.OpeningSfenBookPath);
            var book = new List<BookItem>();
            var failCount = 0;
            while(sr.Peek() != -1)
            {
                var line = sr.ReadLine();
                if (line is not null)
                {
                    var item = BookItem.SfenToBookItem(line);
                    if(item is null)
                    {
                        Console.Error.WriteLine($"Error : Invalid game: {line}\ncontinue loading.");
                        failCount++;
                        continue;
                    }
                    book.Add(item);
                }
            }
            Console.WriteLine($"Done.\n{book.Count} games were loaded.");
            if(failCount > 0)
                Console.WriteLine($"Failed to load {failCount} games");
            return book.ToArray();
        }

        static void Swap<T>(T[] array)
        {
            if (array.Length == 2)
                (array[1], array[0]) = (array[0], array[1]);
        }
    }
}
