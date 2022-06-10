using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using USITestClient.Reversi;

namespace USITestClient
{
    /// <summary>
    /// USIプロトコルに準拠した思考エンジンの基底クラス.
    /// </summary>
    internal abstract class USIEngine
    {
        /// <summary>
        /// エンジンのオプション.
        /// </summary>
        Dictionary<string, USIEngine> options = new();

        /// <summary>
        /// 盤面を設定する. positionコマンドが呼ばれたときに実行されるメソッド.
        /// </summary>
        /// <param name="rootBoard">初期盤面.</param>
        /// <param name="currentBoard">現在の盤面.</param>
        /// <param name="moves">現在の盤面に至るまでの着手.</param>
        public abstract void SetBoard(Board rootBoard, Board currentBoard, IEnumerable<Move> moves);

        /// <summary>
        /// エンジンのオプションを設定する. setoptionコマンドが呼ばれたときに実行されるメソッド.
        /// </summary>
        /// <param name="optionID">オプションのID.</param>
        public void SetOption(string optionID) => SetOption(optionID, string.Empty);

        /// <summary>
        /// エンジンのオプションを設定する. setoptionコマンドが呼ばれたときに実行されるメソッド.
        /// </summary>
        /// <param name="optionID">オプションのID.</param>
        /// <param name="value">オプションの値.</param>
        public abstract void SetOption(string optionID, string value);

        /// <summary>
        /// 着手を決定する. goコマンドが呼ばれたときに実行されるメソッド.
        /// </summary>
        /// <param name="board">思考開始局面.</param>
        /// <param name="byoyomi">秒読み時間.</param>
        /// <returns>着手.</returns>
        public abstract BoardCoordinate GenerateMove(Board board, int byoyomi);   
    }
}
