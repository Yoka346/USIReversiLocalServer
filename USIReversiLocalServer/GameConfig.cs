using System.Text.Json;

namespace USITestClient
{
    /// <summary>
    /// 対局の設定.
    /// Jsonファイルからロードする.
    /// </summary>
    internal class GameConfig   // 各設定値はあらかじめデフォルト値をいれておく
                                // (ロードしたJsonファイルにすべての設定項目の値が記述されているとは限らないので).
    {
        /// <summary>
        /// 序盤のBook.
        /// </summary>
        public string OpeningSfenBookPath { get; private set; } = string.Empty;

        // 何手目までBookに従うかどうかはMinBookMoveNum以上MaxBookMoveNum以下の乱数で決める.
        /// <summary>
        /// Bookに従う手数の最小値.
        /// </summary>
        public int MinBookMoveNum { get; private set; } = 10;

        /// <summary>
        /// Bookに従う手数の最大値.
        /// </summary>
        public int MaxBookMoveNum { get; private set; } = 21;

        public GameConfig? Load(string path) => JsonSerializer.Deserialize<GameConfig>(File.ReadAllText(path));
        public void Save(string path)
            => File.WriteAllText(path, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }
}
