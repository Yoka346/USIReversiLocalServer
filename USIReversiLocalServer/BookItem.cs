
using USIReversiLocalServer.Reversi;

namespace USIReversiLocalServer
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

        public static BookItem? SfenToBookItem(string position)     
        {
            if (position.Length < Board.SQUARE_NUM + 1)
                return null;

            var posReader = new IgnoreSpaceStringReader(position);
            if (posReader.Read() != "position")
                return null;

            Board? board = null;
            var token = posReader.Read();
            if (token == "sfen")
                board = USI.SfenStringToBoard(posReader.ReadToEnd());
            else if (token == "startpos")
                board = Board.CreateCrossBoard();
            if (board is null)
                return null;

            BoardCoordinate[] moves;
            token = posReader.Read();
            if (token == "\0")
                moves = new BoardCoordinate[0];
            else if (token != "moves")
                return null;
            else
            {
                string usiMove;
                Span<BoardCoordinate> moveList = stackalloc BoardCoordinate[Board.MAX_MOVE_NUM];
                int moveCount = 0;
                while ((usiMove = posReader.Read()) != "\0")
                {
                    var move = USI.ParseUSIMove(usiMove);
                    if (move == BoardCoordinate.Null || !board.Update(move))    // 合法手チェック
                        return null;
                    moveList[moveCount++] = move;
                }

                while (board.Undo()) ;

                moves = moveList[0..moveCount].ToArray();
            }

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
