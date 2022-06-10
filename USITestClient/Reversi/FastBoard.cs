using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

using static USITestClient.Reversi.BitManipulations;

namespace USITestClient.Reversi
{
    public struct Bitboard
    {
        public ulong CurrentPlayer { get; set; }
        public ulong OpponentPlayer { get; set; }
        public ulong Empty => ~(this.CurrentPlayer | this.OpponentPlayer);
        public int EmptyCount => (int)PopCount(this.Empty);
        public int CurrentPlayerDiscCount => (int)PopCount(this.CurrentPlayer);
        public int OpponentPlayerDiscCount => (int)PopCount(this.OpponentPlayer);

        public Bitboard(ulong currentPlayer, ulong opponentPlayer)
        {
            this.CurrentPlayer = currentPlayer;
            this.OpponentPlayer = opponentPlayer;
        }

        public override bool Equals(object? obj) => obj is Bitboard && (Bitboard)obj == this;

        public override int GetHashCode() => base.GetHashCode();

        public static bool operator ==(Bitboard left, Bitboard right) 
            =>left.CurrentPlayer == right.CurrentPlayer && left.OpponentPlayer == right.OpponentPlayer;

        public static bool operator !=(Bitboard left, Bitboard right) => !(left == right);
    }

    /// <summary>
    /// Provides fast reversi board for searching.
    /// Source code reference: https://github.com/okuhara/edax-reversi-AVX/blob/master/src/board_sse.c
    /// Bit operations reference: http://www.amy.hi-ho.ne.jp/okuhara/bitboard.htm (Japanese document)
    /// </summary>
    internal class FastBoard     // board for searching
    {
        Bitboard bitboard;
        bool mobilityWasCalculated = false;
        ulong mobility;

        public DiscColor SideToMove { get; private set; }
        public DiscColor Opponent { get { return this.SideToMove ^ DiscColor.White; } }
        public int CurrentPlayerDiscCount => this.bitboard.CurrentPlayerDiscCount;
        public int OpponentPlayerDiscCount => this.bitboard.OpponentPlayerDiscCount;
        public int EmptySquareCount => this.bitboard.EmptyCount;

        public FastBoard() : this(new Board(DiscColor.Black)) { }

        public FastBoard(Board board) : this(board.SideToMove, board.GetBitBoard()) { }

        public FastBoard(DiscColor sideToMove, Bitboard bitboard)
        {
            Init(sideToMove, bitboard);
        }

        public FastBoard(FastBoard board)
        {
            this.bitboard = board.bitboard;
            this.SideToMove = board.SideToMove;
            this.mobilityWasCalculated = board.mobilityWasCalculated;
            this.mobility = board.mobility;
        }

        public static DiscColor GetOpponentColor(DiscColor color) => color ^ DiscColor.White;

        public void Init(DiscColor sideToMove, Bitboard bitboard)
        {
            this.bitboard = bitboard;
            this.SideToMove = sideToMove;
        }

        public Bitboard GetBitboard() => this.bitboard;

        public void SetBitboard(Bitboard bitboard)
        {
            this.bitboard = bitboard;
            this.mobilityWasCalculated = false;
        }

        public void CopyTo(FastBoard dest)
        {
            dest.bitboard = this.bitboard;
            dest.SideToMove = this.SideToMove;
        }

        public DiscColor GetDiscColor(BoardCoordinate pos)
        {
            var x = (int)pos;
            var sideToMove = (ulong)this.SideToMove + 1UL;
            var color = sideToMove * ((this.bitboard.CurrentPlayer >> x) & 1) + (sideToMove ^ 3) * ((this.bitboard.OpponentPlayer >> x) & 1);
            return (color != 0) ? (DiscColor)(color - 1) : DiscColor.Null;
        }

        public void PutCurrentPlayerDisc(BoardCoordinate pos)
        {
            var x = 1UL << (int)pos;
            this.bitboard.CurrentPlayer |= x;
            this.bitboard.OpponentPlayer &= ~x;
        }

        public void PutOpponentPlayerDisc(BoardCoordinate pos)
        {
            var x = 1UL << (int)pos;
            this.bitboard.OpponentPlayer |= x;
            this.bitboard.CurrentPlayer &= ~x;
        }

        public bool IsLegalMove(BoardCoordinate pos)
        {
            var x = 1UL << (int)pos;
            var mobility = GetCurrentPlayerMobility();
            return (PopCount(mobility) == 0 && pos == BoardCoordinate.Pass) || (mobility & x) != 0UL;
        }

        public void PutStoneWithoutFlip(DiscColor color, BoardCoordinate pos)
        {
            if (this.SideToMove == color)
            {
                var mask = 1UL << (int)pos;
                this.bitboard.CurrentPlayer |= mask;
                if ((this.bitboard.OpponentPlayer & mask) != 0)
                    this.bitboard.OpponentPlayer ^= mask;
            }
            else
            {
                var mask = 1UL << (int)pos;
                this.bitboard.OpponentPlayer |= mask;
                if ((this.bitboard.CurrentPlayer & mask) != 0)
                    this.bitboard.CurrentPlayer ^= mask;
            }
        }

        public ulong Update(BoardCoordinate coord)
        {
            var flipped = 0UL;
            if (coord != BoardCoordinate.Pass)
            {
                var x = 1UL << (byte)coord;
                flipped = CalculateFlippedDiscs((byte)coord);
                this.bitboard.OpponentPlayer ^= flipped;
                this.bitboard.CurrentPlayer |= (flipped | x);
            }
            SwitchSideToMove();
            return flipped;
        }

        public void Undo(Move move)
        {
            SwitchSideToMove();
            var x = 1UL << (byte)move.Coord;
            this.bitboard.CurrentPlayer ^= (move.Flipped | x);
            this.bitboard.OpponentPlayer |= move.Flipped;
        }

        public GameResult GetGameResult()
        {
            if (this.bitboard.EmptyCount != 0)
            {
                var mobility = GetCurrentPlayerMobility();
                if (PopCount(mobility) != 0 || PopCount(CalculateMobility(this.bitboard.OpponentPlayer, this.bitboard.CurrentPlayer)) != 0)
                    return GameResult.NotOver;
            }

            var diff = (int)PopCount(this.bitboard.CurrentPlayer) - (int)PopCount(this.bitboard.OpponentPlayer);
            if (diff > 0)
                return GameResult.Win;
            if (diff < 0)
                return GameResult.Loss;
            return GameResult.Draw;
        }

        public void SwitchSideToMove()
        {
            var tmp = this.bitboard.CurrentPlayer;
            this.bitboard.CurrentPlayer = this.bitboard.OpponentPlayer;
            this.bitboard.OpponentPlayer = tmp;
            this.mobilityWasCalculated = false;
            this.SideToMove ^= DiscColor.White;
        }

        public int GetNextMoves(BoardCoordinate[] positions) => GetNextPositionCandidates(positions.AsSpan());

        public int GetNextPositionCandidates(Span<BoardCoordinate> positions)
        {
            var mobility = GetCurrentPlayerMobility();
            var posCount = (int)PopCount(mobility);
            if (posCount == 0)
            {
                positions[0] = BoardCoordinate.Pass;
                return 1;
            }

            var mask = 1UL;
            var idx = 0;
            for (byte i = 0; idx < posCount; i++)
            {
                if ((mobility & mask) != 0)
                    positions[idx++] = (BoardCoordinate)i;
                mask <<= 1;
            }
            return posCount;
        }

        public int GetNextPositionsCandidatesNumAfter(BoardCoordinate pos)
        {
            var bitboard = this.bitboard;
            if (pos != BoardCoordinate.Pass)
            {
                var flipped = CalculateFlippedDiscs((int)pos);
                bitboard.CurrentPlayer |= (flipped | (1UL << (int)pos));
                bitboard.OpponentPlayer ^= flipped;
            }
            return (int)PopCount(CalculateMobility(bitboard.OpponentPlayer, bitboard.CurrentPlayer));
        }

        public ulong GetCurrentPlayerMobility()
        {
            if (!this.mobilityWasCalculated)
            {
                this.mobility = CalculateMobility(this.bitboard.CurrentPlayer, this.bitboard.OpponentPlayer);
                this.mobilityWasCalculated = true;
            }
            return this.mobility;
        }

        public ulong GetCurrentPlayerMobility(out int mobilityNum)
        {
            GetCurrentPlayerMobility();
            mobilityNum = (int)PopCount(this.mobility);
            return this.mobility;
        }

        static ulong CalculateMobility(ulong p, ulong o)
        {
            if (Sse2.X64.IsSupported && Avx2.IsSupported)
                return CalculateMobility_AVX2(p, o);
            else
                return CalculateMobility_SSE(p, o);
        }

        ulong CalculateFlippedDiscs(int pos)
        {
            if (Sse2.X64.IsSupported && Avx2.IsSupported)
                return CalculateFilippedDiscs_AVX2(this.bitboard.CurrentPlayer, this.bitboard.OpponentPlayer, pos);
            else
                return CalculateFlippedDiscs_SSE(this.bitboard.CurrentPlayer, this.bitboard.OpponentPlayer, pos);
        }

        static ulong CalculateMobility_AVX2(ulong p, ulong o)   // p is current player's board      o is opponent player's board
        {
            var shift = Vector256.Create(1UL, 8UL, 9UL, 7UL);
            var shift2 = Vector256.Create(2UL, 16UL, 18UL, 14UL);
            var flipMask = Vector256.Create(0x7e7e7e7e7e7e7e7eUL, 0xffffffffffffffffUL, 0x7e7e7e7e7e7e7e7eUL, 0x7e7e7e7e7e7e7e7eUL);

            var p4 = Avx2.BroadcastScalarToVector256(Sse2.X64.ConvertScalarToVector128UInt64(p));
            var maskedO4 = Avx2.And(Avx2.BroadcastScalarToVector256(Sse2.X64.ConvertScalarToVector128UInt64(o)), flipMask);
            var prefixLeft = Avx2.And(maskedO4, Avx2.ShiftLeftLogicalVariable(maskedO4, shift));
            var prefixRight = Avx2.ShiftRightLogicalVariable(prefixLeft, shift);

            var flipLeft = Avx2.And(maskedO4, Avx2.ShiftLeftLogicalVariable(p4, shift));
            var flipRight = Avx2.And(maskedO4, Avx2.ShiftRightLogicalVariable(p4, shift));
            flipLeft = Avx2.Or(flipLeft, Avx2.And(maskedO4, Avx2.ShiftLeftLogicalVariable(flipLeft, shift)));
            flipRight = Avx2.Or(flipRight, Avx2.And(maskedO4, Avx2.ShiftRightLogicalVariable(flipRight, shift)));
            flipLeft = Avx2.Or(flipLeft, Avx2.And(prefixLeft, Avx2.ShiftLeftLogicalVariable(flipLeft, shift2)));
            flipRight = Avx2.Or(flipRight, Avx2.And(prefixRight, Avx2.ShiftRightLogicalVariable(flipRight, shift2)));
            flipLeft = Avx2.Or(flipLeft, Avx2.And(prefixLeft, Avx2.ShiftLeftLogicalVariable(flipLeft, shift2)));
            flipRight = Avx2.Or(flipRight, Avx2.And(prefixRight, Avx2.ShiftRightLogicalVariable(flipRight, shift2)));

            var mobility4 = Avx2.ShiftLeftLogicalVariable(flipLeft, shift);
            mobility4 = Avx2.Or(mobility4, Avx2.ShiftRightLogicalVariable(flipRight, shift));
            var mobility2 = Sse2.Or(Avx2.ExtractVector128(mobility4, 0), Avx2.ExtractVector128(mobility4, 1));
            mobility2 = Sse2.Or(mobility2, Sse2.UnpackHigh(mobility2, mobility2));
            return Sse2.X64.ConvertToUInt64(mobility2) & ~(p | o);
        }

        static ulong CalculateMobility_SSE(ulong p, ulong o)    // p is current player's board      o is opponent player's board
        {
            var maskedO = o & 0x7e7e7e7e7e7e7e7eUL;
            var p2 = Vector128.Create(p, ByteSwap(p));   // byte swap = vertical mirror
            var maskedO2 = Vector128.Create(maskedO, ByteSwap(maskedO));
            var prefix = Sse2.And(maskedO2, Sse2.ShiftLeftLogical(maskedO2, 7));
            var prefix1 = maskedO & (maskedO << 1);
            var prefix8 = o & (o << 8);

            var flip = Sse2.And(maskedO2, Sse2.ShiftLeftLogical(p2, 7));
            var flip1 = maskedO & (p << 1);
            var flip8 = o & (p << 8);

            flip = Sse2.Or(flip, Sse2.And(maskedO2, Sse2.ShiftLeftLogical(flip, 7)));
            flip1 |= maskedO & (flip1 << 1);
            flip8 |= o & (flip8 << 8);

            flip = Sse2.Or(flip, Sse2.And(prefix, Sse2.ShiftLeftLogical(flip, 14)));
            flip1 |= prefix1 & (flip1 << 2);
            flip8 |= prefix8 & (flip8 << 16);

            flip = Sse2.Or(flip, Sse2.And(prefix, Sse2.ShiftLeftLogical(flip, 14)));
            flip1 |= prefix1 & (flip1 << 2);
            flip8 |= prefix8 & (flip8 << 16);

            var mobility2 = Sse2.ShiftLeftLogical(flip, 7);
            var mobility = (flip1 << 1) | (flip8 << 8);

            prefix = Sse2.And(maskedO2, Sse2.ShiftLeftLogical(maskedO2, 9));
            prefix1 >>= 1;
            prefix8 >>= 8;
            flip = Sse2.And(maskedO2, Sse2.ShiftLeftLogical(p2, 9));
            flip1 = maskedO & (p >> 1);
            flip8 = o & (p >> 8);
            flip = Sse2.Or(flip, Sse2.And(maskedO2, Sse2.ShiftLeftLogical(flip, 9)));
            flip1 |= maskedO & (flip1 >> 1);
            flip8 |= o & (flip8 >> 8);
            flip = Sse2.Or(flip, Sse2.And(prefix, Sse2.ShiftLeftLogical(flip, 18)));
            flip1 |= prefix1 & (flip1 >> 2);
            flip8 |= prefix8 & (flip8 >> 16);
            flip = Sse2.Or(flip, Sse2.And(prefix, Sse2.ShiftLeftLogical(flip, 18)));
            flip1 |= prefix1 & (flip1 >> 2);
            flip8 |= prefix8 & (flip8 >> 16);
            mobility2 = Sse2.Or(mobility2, Sse2.ShiftLeftLogical(flip, 9));
            mobility |= (flip1 >> 1) | (flip8 >> 8);

            if (Sse2.X64.IsSupported)
                mobility |= Sse2.X64.ConvertToUInt64(mobility2) | ByteSwap(Sse2.X64.ConvertToUInt64(Sse2.UnpackHigh(mobility2, mobility2)));
            else
                mobility |= mobility2.GetElement(0) | ByteSwap(Sse2.UnpackHigh(mobility2, mobility2).GetElement(0));
            return mobility & ~(p | o);
        }

        static ulong CalculateFilippedDiscs_AVX2(ulong p, ulong o, int pos)    // p is current player's board      o is opponent player's board
        {
            var shift = Vector256.Create(1UL, 8UL, 9UL, 7UL);
            var shift2 = Vector256.Create(2UL, 16UL, 18UL, 14UL);
            var flipMask = Vector256.Create(0x7e7e7e7e7e7e7e7eUL, 0xffffffffffffffffUL, 0x7e7e7e7e7e7e7e7eUL, 0x7e7e7e7e7e7e7e7eUL);

            var x = 1UL << pos;
            var x4 = Avx2.BroadcastScalarToVector256(Sse2.X64.ConvertScalarToVector128UInt64(x));
            var p4 = Avx2.BroadcastScalarToVector256(Sse2.X64.ConvertScalarToVector128UInt64(p));
            var maskedO4 = Avx2.And(Avx2.BroadcastScalarToVector256(Sse2.X64.ConvertScalarToVector128UInt64(o)), flipMask);
            var prefixLeft = Avx2.And(maskedO4, Avx2.ShiftLeftLogicalVariable(maskedO4, shift));
            var prefixRight = Avx2.ShiftRightLogicalVariable(prefixLeft, shift);

            var flipLeft = Avx2.And(Avx2.ShiftLeftLogicalVariable(x4, shift), maskedO4);
            var flipRight = Avx2.And(Avx2.ShiftRightLogicalVariable(x4, shift), maskedO4);
            flipLeft = Avx2.Or(flipLeft, Avx2.And(maskedO4, Avx2.ShiftLeftLogicalVariable(flipLeft, shift)));
            flipRight = Avx2.Or(flipRight, Avx2.And(maskedO4, Avx2.ShiftRightLogicalVariable(flipRight, shift)));
            flipLeft = Avx2.Or(flipLeft, Avx2.And(prefixLeft, Avx2.ShiftLeftLogicalVariable(flipLeft, shift2)));
            flipRight = Avx2.Or(flipRight, Avx2.And(prefixRight, Avx2.ShiftRightLogicalVariable(flipRight, shift2)));
            flipLeft = Avx2.Or(flipLeft, Avx2.And(prefixLeft, Avx2.ShiftLeftLogicalVariable(flipLeft, shift2)));
            flipRight = Avx2.Or(flipRight, Avx2.And(prefixRight, Avx2.ShiftRightLogicalVariable(flipRight, shift2)));

            var outflankLeft = Avx2.And(p4, Avx2.ShiftLeftLogicalVariable(flipLeft, shift));
            var outflankRight = Avx2.And(p4, Avx2.ShiftRightLogicalVariable(flipRight, shift));
            flipLeft = Avx2.AndNot(Avx2.CompareEqual(outflankLeft, Vector256<ulong>.Zero), flipLeft);
            flipRight = Avx2.AndNot(Avx2.CompareEqual(outflankRight, Vector256<ulong>.Zero), flipRight);
            var flip4 = Avx2.Or(flipLeft, flipRight);
            var flip2 = Sse2.Or(Avx2.ExtractVector128(flip4, 0), Avx2.ExtractVector128(flip4, 1));
            flip2 = Sse2.Or(flip2, Sse2.UnpackHigh(flip2, flip2));
            return Sse2.X64.ConvertToUInt64(flip2);
        }

        static ulong CalculateFlippedDiscs_SSE(ulong p, ulong o, int pos)    // p is current player's board      o is opponent player's board
        {
            var x = 1UL << pos;
            var maskedO = o & 0x7e7e7e7e7e7e7e7eUL;
            var x2 = Vector128.Create(x, ByteSwap(x));   // byte swap = vertical mirror
            var p2 = Vector128.Create(p, ByteSwap(p));
            var maskedO2 = Vector128.Create(maskedO, ByteSwap(maskedO));

            var flip7 = Sse2.And(maskedO2, Sse2.ShiftLeftLogical(x2, 7));
            var flip1Left = maskedO & (x << 1);
            var flip8Left = o & (x << 8);
            flip7 = Sse2.Or(flip7, Sse2.And(maskedO2, Sse2.ShiftLeftLogical(flip7, 7)));
            flip1Left |= maskedO & (flip1Left << 1);
            flip8Left |= o & (flip8Left << 8);

            var prefix = Sse2.And(maskedO2, Sse2.ShiftLeftLogical(maskedO2, 7));
            var prefix1 = maskedO & (maskedO << 1);
            var prefix8 = o & (o << 8);

            flip7 = Sse2.Or(flip7, Sse2.And(prefix, Sse2.ShiftLeftLogical(flip7, 14)));
            flip1Left |= prefix1 & (flip1Left << 2);
            flip8Left |= prefix8 & (flip8Left << 16);
            flip7 = Sse2.Or(flip7, Sse2.And(prefix, Sse2.ShiftLeftLogical(flip7, 14)));
            flip1Left |= prefix1 & (flip1Left << 2);
            flip8Left |= prefix8 & (flip8Left << 16);

            prefix = Sse2.And(maskedO2, Sse2.ShiftLeftLogical(maskedO2, 9));
            prefix1 >>= 1;
            prefix8 >>= 8;

            var flip9 = Sse2.And(maskedO2, Sse2.ShiftLeftLogical(x2, 9));
            var flip1Right = maskedO & (x >> 1);
            var flip8Right = o & (x >> 8);
            flip9 = Sse2.Or(flip9, Sse2.And(maskedO2, Sse2.ShiftLeftLogical(flip9, 9)));
            flip1Right |= maskedO & (flip1Right >> 1);
            flip8Right |= o & (flip8Right >> 8);
            flip9 = Sse2.Or(flip9, Sse2.And(prefix, Sse2.ShiftLeftLogical(flip9, 18)));
            flip1Right |= prefix1 & (flip1Right >> 2);
            flip8Right |= prefix8 & (flip8Right >> 16);
            flip9 = Sse2.Or(flip9, Sse2.And(prefix, Sse2.ShiftLeftLogical(flip9, 18)));
            flip1Right |= prefix1 & (flip1Right >> 2);
            flip8Right |= prefix8 & (flip8Right >> 16);

            var outflank7 = Sse2.And(p2, Sse2.ShiftLeftLogical(flip7, 7));
            var outflankLeft1 = p & (flip1Left << 1);
            var outflankLeft8 = p & (flip8Left << 8);
            var outflank9 = Sse2.And(p2, Sse2.ShiftLeftLogical(flip9, 9));
            var outflankRight1 = p & (flip1Right >> 1);
            var outflankRight8 = p & (flip8Right >> 8);

            if (Sse41.IsSupported)
            {
                flip7 = Sse2.AndNot(Sse41.CompareEqual(outflank7, Vector128<ulong>.Zero), flip7);
                flip9 = Sse2.AndNot(Sse41.CompareEqual(outflank9, Vector128<ulong>.Zero), flip9);
            }
            else
            {
                flip7 = Sse2.And(Sse2.CompareNotEqual(outflank7.AsDouble(), Vector128<ulong>.Zero.AsDouble()).AsUInt64(), flip7);
                flip9 = Sse2.And(Sse2.CompareNotEqual(outflank9.AsDouble(), Vector128<ulong>.Zero.AsDouble()).AsUInt64(), flip9);
            }

            if (outflankLeft1 == 0)
                flip1Left = 0UL;
            if (outflankLeft8 == 0)
                flip8Left = 0UL;
            if (outflankRight1 == 0)
                flip1Right = 0UL;
            if (outflankRight8 == 0)
                flip8Right = 0UL;

            var flippedDiscs2 = Sse2.Or(flip7, flip9);
            var flippedDiscs = flip1Left | flip8Left | flip1Right | flip8Right;

            if (Sse2.X64.IsSupported)
                flippedDiscs |= Sse2.X64.ConvertToUInt64(flippedDiscs2)
                             | ByteSwap(Sse2.X64.ConvertToUInt64(Sse2.UnpackHigh(flippedDiscs2, flippedDiscs2)));
            else
                flippedDiscs |= flippedDiscs2.GetElement(0) | ByteSwap(Sse2.UnpackHigh(flippedDiscs2, flippedDiscs2).GetElement(0));
            return flippedDiscs;
        }
    }
}
