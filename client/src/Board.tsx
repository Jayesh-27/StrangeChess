import { useState, useMemo } from 'react';
import type { GameState, GameSettings } from './types';
import { PIECE_GLYPHS, squareName, squareIndex } from './types';

interface BoardProps {
  state: GameState;
  settings: GameSettings;
  onMove: (uci: string) => void;
  onDrop?: (square: string) => void;
  dropPiece?: string | null;
  disabled: boolean;
}

export const Board = ({ state, settings, onMove, onDrop, dropPiece, disabled }: BoardProps) => {
  const [selectedIdx, setSelectedIdx] = useState<number | null>(null);

  const isFlipped = settings.playerColor === 'black';

  const squares = useMemo(() => {
    const sqs = [];
    for (let r = 7; r >= 0; r--) {
      for (let f = 0; f < 8; f++) {
        sqs.push(r * 8 + f);
      }
    }
    if (isFlipped) sqs.reverse();
    return sqs;
  }, [isFlipped]);

  const handleSquareClick = (idx: number) => {
    if (disabled) return;

    if (dropPiece && onDrop) {
      const sq = squareName(idx);
      if (state.legalDrops[dropPiece]?.includes(sq)) {
        onDrop(sq);
      }
      return;
    }

    if (selectedIdx === null) {
      const piece = state.board[idx];
      if (piece) {
        const isWhitePiece = piece.p === piece.p.toUpperCase();
        const pieceColor = isWhitePiece ? 'white' : 'black';
        if (pieceColor === state.turn) {
           setSelectedIdx(idx);
        }
      }
    } else {
      if (selectedIdx === idx) {
        setSelectedIdx(null);
        return;
      }

      const piece = state.board[idx];
      if (piece) {
        const isWhitePiece = piece.p === piece.p.toUpperCase();
        const pieceColor = isWhitePiece ? 'white' : 'black';
        if (pieceColor === state.turn) {
          setSelectedIdx(idx);
          return;
        }
      }

      const fromSq = squareName(selectedIdx);
      const toSq = squareName(idx);
      
      let moveUci = `${fromSq}${toSq}`;
      let isLegal = state.legalMoves.includes(moveUci);
      
      if (!isLegal) {
        if (state.legalMoves.includes(`${moveUci}q`)) {
          moveUci = `${moveUci}q`;
          isLegal = true;
        }
      }

      if (isLegal) {
        onMove(moveUci);
        setSelectedIdx(null);
      } else {
        setSelectedIdx(null);
      }
    }
  };

  const legalDestinations = useMemo(() => {
    const dests = new Set<number>();
    if (dropPiece) {
      const dropSqs = state.legalDrops[dropPiece] || [];
      dropSqs.forEach(sq => dests.add(squareIndex(sq)));
      return dests;
    }

    if (selectedIdx !== null) {
      const fromSq = squareName(selectedIdx);
      for (const move of state.legalMoves) {
        if (move.startsWith(fromSq)) {
          dests.add(squareIndex(move.substring(2, 4)));
        }
      }
    }
    return dests;
  }, [selectedIdx, state.legalMoves, dropPiece, state.legalDrops]);

  const lastMove = state.history.length > 0 ? state.history[state.history.length - 1] : null;
  const lastMoveFrom = lastMove?.type === 'move' && lastMove.uci ? squareIndex(lastMove.uci.substring(0, 2)) : null;
  const lastMoveTo = lastMove?.type === 'move' && lastMove.uci ? squareIndex(lastMove.uci.substring(2, 4)) : 
                     lastMove?.type === 'drop' && lastMove.square ? squareIndex(lastMove.square) : null;

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
                {state.mode === 'variant' && piece.left !== null && (
                  <div className={`piece-badge ${piece.left <= 2 ? 'warning flash' : ''}`}>
                    {piece.left}
                  </div>
                )}
              </span>
            )}
          </div>
        );
      })}
    </div>
  );
};
