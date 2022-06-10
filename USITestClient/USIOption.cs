namespace USITestClient
{
    internal delegate void USIOptionEventHandler(USIOption sender, dynamic oldValue, dynamic newValue);

    /// <summary>
    /// USIプロトコルのオプション. 
    /// オプションの値の型はdynamic(動的型付け). SortedDictionaryで一括管理したいのでジェネリックにはしなかった.
    /// </summary>
    internal class USIOption
    {
        dynamic currentValue;   
        readonly Func<string, dynamic> STRING_PARSER;  // 文字列をオプションの値に変換するメソッド.

        /// <summary>
        /// オプションのデフォルト値.
        /// </summary>
        public dynamic DefaultValue { get; } 
        
        /// <summary>
        /// オプションの最小値.
        /// </summary>
        public IComparable? MinValue { get; }

        /// <summary>
        /// オプションの最大値.
        /// </summary>
        public IComparable? MaxValue { get; }

        /// <summary>
        /// 辞書に追加したときの順番. C#では要素を追加した順番を保持するOrderedDictionaryが存在するが, ジェネリックではないので, このインデックスを用いてDictionaryへの追加順を保持する.
        /// 詳しくはUSI.csやUSIEngine.csを参照.
        /// </summary>
        public int Idx { get; }

        /// <summary>
        /// 現在のオプション値.
        /// setterで型チェックと範囲チェックを行う. 
        /// </summary>
        public dynamic CurrentValue
        {
            get => this.currentValue;

            set
            {
                if(value is null)   
                    throw new ArgumentNullException("Value cannot be null.");

                if (!this.currentValue.Equals(value.GetType())) // 型チェック.
                    throw new ArgumentException($"The type of CurrentValue is {this.currentValue?.GetType()}, but that of the specified value was {value?.GetType()}. They must be same.");

                if (this.MinValue?.CompareTo(value) < 0)
                    throw new ArgumentOutOfRangeException($"The specified value was less than minimum value {this.MinValue}.");

                if (this.MaxValue?.CompareTo(value) > 0)
                    throw new ArgumentOutOfRangeException($"The specified value was greater than maximum value {this.MaxValue}.");

                var oldValue = this.currentValue;
                this.currentValue = value;
                this.OnValueChanged?.Invoke(this, oldValue, this.currentValue);
            }
        }

        /// <summary>
        /// オプションの値を表す文字列.
        /// getterは単純にToStringメソッドを呼び出すだけ, getterでは受けたvalueをstringParserで文字列に変換し, CurrentValueに代入する.
        /// </summary>
        public string ValueString
        {
            get => this.currentValue.ToString();

            set => this.CurrentValue = this.STRING_PARSER(value);
        }

        /// <summary>
        /// オプションの値が変更されたときに発火するイベント.
        /// </summary>
        public event USIOptionEventHandler? OnValueChanged;

        // 外からのオブジェクトの生成はCreateOption<T>メソッドで行うのでコンストラクタはprivate.
        // コンストラクタから直接生成することを許してしまうと, defaultValue, min, max それぞれに別々の型の値を入れることが可能になってしまうため.
        USIOption(int idx, dynamic defaultValue, IComparable? min, IComparable? max, Func<string, dynamic> stringParser)    
        {
            this.Idx = idx;
            this.DefaultValue = defaultValue;
            this.currentValue = defaultValue;
            this.MinValue = (min is not null) ? min : null;
            this.MaxValue = (max is not null) ? max : null;
            this.STRING_PARSER = stringParser;
        }

        /// <summary>
        /// USIOptionを生成する. 
        /// </summary>
        /// <param name="defaultValue">オプションのデフォルト値.</param>
        /// <returns></returns>
        public static USIOption CreateOption<T>(int idx, T defaultValue, Func<string, T> stringParser)
            => new USIOption(idx, defaultValue, null, null, x => stringParser);

        /// <summary>
        /// USIOptionを生成する.
        /// </summary>
        /// <typeparam name="T">オプションの型(制約: IComparableオブジェクト)</typeparam>
        /// <param name="defaultValue">オプションの比較可能なデフォルト値.</param>
        /// <param name="min">オプションの最小値.</param>
        /// <param name="max">オプションの最大値.</param>
        /// <returns></returns>
        public static USIOption CreateOption<T>(int idx, T defaultValue, T min, T max, Func<string, T> stringParser) where T : IComparable
            => new USIOption(idx, defaultValue, min, max, x => stringParser);

        // 以下, よく使う型に関してはあらかじめUSIOptionオブジェクトを生成するメソッドを用意する. 上とほぼ同じなのでメソッドのサマリーは省略.

        public static USIOption CreateOption(int idx, int defaultValue, int min, int max)
            => CreateOption(idx, defaultValue, min, max, int.Parse);

        public static USIOption CreateOption(int idx, long defaultValue, long min, long max)
            => CreateOption(idx, defaultValue, min, max, long.Parse);

        public static USIOption CreateOption(int idx, float defaultValue, float min, float max)
            => CreateOption(idx, defaultValue, min, max, float.Parse);

        public static USIOption CreateOption(int idx, string defaultValue, string min, string max)
            => CreateOption(idx, defaultValue, min, max, x => x);
    }
}
