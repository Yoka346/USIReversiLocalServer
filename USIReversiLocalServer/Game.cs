using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using USIReversiGameServer.Reversi;

namespace USIReversiGameServer
{
    /// <summary>
    /// 対局を管理するクラス
    /// </summary>
    internal class Game
    {
        public GameConfig Config { get; }
        public EngineConfig? Engine0 { get; set; } = null;
        public EngineConfig? Engine1 { get; set; } = null;

        public Game(GameConfig gameConfig) => this.Config = gameConfig;

        public void Start(int gameNum) => Start(gameNum, true);

        /// <summary>
        /// 対局を開始する.
        /// </summary>
        /// <param name="gameNum">対局回数.</param>
        /// <param name="swapPlayer">1手ごとに手番を入れ替えるか.</param>
        public void Start(int gameNum, bool swapPlayer)
        {
            if (this.Engine0 is null || this.Engine1 is null)
                throw new NullReferenceException("All engines' config was not set.");

            var engineConfigs = new EngineConfig[2] { this.Engine0, this.Engine1 };
            var engines = RunEngines(engineConfigs);
            if (engines is null)
                return;

            var book = LoadBook();
            if (book is null)
                return;

            Console.WriteLine($"USI reversi server start : {Path.GetFileName(engineConfigs[0].Path)} v.s. {Path.GetFileName(engineConfigs[1].Path)}");
            Mainloop(gameNum, swapPlayer, engineConfigs, engines, book);
        }

        void Mainloop(int gameNum, bool swapPlayer, EngineConfig[] engineConfigs, USIEngine[] engines, BookItem[] book)
        {
            const int GAME_START_TIMEOUT_MS = 100000;

            var rand = new Random();
            
            for(var gameID = 0; gameID < gameNum; gameID++)
            {
                for (var i = 0; i < engines.Length; i++)
                    if (!engines[i].TransitionToGameStartState(GAME_START_TIMEOUT_MS))
                    {
                        Console.WriteLine($"Error : Engine \"{Path.GetFileName(engineConfigs[i].Path)}\" did not transition to game start state.");
                        return;
                    }

                var rootBoard = InitBoard(book, rand);
                var board = new Board(rootBoard);
                GameResult result;
                while((result = rootBoard.GetGameResult(DiscColor.Black)) == GameResult.NotOver)
                {
                    var sideToMove = board.SideToMove;
                    var engine = engines[(int)sideToMove];
                    var engineConfig = engineConfigs[(int)sideToMove];
                    var move = engine.Think(rootBoard, board, engineConfig.MilliSecondsPerMove);
                    if(move != BoardCoordinate.Resign)
                    {
                        board.Update(move);
                    }
                }
            }
        }

        Board InitBoard(BookItem[] book, Random rand)   // Bookからランダムに対局を選択して盤面を生成.
        {
            var bookItem = book[rand.Next(book.Length)];
            var board = bookItem.GetInitialBoard();
            var min = Math.Min(this.Config.MinBookMoveNum, bookItem.Moves.Length);
            var max = Math.Min(this.Config.MaxBookMoveNum, bookItem.Moves.Length);
            var moves = bookItem.Moves[..rand.Next(min, max + 1)];
            foreach (var move in moves)     
                board.Update(move);
            return board;
        }

        static USIEngine[]? RunEngines(EngineConfig[] engineConfigs)
        {
            var engines = (from config in engineConfigs select new USIEngine(config.Path)).ToArray();
            for (var i = 0; i < engines.Length; i++)
            {
                engines[i].InitialCommands.AddRange(engineConfigs[i].InitialCommands);
                if (!engines[i].Run())
                {
                    Console.WriteLine($"Error : Engine did not start.\nEngine path is \"{engineConfigs[i].Path}\"");
                    return null;
                }
            }
            return engines;
        }

        /// <summary>
        /// Bookのロード. Bookの形式は[sfen] moves [着手座標の羅列(f5d6c3...)]
        /// </summary>
        /// <returns></returns>
        BookItem[]? LoadBook()
        {
            if (this.Config.OpeningSfenBookPath == string.Empty)    // Bookが指定されていないなら普通に初期盤面だけを返す.
                return new BookItem[1] { BookItem.SfenToBookItem($"{USI.BoardToSfenString(Board.CreateCrossBoard())} moves ") };

            if (!File.Exists(this.Config.OpeningSfenBookPath))
            {
                Console.WriteLine($"Error : Book was not found.");
                Console.WriteLine($"Book path was \"{this.Config.OpeningSfenBookPath}\".");
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
                        Console.WriteLine($"Error : Invalid game.\nSFEN = {line}\ncontinue loading.");
                        failCount++;
                        continue;
                    }
                    book.Add(item);
                }
            }
            Console.WriteLine($"Done.\n{book.Count} games were loaded. Failed to load {failCount} games");
            return book.ToArray();
        }
    }
}
