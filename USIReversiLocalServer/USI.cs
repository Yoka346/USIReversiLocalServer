using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using USIReversiGameServer.Reversi;

namespace USIReversiGameServer
{
    internal static class USI
    {
        internal const int SFEN_LEN = 65;    // リバーシ用
        static ReadOnlySpan<char> SFEN_DISC => new char[3] { 'X', 'O', '-' };
        static ReadOnlySpan<char> SFEN_SIDE_TO_MOVE => new char[2] { 'B', 'W' };

        /// <summary>
        /// 盤面をSFEN文字列で表現する.
        /// </summary>
        /// <param name="board">盤面</param>
        /// <returns></returns>
        public static string BoardToSfenString(Board board)
        {
            Span<char> sfen = stackalloc char[SFEN_LEN];    // SFEN文字列の長さはSFEN_LENと最初から決まっているので,
                                                            // StringBuilderを使うよりスタックに長さSFEN_LENのSpan<char>を確保した方が高速かも.
            for (var coord = BoardCoordinate.A1; coord <= BoardCoordinate.H8; coord++)
                sfen[(int)coord] = SFEN_DISC[(int)board.GetDiscColor(coord)];
            sfen[^1] = SFEN_SIDE_TO_MOVE[(int)board.SideToMove];
            return sfen.ToString();
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
            if (sfen.Length != SFEN_LEN)
                return null;

            var board = new Board();
            for (var coord = 0; coord < sfen.Length - 1; coord++)
                if (sfen[coord] == SFEN_DISC[(int)DiscColor.Black])
                    board.Put(DiscColor.Black, (BoardCoordinate)coord);
                else if (sfen[coord] == SFEN_DISC[(int)DiscColor.White])
                    board.Put(DiscColor.Black, (BoardCoordinate)coord);
                else if (sfen[coord] != SFEN_DISC[(int)DiscColor.Null])    // 無効な文字を発見したら失敗.
                    return null;

            DiscColor side;
            if (sfen[^1] == SFEN_SIDE_TO_MOVE[(int)DiscColor.Black])
                side = DiscColor.Black;
            else if (sfen[^1] == SFEN_SIDE_TO_MOVE[(int)DiscColor.White])
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
        public static BoardCoordinate ParseUSIMove(string move) => ParseUSIMove(move.AsSpan());

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
                if(move != BoardCoordinate.Pass && move != BoardCoordinate.Null)    // passは無視. 一般的にリバーシのf5d6c3...形式ではパスを表記しないので.
                    sb.Append(MoveToUSIMove(move));
            return sb.ToString();
        }

        /// <summary>
        /// USIプロトコルの棋譜文字列を盤面座標として列挙する.
        /// </summary>
        /// <param name="moves"></param>
        /// <returns></returns>
        public static IEnumerable<BoardCoordinate> ParseUSIMoves(string moves) => ParseUSIMoves(moves.AsSpan());

        /// <summary>
        /// USIプロトコルの棋譜文字列を盤面座標として列挙する.
        /// </summary>
        /// <param name="moves"></param>
        /// <returns></returns>
        public static IEnumerable<BoardCoordinate> ParseUSIMoves(ReadOnlySpan<char> moves)
        {
            for(var i = 0; i < moves.Length - 1; i += 2)
            {
                var move = ParseUSIMove(moves[i..(i + 1)]);
                if(move == BoardCoordinate.Null)
                {
                    yield return BoardCoordinate.Null;  // 無効な文字が含まれていたのでNullを返して終了.
                    yield break;
                }
                yield return move;
            }
        }
    }
}
