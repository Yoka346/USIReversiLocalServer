using System.Collections.ObjectModel;

namespace USITestClient
{
    enum USIOptionType
    {
        Check,
        Spin,
        Combo,
        Button,
        String,
        FileName
    }

    internal delegate void USIOptionEventHandler(USIOption sender, dynamic oldValue, dynamic newValue);

    /// <summary>
    /// USIプロトコルのオプション. 
    /// オプションの値の型はdynamic(動的型付け). Dictionaryで一括管理したいのでジェネリックにはしなかった.
    /// </summary>
    internal class USIOption
    {
        dynamic currentValue;
        List<dynamic> valueCandidates = new();   // オプションの値の候補. TypeがUSIOptionType.Comboのときに使う.
        readonly Func<string, dynamic> STRING_PARSER;  // 文字列をオプションの値に変換するメソッド.

        public USIOptionType Type { get; }

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
        /// オプションの値の候補.
        /// </summary>
        public ReadOnlyCollection<dynamic> ValueCandidates => new(valueCandidates);

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

                if (!this.currentValue.GetType().Equals(value.GetType())) // 型チェック.
                    throw new ArgumentException($"The type of CurrentValue is {this.currentValue?.GetType()}, but that of the specified value was {value?.GetType()}. They must be same.");

                if (this.MinValue?.CompareTo(value) > 0)
                    throw new ArgumentOutOfRangeException(nameof(value), $"The specified value was less than minimum value {this.MinValue}.");

                if (this.MaxValue?.CompareTo(value) < 0)
                    throw new ArgumentOutOfRangeException(nameof(value), $"The specified value was greater than maximum value {this.MaxValue}.");

                var oldValue = this.currentValue;
                this.currentValue = value;
                this.OnValueChanged?.Invoke(this, oldValue, this.currentValue);
            }
        }

        /// <summary>
        /// オプションの値を表す文字列.
        /// getterは単純にToStringメソッドを呼び出すだけ, setterでは受けたvalueをstringParserで文字列から実際の値に変換し, CurrentValueに代入する.
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

        public USIOption(int idx, USIOptionType type, int defaultValue) 
            : this(idx, type, defaultValue, null, null, s=>int.Parse(s)) { }

        public USIOption(int idx, int defaultValue, int min, int max) 
            : this(idx, USIOptionType.Spin, defaultValue, min, max) { }

        public USIOption(int idx, USIOptionType type, int defaultValue, int min, int max) 
            : this(idx, type, defaultValue, min, max, s => int.Parse(s)) { }

        public USIOption(int idx, USIOptionType type, long defaultValue)
            : this(idx, type, defaultValue, null, null, s => long.Parse(s)) { }

        public USIOption(int idx, USIOptionType type, long defaultValue, long min, long max)
            : this(idx, type, defaultValue, min, max, s => long.Parse(s)) { }

        public USIOption(int idx, USIOptionType type, float defaultValue)
            : this(idx, type, defaultValue, null, null, s => float.Parse(s)) { }

        public USIOption(int idx, USIOptionType type, float defaultValue, float min, float max)
            : this(idx, type, defaultValue, min, max, s => float.Parse(s)) { }

        public USIOption(int idx, string defaultValue)
            : this(idx, USIOptionType.String, defaultValue, null, null, s => s) { }

        public USIOption(int idx, USIOptionType type, string defaultValue)
            : this(idx, type, defaultValue, null, null, s => s) { }

        public USIOption(int idx, bool defaultValue)
        {
            this.Idx = idx;
            this.Type = USIOptionType.Check;
            this.DefaultValue = defaultValue;
            this.currentValue = defaultValue;
            this.MinValue = this.MaxValue = null;
            this.STRING_PARSER = s => bool.Parse(s);
        }

        USIOption(int idx, USIOptionType type, IComparable defaultValue, IComparable? min, IComparable? max, Func<string, dynamic> stringParser)
        {
            this.Idx = idx;
            this.Type = type;
            this.DefaultValue = defaultValue;
            this.currentValue = defaultValue;
            this.MinValue = min;
            this.MaxValue = max;
            this.STRING_PARSER = stringParser;
        }

        public void AddValueCandidates(dynamic value)
        {
            if (value is null)
                throw new ArgumentNullException("Value cannot be null.");

            if (!value.GetType().Equals(this.DefaultValue.GetType()))
                throw new ArgumentException($"The type of CurrentValue is {this.currentValue.GetType()}, but that of the specified value was {value.GetType()}. They must be same.");

            this.valueCandidates.Add(value);
        }
    }
}
