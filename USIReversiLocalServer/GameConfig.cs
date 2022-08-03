using System.Text.Json;

namespace USIReversiLocalServer
{
    /// <summary>
    /// 対局の設定.
    /// Jsonファイルからロードする.
    /// </summary>
    internal class GameConfig   // 各設定値はあらかじめデフォルト値をいれておく
                                // (ロードしたJsonファイルにすべての設定項目の値が記述されているとは限らないので).
    {
        /// <summary>
        /// 1ゲームごとに手番を入れ替えるか否か.
        /// </summary>
        public bool SwapPlayer { get; set; } = true;

        /// <summary>
        /// 序盤のBook.
        /// </summary>
        public string OpeningSfenBookPath { get; set; } = string.Empty;

        // 何手目までBookに従うかどうかはMinBookMoveNum以上MaxBookMoveNum以下の乱数で決める.
        /// <summary>
        /// Bookに従う手数の最小値.
        /// </summary>
        public int MinBookMoveNum { get; set; } = 10;

        /// <summary>
        /// Bookに従う手数の最大値.
        /// </summary>
        public int MaxBookMoveNum { get; set; } = 21;

        /// <summary>
        /// 秒読みオーバーの許容値(ms). 通信の遅延などを考慮して値を決める.
        /// </summary>
        public int ByoyomiToleranceMs { get; set; } = 10;

        public static GameConfig? Load(string path) => JsonSerializer.Deserialize<GameConfig>(File.ReadAllText(path));
        public void Save(string path)
            => File.WriteAllText(path, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }
}
