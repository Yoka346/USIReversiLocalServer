
using static USITestClient.Reversi.Board;

namespace USITestClient.Reversi
{
    internal struct Move
    {
        public static Move Null { get; } = new Move(BoardCoordinate.Null, 0UL);

        public BoardCoordinate Coord { get; }
        public ulong Flipped { get; }

        public int CoordX { get { return (byte)this.Coord % BOARD_SIZE; } }
        public int CoordY { get { return (byte)this.Coord / BOARD_SIZE; } }

        public Move(string coord, ulong flipped) : this(StringToPosition(coord), flipped) { }

        public Move((int x, int y) coord, ulong flipped) : this(coord.x, coord.y, flipped) { }

        public Move(int x, int y, ulong flipped) : this((BoardCoordinate)(x + y * BOARD_SIZE), flipped) { }

        public Move(BoardCoordinate coord, ulong flipped)
        {
            this.Coord = coord;
            this.Flipped = flipped;
        }

        public override string ToString()
        {
            if (this.Coord == BoardCoordinate.Pass)
                return "pass";

            var posX = (char)('A' + (byte)this.Coord % BOARD_SIZE);
            var posY = (byte)this.Coord / BOARD_SIZE;
            return $"{char.ToUpper(posX)}{posY + 1}";
        }

        public override bool Equals(object? obj) => obj is Move && this == (Move)obj;

        // This method will not be used. I implemented this just to suppress a caution.
        public override int GetHashCode() => base.GetHashCode();

        public static bool operator ==(Move left, Move right) => (left.Coord == right.Coord) && left.Flipped == right.Flipped;

        public static bool operator !=(Move left, Move right) => !(left == right);

        public static BoardCoordinate StringToPosition(string coord)
        {
            if (coord.ToLower() == "pass")
                return BoardCoordinate.Pass;
            var posX = char.ToLower(coord[0]) - 'a';
            var posY = int.Parse(coord[1].ToString()) - 1;
            return (BoardCoordinate)(posX + posY * BOARD_SIZE);
        }
    }
}
