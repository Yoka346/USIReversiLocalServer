using System.Collections.ObjectModel;

using USITestClient.Reversi;

namespace USITestClient
{
    /// <summary>
    /// USIで実際にやり取りを行う部分.
    /// あくまでもUSITestClientのテスト用なので一部のUSIコマンドにしか対応していない.
    /// </summary>
    internal static class USI
    {
        const int SFEN_LEN = 67;
        static ReadOnlySpan<char> SFEN_DISC => new char[3] { 'X', 'O', '-' };
        static ReadOnlySpan<char> SFEN_SIDE_TO_MOVE => new char[2] { 'b', 'w' };

        static readonly ReadOnlyDictionary<string, Action<IgnoreSpaceStringReader>> COMMANDS;

        static USIEngine? Engine;

        static USI()
        {
            var cmds = new Dictionary<string, Action<IgnoreSpaceStringReader>>();
            cmds["usi"] = ExecuteUSICommand;
            cmds["isreadey"] = ExecuteIsReadyCommand;
            cmds["setoption"] = ExecuteSetOptionCommand;
            cmds["usinewgame"] = ExecuteUSINewGameCommand;
            cmds["position"] = ExecutePositionCommand;
            cmds["go"] = ExecuteGoCommand;
            cmds["stop"] = ExecuteStopCommand;

            COMMANDS = new ReadOnlyDictionary<string, Action<IgnoreSpaceStringReader>>(cmds);
        }

        public static void SetEngine(USIEngine engine) => Engine = engine;

        /// <summary>
        /// USIのメインループ. コマンドの受理→実行..の流れをquitを受け取るまで繰り返す.
        /// </summary>
        public static void Mainloop()
        {
            if (Engine is null)
                throw new NullReferenceException("Engine was not set.");

            while (true)
            {
                var line = Console.ReadLine()?.ToLower();
                if (line is null)
                    continue;

                var cmdLine = new IgnoreSpaceStringReader(line);
                var cmdName = cmdLine.Read();

                if (cmdName == "quit")
                {
                    Engine.Quit();
                    break;
                }

                if (!COMMANDS.ContainsKey(cmdName))
                {
                    Console.WriteLine($"info string Error! : No such command: {cmdName}");
                    continue;
                }
                Task.Run(() => COMMANDS[cmdName](cmdLine));
            }
        }

        static void ExecuteUSICommand(IgnoreSpaceStringReader cmdLine)
        {
            if (Engine is null)
                throw new NullReferenceException("Engine was not set.");

            Engine.Init();
            Console.WriteLine($"id name {Engine.Name}");
            Console.WriteLine($"id author {Engine.Author}");
            foreach (var option in Engine.EnumerateOptionString())
                Console.WriteLine(option);
            Console.WriteLine("usiok");
        }

        static void ExecuteIsReadyCommand(IgnoreSpaceStringReader cmdLine)
        {
            Engine?.ReadyForGame();
            Console.WriteLine("readyok");
        }

        static void ExecuteSetOptionCommand(IgnoreSpaceStringReader cmdLine)
        {
            if (Engine is null)
                throw new NullReferenceException("Engine was not set.");

            var token = cmdLine.Read();
            if (token != "name")
            {
                Console.WriteLine("info string Error! : \"name\" token is required before option's name.");
                return;
            }

            var optionName = cmdLine.Read();
            if (!Engine.HasOption(optionName))
            {
                Console.WriteLine($"info string Error! : No such option: {optionName}");
                return;
            }

            token = cmdLine.Read();
            if (token != string.Empty && token != "value")
            {
                Console.WriteLine("info string Error! : \"value\" token is required before option's value");
                return;
            }

            Engine.SetOption(optionName, cmdLine.Read());
        }

        static void ExecuteUSINewGameCommand(IgnoreSpaceStringReader cmdLine) => Engine?.StartNewGame();

        static void ExecutePositionCommand(IgnoreSpaceStringReader cmdLine)
        {
            var sfen = cmdLine.Read();
            var rootBoard = SfenStringToBoard(sfen);
            if(rootBoard is null)
            {
                Console.WriteLine($"info string Error! : Invalid sfen: {sfen}");
                return;
            }

            if (cmdLine.Read() == "moves")
            {
                var board = new Board(rootBoard);
                var moves = ParseUSIMoves(cmdLine).ToArray();
                foreach (var move in moves)
                    if (!board.Update(move))
                    {
                        Console.WriteLine($"info string Error! : Invalid moves");
                        return;
                    }
                Engine?.SetBoard(rootBoard, board, moves);
            }
            else
                Engine?.SetBoard(rootBoard, new Board(rootBoard), new BoardCoordinate[0]);
        }

        static void ExecuteGoCommand(IgnoreSpaceStringReader cmdLine)
        {
            if (Engine is null)
                throw new NullReferenceException("Engine was not set.");

            var byoyomiIsEnabled = false;
            while (cmdLine.Peek() != -1 && !(byoyomiIsEnabled = cmdLine.Read() == "byoyomi")) ;

            BoardCoordinate move;
            if (byoyomiIsEnabled && int.TryParse(cmdLine.Read(), out int byoyomi))
                move = Engine.GenerateMove(byoyomi);
            else
                move = Engine.GenerateMove();
            Console.WriteLine($"bestmove {move.ToString().ToLower()}");
        }

        static void ExecuteStopCommand(IgnoreSpaceStringReader cmdLine) => Engine?.StopThinking();

        /// <summary>
        /// SFEN文字列を盤面に変換する.
        /// </summary>
        /// <param name="sfen"></param>
        /// <returns></returns>
        static Board? SfenStringToBoard(ReadOnlySpan<char> sfen)
        {
            if (sfen.Length < Board.SQUARE_NUM + 1)     // 盤面 + 手番 で65文字. 手数情報が含まれていなくてもokとする.  
                return null;

            var board = new Board();
            for (var coord = 0; coord < Board.SQUARE_NUM; coord++)
                if (sfen[coord] == SFEN_DISC[(int)DiscColor.Black])
                    board.Put(DiscColor.Black, (BoardCoordinate)coord);
                else if (sfen[coord] == SFEN_DISC[(int)DiscColor.White])
                    board.Put(DiscColor.White, (BoardCoordinate)coord);
                else if (sfen[coord] != SFEN_DISC[(int)DiscColor.Null])    // 無効な文字を発見したら失敗.
                    return null;

            DiscColor side;
            if (sfen[Board.SQUARE_NUM] == SFEN_SIDE_TO_MOVE[(int)DiscColor.Black])
                side = DiscColor.Black;
            else if (sfen[Board.SQUARE_NUM] == SFEN_SIDE_TO_MOVE[(int)DiscColor.White])
                side = DiscColor.White;
            else
                return null;    // 手番文字が無効であった場合は失敗.

            if (side == DiscColor.White)
                board.SwitchSideToMove();

            return board;
        }

        /// <summary>
        /// USIプロトコルの着手文字列を盤面座標に変換する.
        /// </summary>
        /// <param name="move">USIプロトコルにおける着手.</param>
        /// <returns></returns>
        public static BoardCoordinate ParseUSIMove(ReadOnlySpan<char> move)
        {
            if (move == "pass")
                return BoardCoordinate.Pass;

            if (move == "resign")
                return BoardCoordinate.Resign;

            if (move.Length != 2)
                return BoardCoordinate.Null;

            var x = char.ToLower(move[0]) - 'a';
            var y = move[1] - '1';
            if (x < 0 || x >= Board.BOARD_SIZE || y < 0 || y >= Board.BOARD_SIZE)
                return BoardCoordinate.Null;
            return (BoardCoordinate)(x + y * Board.BOARD_SIZE);
        }

        /// <summary>
        /// USIプロトコルの棋譜文字列を盤面座標として列挙する.
        /// </summary>
        /// <param name="moves"></param>
        /// <returns></returns>
        public static IEnumerable<BoardCoordinate> ParseUSIMoves(IgnoreSpaceStringReader isr)
        {
            while (isr.Peek() != -1)
            {
                var move = ParseUSIMove(isr.Read());
                if (move == BoardCoordinate.Null)
                {
                    yield return BoardCoordinate.Null;  // 無効な文字が含まれていたのでNullを返して終了.
                    yield break;
                }
                yield return move;
            }
        }
    }
}
