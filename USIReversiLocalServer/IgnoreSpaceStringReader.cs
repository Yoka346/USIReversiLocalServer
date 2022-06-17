using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace USIReversiLocalServer
{
    /// <summary>
    /// 空白を無視してstringを読むクラス.
    /// </summary>
    internal struct IgnoreSpaceStringReader
    {
        readonly string STR;
        int position = 0;

        public int Position => position;

        public IgnoreSpaceStringReader(string str) => this.STR = str;
        public IgnoreSpaceStringReader(ReadOnlySpan<char> str) => this.STR = str.ToString();

        public int Peek() => (this.position >= this.STR.Length - 1) ? -1 : this.STR[this.position + 1];

        /// <summary>
        /// offsetから空白文字以外の文字が始まる位置を探し、そこから次の空白文字までの部分文字列を返す.
        /// </summary>
        /// <param name="offset"></param>
        /// <returns></returns>
        public ReadOnlySpan<char> Read(int offset = 0)
        {
            var canRead = false;
            for(; this.position < this.STR.Length; this.position++)
                if (this.STR[this.position] != ' ')
                {
                    canRead = true;
                    break;
                }

            if (!canRead)
                return "\0".AsSpan();

            int count;
            for (count = 1; this.position + count < this.STR.Length && this.STR[count] != ' '; count++) ;

            var start = this.position;
            this.position += count;
            return this.STR.AsSpan(start, count);
        }

        /// <summary>
        /// 現在の位置 + offsetから最後まで文字列を読む. 空白も含む.
        /// </summary>
        /// <param name="offset"></param>
        /// <returns></returns>
        public ReadOnlySpan<char> ReadToEnd(int offset = 0)
        {
            if (this.position == this.STR.Length)
                return string.Empty.AsSpan();

            var ret = this.STR.AsSpan(this.position, this.STR.Length - this.position);
            this.position = this.STR.Length;
            return ret;
        }
    }
}
