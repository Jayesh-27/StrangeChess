import React, { useState, useMemo } from 'react';
import type { GameState, GameSettings } from './types';
import { PIECE_GLYPHS, squareName, squareIndex } from './types';

interface BoardProps {
  state: GameState;
  settings: GameSettings;
  onMove: (uci: string) => void;
  disabled: boolean;
}

export const Board: React.FC<BoardProps> = ({ state, settings, onMove, disabled }) => {
  const [selectedIdx, setSelectedIdx] = useState<number | null>(null);

  // We want to draw rank 8 at the top, rank 1 at the bottom.
  // If player is black, flip the board (rank 1 at top).
  const isFlipped = settings.playerColor === 'black';

  const squares = useMemo(() => {
    const sqs = [];
    for (let r = 7; r >= 0; r--) {
      for (let f = 0; f < 8; f++) {
        sqs.push(r * 8 + f);
      }
    }
    if (isFlipped) {
      sqs.reverse();
    }
    return sqs;
  }, [isFlipped]);

  const handleSquareClick = (idx: number) => {
    if (disabled) return;

    if (selectedIdx === null) {
      // Select a piece if it belongs to the player whose turn it is
      // and it's actually their piece.
      const piece = state.board[idx];
      if (piece) {
        const isWhitePiece = piece.p === piece.p.toUpperCase();
        const pieceColor = isWhitePiece ? 'white' : 'black';
        if (pieceColor === state.turn) {
           setSelectedIdx(idx);
        }
      }
    } else {
      // If clicking the same square, deselect
      if (selectedIdx === idx) {
        setSelectedIdx(null);
        return;
      }

      // If clicking another of own pieces, change selection
      const piece = state.board[idx];
      if (piece) {
        const isWhitePiece = piece.p === piece.p.toUpperCase();
        const pieceColor = isWhitePiece ? 'white' : 'black';
        if (pieceColor === state.turn) {
          setSelectedIdx(idx);
          return;
        }
      }

      // Try to move
      const fromSq = squareName(selectedIdx);
      const toSq = squareName(idx);
      
      // Look for the move in legal moves. It might be a promotion.
      // Auto-queen for now if promotion is available.
      let moveUci = `${fromSq}${toSq}`;
      let isLegal = state.legalMoves.includes(moveUci);
      
      if (!isLegal) {
        // Check if there is a promotion move
        if (state.legalMoves.includes(`${moveUci}q`)) {
          moveUci = `${moveUci}q`;
          isLegal = true;
        }
      }

      if (isLegal) {
        onMove(moveUci);
        setSelectedIdx(null);
      } else {
        setSelectedIdx(null); // Invalid move, deselect
      }
    }
  };

  const getLegalDestinations = () => {
    if (selectedIdx === null) return new Set<number>();
    const fromSq = squareName(selectedIdx);
    const dests = new Set<number>();
    for (const move of state.legalMoves) {
      if (move.startsWith(fromSq)) {
        const toSq = move.substring(2, 4);
        dests.add(squareIndex(toSq));
      }
    }
    return dests;
  };

  const legalDestinations = getLegalDestinations();
  const lastMove = state.history.length > 0 ? state.history[state.history.length - 1].uci : null;
  const lastMoveFrom = lastMove ? squareIndex(lastMove.substring(0, 2)) : null;
  const lastMoveTo = lastMove ? squareIndex(lastMove.substring(2, 4)) : null;

  // Find king position for check indicator
  const kingIndex = state.board.findIndex(p => p && p.p === (state.turn === 'white' ? 'K' : 'k'));

  return (
    <div className="board">
      {squares.map((idx) => {
        const rank = Math.floor(idx / 8);
        const file = idx % 8;
        const isLight = (rank + file) % 2 !== 0;
        const piece = state.board[idx];
        const isWhitePiece = piece ? piece.p === piece.p.toUpperCase() : false;
        const isLegalDest = legalDestinations.has(idx);
        const isSelected = selectedIdx === idx;
        const isLastMove = idx === lastMoveFrom || idx === lastMoveTo;
        const isInCheck = state.inCheck && idx === kingIndex;

        let className = `square ${isLight ? 'light' : 'dark'}`;
        if (isSelected) className += ' selected';
        if (isLastMove) className += ' last-move';
        if (isLegalDest) className += ` legal-move ${piece ? 'has-piece' : ''}`;
        if (isInCheck) className += ' in-check';

        return (
          <div
            key={idx}
            className={className}
            onClick={() => handleSquareClick(idx)}
          >
            {piece && (
              <span className={`piece ${isWhitePiece ? 'white' : 'black'}`}>
                {PIECE_GLYPHS[piece.p] || piece.p}
              </span>
            )}
          </div>
        );
      })}
    </div>
  );
};
