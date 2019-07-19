﻿using System;

namespace ChessLibrary.Pieces
{
    public class Rook : Piece
    {
        public override Player Owner { get; set; }

        protected override bool IsValidPieceMove(Move move)
        {
            int deltaX = move.GetAbsDeltaX();
            int deltaY = move.GetAbsDeltaY();
            if (deltaX == 0 && deltaY == 0)
            {
                return false;
            }
            return (deltaX == 0 || deltaY == 0);
        }

        public override bool IsValidGameMove(Move move, GameBoard board)
        {
            if (move == null)
            {
                throw new ArgumentNullException(nameof(move));
            }

            if (board == null)
            {
                throw new ArgumentNullException(nameof(board));
            }

            if (!IsValidPieceMove(move))
            {
                return false;
            }

            if (board[move.Destination].Owner == move.Player)
            {
                return false; // Can't take your own piece.
            }

            if (ChessUtilities.PlayerWillBeInCheck(move, board))
            {
                return false;
            }

            return !ChessUtilities.IsTherePieceInBetween(move, board);
        }
    }
}