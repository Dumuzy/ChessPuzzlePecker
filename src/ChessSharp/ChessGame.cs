﻿using ChessSharp.Pieces;
using ChessSharp.SquareData;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AwiUtils;

namespace ChessSharp
{
    /// <summary>Represents the chess game.</summary>
    public class ChessGame : IDeepCloneable<ChessGame>
    {
        /// <summary>Gets <see cref="Piece"/> in a specific square.</summary>
        /// <param name="file">The <see cref="Linie"/> of the square.</param>
        /// <param name="rank">The <see cref="Rank"/> of the square.</param>
        public Piece this[Linie file, Rank rank] => Board[(int)rank][(int)file];

        public Piece this[Square sq] => Board[(int)sq.Rank][(int)sq.File];

        public string Fen
        {
            get
            {
                var fen = "";
                for (Rank ra = Rank.Eighth; ra >= Rank.First; --ra)
                {
                    int spaceCount = 0;
                    for (Linie li = Linie.A; li <= Linie.H; ++li)
                    {
                        if (this[li, ra] == null)
                            spaceCount++;
                        else
                        {
                            if (spaceCount != 0)
                                fen += spaceCount;
                            spaceCount = 0;
                            var piece = this[li, ra];
                            fen += ChessUtilities.FenCharOfPiece(piece);
                        }
                        if (li == Linie.H && spaceCount != 0)
                            fen += spaceCount;
                    }
                    if (ra != Rank.First)
                        fen += "/";
                }
                fen += WhoseTurn == Player.White ? " w " : " b ";
                fen += FenCastleState 
                    + " - 1 1";  // TODO
                return fen;
            }
        }

        public string ShortFen => Fen.Split()[0];

        public void SetPiece(Square sq, Piece p) => Board[(int)sq.Rank][(int)sq.File] = p;

        /// <summary>Gets a list of the game moves.</summary>
        public Li<Move> Moves { get; private set; }

        /// <summary>Gets a 2D array of <see cref="Piece"/>s in the board.</summary>
        public Piece[][] Board { get; private set; }

        /// <summary>Gets the <see cref="Player"/> who has turn.</summary>
        public Player WhoseTurn { get; private set; } = Player.White;

        /// <summary>Gets the current <see cref="ChessSharp.GameState"/>.</summary>
        public GameState GameState { get; private set; }

        public bool IsPromotionMove(Square? source, Square target)
        {
            bool isPromotion = false;
            if (source != null && (target.Rank == Rank.First || target.Rank == Rank.Eighth))
            {
                Piece? piece = this[source.Value];
                isPromotion = piece != null && piece.GetType() == typeof(Pawn);
            }
            return isPromotion;
        }


        internal bool CanWhiteCastleKingSide { get; set; } = true;
        internal bool CanWhiteCastleQueenSide { get; set; } = true;
        internal bool CanBlackCastleKingSide { get; set; } = true;
        internal bool CanBlackCastleQueenSide { get; set; } = true;


        /// <summary>Initializes a new instance of <see cref="ChessGame"/>.</summary>
        public ChessGame(string line)
        {
            puzzle = LichessPuzzle.Create(line);

            Moves = new Li<Move>();

            Board = new Piece[8][];
            var fenparts = puzzle.Fen.Split(" /".ToCharArray()).ToList();
            for (int i = 0; i < 8; ++i)
            {
                var fenrow = fenparts[i];
                var r = new Piece[8];
                for (int j = 0, c = 0; j < fenrow.Length; ++j)
                {
                    char curr = fenrow[j];
                    if (int.TryParse(curr.ToString(), out int spaces) && spaces != 0)
                        c += spaces;
                    else
                        r[c++] = ChessUtilities.FenChars2Pieces[curr];
                }
                Board[7 - i] = r;
            }
            WhoseTurn = fenparts[8] == "w" ? Player.White : Player.Black;
            if (fenparts.Count > 9)
            {
                CanWhiteCastleKingSide = fenparts[9].Contains('K');
                CanWhiteCastleQueenSide = fenparts[9].Contains('Q');
                CanBlackCastleKingSide = fenparts[9].Contains('k');
                CanBlackCastleQueenSide = fenparts[9].Contains('q');

                // TODO: Es folgen enpassant, Halbzüge seit dem letzten Bauernzug oder Schlagen einer Figur, Zugnummer
                // 
            }

        }

        private string FenCastleState
        {
            get
            {
                string s = CanWhiteCastleKingSide ? "K" : "";
                s += CanWhiteCastleQueenSide ? "Q" : "";
                s += CanBlackCastleKingSide ? "k" : "";
                s += CanBlackCastleQueenSide ? "q" : "";
                if (s == "")
                    s = "-";
                return s;
            }
        }


        /// <summary>Makes a move in the game.</summary>
        /// <param name="move">The <see cref="Move"/> you want to make.</param>
        /// <param name="isMoveValidated">Only pass true when you've already checked that the move is valid.</param>
        /// <returns>Returns true if the move is made; false otherwise.</returns>
        /// <exception cref="ArgumentNullException">
        ///     The <c>move</c> is null.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        ///     The <see cref="Move.Source"/> square of the <c>move</c> doesn't contain a piece.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        ///    The <c>move.PromoteTo</c> is null and the move is a pawn promotion move.
        /// </exception>
        public bool MakeMove(Move move, bool isMoveValidated)
        {
            if (move == null)
                throw new ArgumentNullException(nameof(move));

            Piece? piece = this[move.Source.File, move.Source.Rank];
            if (piece == null)
                throw new InvalidOperationException("Source square has no piece.");

            if (!isMoveValidated && !IsValidMove(move))
                return false;

            SetCastleStatus(move, piece);

            if (piece is King && move.GetAbsDeltaX() == 2)
            {
                // Queen-side castle
                if (move.Destination.File == Linie.C)
                {
                    var rook = this[Linie.A, move.Source.Rank];
                    Board[(int)move.Source.Rank][(int)Linie.A] = null;
                    Board[(int)move.Source.Rank][(int)Linie.D] = rook;
                }

                // King-side castle
                if (move.Destination.File == Linie.G)
                {
                    var rook = this[Linie.H, move.Source.Rank];
                    Board[(int)move.Source.Rank][(int)Linie.H] = null;
                    Board[(int)move.Source.Rank][(int)Linie.F] = rook;
                }
            }

            if (piece is Pawn)
            {
                if ((move.Player == Player.White && move.Destination.Rank == Rank.Eighth) ||
                    (move.Player == Player.Black && move.Destination.Rank == Rank.First))
                {
                    piece = move.PromoteTo switch
                    {
                        PawnPromotion.Knight => new Knight(piece.Owner),
                        PawnPromotion.Bishop => new Bishop(piece.Owner),
                        PawnPromotion.Rook => new Rook(piece.Owner),
                        PawnPromotion.Queen => new Queen(piece.Owner),
                        _ => throw new ArgumentException($"A promotion move should have a valid {move.PromoteTo} property.", nameof(move)),
                    };
                }
                // Enpassant
                if (Pawn.GetPawnMoveType(move) == PawnMoveType.Capture &&
                    this[move.Destination.File, move.Destination.Rank] == null)
                {
                    // Wenn der erste angegebene Zug ein e.p ist, ist Moves leer. 
                    if (Moves.IsEmpty)
                    {
                        var killed = new Square(move.Destination.File, move.Source.Rank);
                        this.SetPiece(killed, null);
                    }
                    else
                        this.SetPiece(Moves.Last().Destination, null);

                }

            }
            this.SetPiece(move.Source, null);
            this.SetPiece(move.Destination, piece);
            Moves.Add(move);
            WhoseTurn = ChessUtilities.Opponent(move.Player);
            SetGameState();
            return true;
        }

        private void SetCastleStatus(Move move, Piece piece)
        {
            if (piece.Owner == Player.White && piece is King)
            {
                CanWhiteCastleKingSide = false;
                CanWhiteCastleQueenSide = false;
            }

            if (piece.Owner == Player.White && piece is Rook &&
                move.Source.File == Linie.A && move.Source.Rank == Rank.First)
            {
                CanWhiteCastleQueenSide = false;
            }

            if (piece.Owner == Player.White && piece is Rook &&
                move.Source.File == Linie.H && move.Source.Rank == Rank.First)
            {
                CanWhiteCastleKingSide = false;
            }

            if (piece.Owner == Player.Black && piece is King)
            {
                CanBlackCastleKingSide = false;
                CanBlackCastleQueenSide = false;
            }

            if (piece.Owner == Player.Black && piece is Rook &&
                move.Source.File == Linie.A && move.Source.Rank == Rank.Eighth)
            {
                CanBlackCastleQueenSide = false;
            }

            if (piece.Owner == Player.Black && piece is Rook &&
                move.Source.File == Linie.H && move.Source.Rank == Rank.Eighth)
            {
                CanBlackCastleKingSide = false;
            }
        }

        /// <summary>Checks if a given move is valid or not.</summary>
        /// <param name="move">The <see cref="Move"/> to check its validity.</param>
        /// <returns>Returns true if the given <c>move</c> is valid; false otherwise.</returns>
        /// <exception cref="ArgumentNullException">
        ///     The given <c>move</c> is null.
        /// </exception>
        public bool IsValidMove(Move move)
        {
            if (move == null)
                throw new ArgumentNullException(nameof(move));

            Piece? pieceSource = this[move.Source];
            Piece? pieceDestination = this[move.Destination];
            return (WhoseTurn == move.Player && pieceSource != null && pieceSource.Owner == move.Player &&
                    !Equals(move.Source, move.Destination) &&
                    (pieceDestination == null || pieceDestination.Owner != move.Player) &&
                    pieceSource.IsValidGameMove(move, this) && !PlayerWillBeInCheck(move));
        }

        internal bool PlayerWillBeInCheck(Move move)
        {
            if (move == null)
                throw new ArgumentNullException(nameof(move));

            ChessGame clone = DeepClone(); // Make the move on this board to keep original board as is.
            Piece? piece = clone[move.Source];
            clone.SetPiece(move.Source, null);
            if (piece is Pawn && clone.Moves.Any())
            {
                var lm = clone.Moves.Last();
                // en passant handling
                if (Pawn.GetPawnMoveType(lm) == PawnMoveType.TwoSteps &&
                    lm.Destination.Rank == move.Source.Rank)
                    if (lm.Source.File == move.Destination.File)
                    {
                        var lmPiece = clone[lm.Destination];
                        if (lmPiece is Pawn &&
                                AbsDist(lm.Source.File, move.Source.File) == 1 &&
                                clone[move.Destination] == null)
                            // It is en passant. Remove the taken pawn. 
                            clone.SetPiece(lm.Destination, null);
                    }
            }
            clone.SetPiece(move.Destination, piece);

            return ChessUtilities.IsPlayerInCheck(move.Player, clone);
        }

        public int Dist(Linie l1, Linie l2) => (int)l1 - (int)l2;

        public int AbsDist(Linie l1, Linie l2) => Math.Abs((int)l1 - (int)l2);

        internal void SetGameState()
        {
            Player opponent = WhoseTurn;
            Player lastPlayer = ChessUtilities.Opponent(opponent);
            bool isInCheck = ChessUtilities.IsPlayerInCheck(opponent, this);
            var hasValidMoves = ChessUtilities.GetValidMoves(this).Count > 0;

            if (isInCheck && !hasValidMoves)
            {
                GameState = lastPlayer == Player.White ? GameState.WhiteWinner : GameState.BlackWinner;
                return;
            }

            if (!hasValidMoves)
            {
                GameState = GameState.Stalemate;
                return;
            }

            if (isInCheck)
            {
                GameState = opponent == Player.White ? GameState.WhiteInCheck : GameState.BlackInCheck;
                return;
            }
            GameState = IsInsufficientMaterial() ? GameState.Draw : GameState.NotCompleted;
        }

        internal bool IsInsufficientMaterial() // TODO: Much allocations seem to happen here? (LINQ)
        {
            IEnumerable<Piece?> pieces = Board.SelectMany(x => x); // https://stackoverflow.com/questions/32588070/flatten-jagged-array-in-c-sharp

            var whitePieces = pieces.Select((p, i) => new { Piece = p, SquareColor = (i % 8 + i / 8) % 2 })
                .Where(p => p.Piece?.Owner == Player.White).ToArray();

            var blackPieces = pieces.Select((p, i) => new { Piece = p, SquareColor = (i % 8 + i / 8) % 2 })
                .Where(p => p.Piece?.Owner == Player.Black).ToArray();

            switch (whitePieces.Length)
            {
                // King vs King
                case 1 when blackPieces.Length == 1:
                // White King vs black king and (Bishop|Knight)
                case 1 when blackPieces.Length == 2 && blackPieces.Any(p => p.Piece is Bishop ||
                                                                            p.Piece is Knight):
                // Black King vs white king and (Bishop|Knight)
                case 2 when blackPieces.Length == 1 && whitePieces.Any(p => p.Piece is Bishop ||
                                                                            p.Piece is Knight):
                    return true;
                // King and bishop vs king and bishop
                case 2 when blackPieces.Length == 2:
                    {
                        var whiteBishop = whitePieces.FirstOrDefault(p => p.Piece is Bishop);
                        var blackBishop = blackPieces.FirstOrDefault(p => p.Piece is Bishop);
                        return whiteBishop != null && blackBishop != null &&
                               whiteBishop.SquareColor == blackBishop.SquareColor;
                    }
                default:
                    return false;
            }
        }

        internal static bool IsValidMove(Move move, ChessGame board)
        {
            if (move == null)
                throw new ArgumentNullException(nameof(move));

            Piece? pieceSource = board[move.Source];
            Piece? pieceDestination = board[move.Destination];

            return (pieceSource != null && pieceSource.Owner == move.Player &&
                    !Equals(move.Source, move.Destination) &&
                    (pieceDestination == null || pieceDestination.Owner != move.Player) &&
                    pieceSource.IsValidGameMove(move, board) && !board.PlayerWillBeInCheck(move));
        }

        internal bool IsTherePieceInBetween(Square square1, Square square2)
        {
            int xStep = Math.Sign(square2.File - square1.File);
            int yStep = Math.Sign(square2.Rank - square1.Rank);

            Rank rank = square1.Rank;
            Linie file = square1.File;
            while (true) // TODO: Prevent possible infinite loop (by throwing an exception) when passing un-logical squares (two squares not on same file, rank, or diagonal).
            {
                rank += yStep;
                file += xStep;
                if (rank == square2.Rank && file == square2.File)
                {
                    return false;
                }

                if (Board[(int)rank][(int)file] != null)
                {
                    return true;
                }
            }

        }

        public virtual ChessGame DeepClone() => this.DeepTClone<ChessGame>();

        protected LichessPuzzle puzzle;
    }
}
