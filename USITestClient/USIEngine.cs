using System.Text;

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
        protected Dictionary<string, USIOption> options = new();

        public string Name { get; }
        public string Author { get; }

        public USIEngine(string name, string author) => (this.Name, this.Author) = (name, author);

        public bool HasOption(string name) => options.ContainsKey(name);

        /// <summary>
        /// オプションを列挙する. optionコマンドの形式(option name <optionname> type <optiontype> <parameter...>)で列挙する.
        /// </summary>
        /// <returns></returns>
        public IEnumerable<string> EnumerateOptionString()
        {
            var sb = new StringBuilder();
            foreach (var option in this.options.OrderBy(opt => opt.Value.Idx))
            {
                var opt = option.Value;
                sb.Append($"option name ").Append(option.Key).Append(" type ").Append(opt.Type.ToString().ToLower());
                sb.Append(" default ").Append(opt.DefaultValue);

                if (opt.Type == USIOptionType.Spin)
                    sb.Append(" min ").Append(opt.MinValue).Append(" max ").Append(opt.MaxValue);
                else if (opt.Type == USIOptionType.Combo)
                    foreach (var candidate in opt.ValueCandidates)
                        sb.Append(" var ").Append(candidate);

                yield return sb.ToString();
                sb.Clear();
            }
        }

        /// <summary>
        /// エンジンのオプションを設定する. setoptionコマンドが呼ばれたときに実行されるメソッド.
        /// </summary>
        /// <param name="name">オプションの名前.</param>
        /// <param name="value">オプションの値.</param>
        public void SetOption(string name, string value)
            => this.options[name].ValueString = value;

        /// <summary>
        /// usiコマンドが呼ばれたときに実行されるメソッド.
        /// </summary>
        public abstract void Init();

        /// <summary>
        /// quitコマンドが呼ばれたときに実行されるメソッド.
        /// </summary>
        public abstract void Quit();

        /// <summary>
        /// isreadyコマンドが呼ばれたときに実行されるメソッド.
        /// </summary>
        public abstract void ReadyForGame();

        /// <summary>
        /// usinewgameコマンドが呼ばれたときに実行されるメソッド.
        /// </summary>
        public abstract void StartNewGame();

        /// <summary>
        /// 盤面を設定する. positionコマンドが呼ばれたときに実行されるメソッド.
        /// </summary>
        /// <param name="rootBoard">初期盤面.</param>
        /// <param name="currentBoard">現在の盤面.</param>
        /// <param name="moves">現在の盤面に至るまでの着手.</param>
        public abstract void SetBoard(Board rootBoard, Board currentBoard, IEnumerable<BoardCoordinate> moves);

        /// <summary>
        /// 着手を決定する. goコマンドが呼ばれたときに実行されるメソッド.
        /// </summary>
        /// <param name="board">思考開始局面.</param>
        /// <param name="byoyomi">秒読み時間.</param>
        /// <returns>着手.</returns>
        public abstract BoardCoordinate GenerateMove(int byoyomi = -1);

        /// <summary>
        /// 実行中の思考を停止する. stopコマンドが呼ばれたときに実行されるメソッド.
        /// </summary>
        public abstract void StopThinking();
    }
}
