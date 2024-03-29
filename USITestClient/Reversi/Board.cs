﻿using System.Text;

namespace USITestClient.Reversi
{
    internal enum GameResult : sbyte
    {
        Win = 1,
        Loss = -1,
        Draw = 0,
        NotOver = -2,
        Resigned = -3
    }

    internal enum DiscColor : sbyte
    {
        Black = 0,
        Null = 2,
        White = 1
    }

    internal enum BoardCoordinate : byte
    {
        A1, B1, C1, D1, E1, F1, G1, H1,
        A2, B2, C2, D2, E2, F2, G2, H2,
        A3, B3, C3, D3, E3, F3, G3, H3,
        A4, B4, C4, D4, E4, F4, G4, H4,
        A5, B5, C5, D5, E5, F5, G5, H5,
        A6, B6, C6, D6, E6, F6, G6, H6,
        A7, B7, C7, D7, E7, F7, G7, H7,
        A8, B8, C8, D8, E8, F8, G8, H8,
        Pass, Resign, Null
    }

    internal class Board
    {
        public const int BOARD_SIZE = 8;
        public const int SQUARE_NUM = BOARD_SIZE * BOARD_SIZE;
        public const int MAX_MOVE_CANDIDATE_COUNT = 46;
        const int MOVE_HISTORY_STACK_SIZE = 96;

        FastBoard fastBoard;
        Stack<Move> moveHistory = new(MOVE_HISTORY_STACK_SIZE);

        public DiscColor SideToMove { get { return fastBoard.SideToMove; } }
        public DiscColor Opponent { get { return fastBoard.Opponent; } }

        public Board() : this(DiscColor.Black, 0UL, 0UL) { }

        public Board(DiscColor sideToMove) : this (sideToMove, 0UL, 0UL) { }

        public Board(DiscColor sideToMove, Bitboard bitboard):this(sideToMove, bitboard.CurrentPlayer, bitboard.OpponentPlayer) { }

        public Board(DiscColor sideToMove, ulong currentPlayerBoard, ulong opponentPlayerBoard)
            => this.fastBoard = new FastBoard(sideToMove, new Bitboard(currentPlayerBoard, opponentPlayerBoard));

        public Board(Board board)
        {
            this.fastBoard = new FastBoard(board.fastBoard);
            this.moveHistory = this.moveHistory.Copy();
        }

        /// <summary>
        /// 交差配置の初期盤面を作成する.
        /// </summary>
        public static Board CreateCrossBoard()
        {
            var board = new Board();
            board.Put(DiscColor.Black, BoardCoordinate.E4);
            board.Put(DiscColor.Black, BoardCoordinate.D5);
            board.Put(DiscColor.White, BoardCoordinate.D4);
            board.Put(DiscColor.White, BoardCoordinate.E5);
            return board;
        }

        public void Init(DiscColor sideToMove, Bitboard bitboard) => this.fastBoard = new FastBoard(sideToMove, bitboard);

        public FastBoard GetFastBoard() => new FastBoard(this.fastBoard);

        public ulong GetBitboard(DiscColor color)
        {
            var bitboard = this.fastBoard.GetBitboard();
            return (this.SideToMove == color) ? bitboard.CurrentPlayer : bitboard.OpponentPlayer;
        }

        public Bitboard GetBitBoard() => this.fastBoard.GetBitboard();

        public int GetDiscCount(DiscColor color)
            => (this.SideToMove == color) ? this.fastBoard.CurrentPlayerDiscCount : this.fastBoard.OpponentPlayerDiscCount;
        
        public int GetEmptyCount() => this.fastBoard.EmptySquareCount;

        public DiscColor GetColor(int coordX, int coordY) => GetDiscColor((BoardCoordinate)(coordX + coordY * BOARD_SIZE));

        public DiscColor GetDiscColor(BoardCoordinate coord) => this.fastBoard.GetDiscColor(coord);

        public IEnumerable<BoardCoordinate> EnumerateMoveHistory() => from move in this.moveHistory.Reverse() select move.Coord;

        public DiscColor[,] GetDiscsArray()
        {
            var discs = new DiscColor[BOARD_SIZE, BOARD_SIZE];
            for (var i = 0; i < discs.GetLength(0); i++)
                for (var j = 0; j < discs.GetLength(1); j++)
                    discs[i, j] = DiscColor.Null;
            var currentPlayer = this.SideToMove;
            var opponentPlayer = this.SideToMove ^ DiscColor.White;
            var bitboard = this.fastBoard.GetBitboard();
            var p = bitboard.CurrentPlayer;
            var o = bitboard.OpponentPlayer;

            var mask = 1UL;
            for(var y = 0; y < discs.GetLength(0); y++)
                for(var x = 0; x < discs.GetLength(1); x++)
                {
                    if ((p & mask) != 0)
                        discs[x, y] = currentPlayer;
                    else if ((o & mask) != 0)
                        discs[x, y] = opponentPlayer;
                    mask <<= 1;
                }
            return discs;
        }

        public void SwitchSideToMove() => this.fastBoard.SwitchSideToMove();

        public void Put(DiscColor color, string coord) => Put(color, StringToCoord(coord));

        public void Put(DiscColor color, int coordX, int coordY) => Put(color, (BoardCoordinate)(coordX + coordY * BOARD_SIZE));

        public void Put(DiscColor color, BoardCoordinate coord)
        {
            this.moveHistory.Clear();
            if (color == this.SideToMove)
                this.fastBoard.PutCurrentPlayerDisc(coord);
            else
                this.fastBoard.PutOpponentPlayerDisc(coord);
        }

        public bool Update(BoardCoordinate coord)
        {
            if (!this.fastBoard.IsLegalMove(coord))
                return false;
            this.moveHistory.Push(new Move(coord, this.fastBoard.Update(coord)));
            return true;
        }

        public bool Undo()
        {
            if (this.moveHistory.Count == 0)
                return false;
            this.fastBoard.Undo(this.moveHistory.Pop());
            return true;
        }

        public bool IsLegalMove(BoardCoordinate move) => fastBoard.IsLegalMove(move);

        public BoardCoordinate[] GetNextMoves()
        {
            var moves = new BoardCoordinate[MAX_MOVE_CANDIDATE_COUNT];
            var count = this.fastBoard.GetNextMoves(moves);
            return moves[..count];
        }

        public GameResult GetGameResult(DiscColor color)
        {
            var result = this.fastBoard.GetGameResult();
            if (result == GameResult.NotOver)
                return result;
            return (color == this.SideToMove) ? result : (GameResult)(-(int)result);
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append("  ");
            for (var i = 0; i < BOARD_SIZE; i++)
                sb.Append($"{(char)('A' + i)} ");

            var bitboard = this.fastBoard.GetBitboard();
            var p = bitboard.CurrentPlayer;
            var o = bitboard.OpponentPlayer;
            var mask = 1UL << (SQUARE_NUM - 1);
            for (var y = BOARD_SIZE - 1; y >= 0; y--)
            {
                sb.Append($"\n{y + 1} ");
                var line = new StringBuilder();
                for (var x = 0; x < BOARD_SIZE; x++)
                {
                    if ((p & mask) != 0)
                        line.Append((this.SideToMove == DiscColor.Black) ? "X " : "O ");
                    else if ((o & mask) != 0)
                        line.Append((this.SideToMove != DiscColor.Black) ? "X " : "O ");    
                    else
                        line.Append(". ");
                    mask >>= 1;
                }
                sb.Append(line.ToString().Reverse().ToArray());
            }
            return sb.ToString();
        }

        static BoardCoordinate StringToCoord(string pos)
        {
            var posX = char.ToLower(pos[0]) - 'a';
            var posY = int.Parse(pos[1].ToString()) - 1;
            return (BoardCoordinate)(posX + posY * BOARD_SIZE);
        }
    }
}
