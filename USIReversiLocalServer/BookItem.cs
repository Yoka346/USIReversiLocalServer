using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using USIReversiGameServer.Reversi;

namespace USIReversiGameServer
{
    /// <summary>
    /// Bookに格納されている1局分の情報
    /// </summary>
    internal class BookItem
    {
        BoardCoordinate[] moves;
        Board initialBoard;

        public ReadOnlySpan<BoardCoordinate> Moves => this.moves;

        BookItem() { }

        public static BookItem? SfenToBookItem(string sfen)     // 無効なフォーマットがあったらnullを返しているけど,
                                                                // 本当は無効である理由とともに例外を投げたほうがいいかも.
        {
            if (sfen.Length < USI.SFEN_LEN)
                return null;

            var board = USI.SfenStringToBoard(sfen.AsSpan(0, USI.SFEN_LEN));
            if (board is null)
                return null;

            var usiMoves = sfen.AsSpan(USI.SFEN_LEN);
            if (usiMoves.Length < " moves ".Length || usiMoves[0..(" moves ".Length)] != " moves ")
                return null;
            if (usiMoves.Length == " moves ".Length)
                return new BookItem { initialBoard = board, moves = new BoardCoordinate[0] };

            usiMoves = usiMoves[" moves ".Length..];
            var moves = USI.ParseUSIMoves(usiMoves).ToArray();
            foreach (var move in moves) // 合法手かチェック.
                if (move == BoardCoordinate.Null || !board.Update(move))
                    return null;

            while (board.Undo()) ;

            var item = new BookItem();
            item.initialBoard = board;
            item.moves = moves;
            return item;
        } 

        public Board GetInitialBoard()
        {
            return new Board(this.initialBoard);
        }
    }
}
