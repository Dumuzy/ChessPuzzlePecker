﻿namespace ChessLibrary
{
    public abstract class Piece
    {
        protected abstract bool IsValidPieceMove(Move move);
        public abstract bool IsValidGameMove(Move move, GameBoard board);
        public abstract Player Owner { get; set; }

        public override bool Equals(object obj)
        {
            
            if (obj == null)
            {
                return false;
            }
            if (obj.GetType() != this.GetType())
            {
                return false;
            }

            return Owner == ((dynamic)obj).Owner;
        }

    }
}