using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using USITestClient.Reversi;

namespace USITestClient
{
    /// <summary>
    /// 乱数で着手を決定するエンジン.
    /// </summary>
    internal class RandomEngine : USIEngine
    {
        const int DELAY = 100;
        const int DEFAULT_SEED = 1024;

        Board board;
        Random rand;

        public RandomEngine() : base("Random Mover", "Yoka346")
        {
            this.board = Board.CreateCrossBoard();
            this.rand = new Random(DEFAULT_SEED);
            this.options["rand_seed"] = new USIOption(0, DEFAULT_SEED, 0, int.MaxValue);
            this.options["rand_seed"].OnValueChanged += RandomEngine_OnSeedChanged;
        }

        public override void Init() { }

        public override void Quit() { }

        public override void ReadyForGame() { }

        public override void SetBoard(Board rootBoard, Board currentBoard, IEnumerable<BoardCoordinate> moves)
            => this.board = new Board(currentBoard);

        public override void StartNewGame() { }

        public override BoardCoordinate GenerateMove(int byoyomi = -1)
        {
            if(byoyomi != -1)
                Thread.Sleep(Math.Max(byoyomi - DELAY, 0));  // 秒読み時間が正しく渡されているかチェックするためにわざとスリープする.
            var moves = this.board.GetNextMoves();
            return moves[rand.Next(moves.Length)];
        }

        public override void StopThinking() { }

        void RandomEngine_OnSeedChanged(USIOption sender, dynamic oldValue, dynamic newValue)
            => this.rand = new Random(newValue);
    }
}
