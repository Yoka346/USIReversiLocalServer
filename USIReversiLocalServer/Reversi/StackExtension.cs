using System;
using System.Collections.Generic;
using System.Linq;

namespace USIReversiGameServer.Reversi
{
    internal static class StackExtension
    {
        public static Stack<T> Copy<T>(this Stack<T> stack)
        {
            var copied = new Stack<T>();
            foreach (var n in stack.Reverse())
                copied.Push(n);
            return copied;
        }
    }
}
