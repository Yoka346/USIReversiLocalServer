using System.Text;

using USIReversiLocalServer.Reversi;

namespace USIReversiLocalServer
{
    internal static class USI
    {
        internal const int MAX_SFEN_LEN = 67;    // リバーシ用
        static ReadOnlySpan<char> SFEN_DISC => new char[3] { 'X', 'O', '-' };
        static ReadOnlySpan<char> SFEN_SIDE_TO_MOVE => new char[2] { 'B', 'W' };

        /// <summary>
        /// 盤面をSFEN文字列で表現する.
        /// </summary>
        /// <param name="board">盤面</param>
        /// <returns></returns>
        public static string BoardToSfenString(Board board)
        {
            Span<char> sfen = stackalloc char[MAX_SFEN_LEN];    
            for (var coord = BoardCoordinate.A1; coord <= BoardCoordinate.H8; coord++)
                sfen[(int)coord] = SFEN_DISC[(int)board.GetDiscColor(coord)];
            sfen[Board.SQUARE_NUM] = SFEN_SIDE_TO_MOVE[(int)board.SideToMove];

            // 手数は60 - 空きマス数 + 1で計算する。それ故にパスは手数に含まれない。
            var moveNum = (Board.SQUARE_NUM - 4) - board.GetEmptyCount() + 1;
            if (moveNum < 10)
            {
                sfen[Board.SQUARE_NUM + 1] = (char)('0' + moveNum);
                return sfen[..^1].ToString();
            }
            else
            {
                sfen[Board.SQUARE_NUM + 1] = (char)('0' + moveNum / 10);
                sfen[Board.SQUARE_NUM + 2] = (char)('0' + moveNum % 10); 
                return sfen.ToString();
            }
        }

        /// <summary>
        /// SFEN文字列を盤面に変換する.
        /// </summary>
        /// <param name="sfen"></param>
        /// <returns></returns>
        public static Board? SfenStringToBoard(string sfen) => SfenStringToBoard(sfen.AsSpan());

        /// <summary>
        /// SFEN文字列を盤面に変換する.
        /// </summary>
        /// <param name="sfen"></param>
        /// <returns></returns>
        public static Board? SfenStringToBoard(ReadOnlySpan<char> sfen)
        {
            if (sfen.Length < Board.SQUARE_NUM + 1)    // 少なくとも 盤面 + 手番 の長さがあるか調べる. 
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
        public static BoardCoordinate ParseUSIMove(string move)
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
        /// 盤面座標をUSIプロトコルの着手文字列に変換する.
        /// </summary>
        /// <param name="coord">盤面座標</param>
        /// <returns></returns>
        public static string? MoveToUSIMove(BoardCoordinate coord)
        {
            if (coord == BoardCoordinate.Null)
                return null;

            if (coord == BoardCoordinate.Pass)
                return "pass";
            return $"{(char)('a' + (byte)coord % Board.BOARD_SIZE)}{((byte)coord / Board.BOARD_SIZE) + 1}";
        }

        /// <summary>
        /// 着手の羅列をUSIプロトコルの着手文字列に変換する.
        /// </summary>
        /// <param name="moves"></param>
        /// <returns></returns>
        public static string MovesToUSIMovesString(IEnumerable<BoardCoordinate> moves)
        {
            var sb = new StringBuilder();
            foreach (var move in moves)
                if(move != BoardCoordinate.Null)   
                    sb.Append(MoveToUSIMove(move)).Append(' ');
            return sb.ToString();
        }

        /// <summary>
        /// USIプロトコルの棋譜文字列を盤面座標として列挙する.
        /// </summary>
        /// <param name="moves"></param>
        /// <returns></returns>
        public static IEnumerable<BoardCoordinate> ParseUSIMoves(IgnoreSpaceStringReader isr)
        {
            while(isr.Peek() != -1)
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
