import { useEffect, useState, useCallback } from 'react';
import { Board } from './Board';
import type { GameState, GameSettings } from './types';
import { DIFFICULTY_PRESETS, PIECE_GLYPHS } from './types';
import { newGame, makeMove, makeDrop, botMove, healthCheck } from './api';
import './index.css';

function App() {
  const [state, setState] = useState<GameState | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [isProcessing, setIsProcessing] = useState(false);
  const [serverOk, setServerOk] = useState(false);

  const [settings, setSettings] = useState<GameSettings>({
    mode: 'classic',
    opponent: 'bot',
    playerColor: 'white',
    difficulty: 'medium',
  });

  const [dropPiece, setDropPiece] = useState<string | null>(null);

  useEffect(() => {
    healthCheck().then(setServerOk);
  }, []);

  const handleNewGame = useCallback(async () => {
    setIsProcessing(true);
    setError(null);
    setDropPiece(null);
    try {
      const gs = await newGame(settings.mode);
      setState(gs);
    } catch (err: any) {
      setError(err.message);
    } finally {
      setIsProcessing(false);
    }
  }, [settings.mode]);

  useEffect(() => {
    if (serverOk && !state) {
      handleNewGame();
    }
  }, [serverOk, state, handleNewGame]);

  useEffect(() => {
    if (!state || state.status !== 'active') return;
    if (settings.opponent !== 'bot') return;
    
    if (state.turn !== settings.playerColor && !isProcessing) {
      const playBot = async () => {
        setIsProcessing(true);
        try {
          const res = await botMove(state, DIFFICULTY_PRESETS[settings.difficulty]);
          setState(res.state);
        } catch (err: any) {
          setError(err.message);
        } finally {
          setIsProcessing(false);
        }
      };
      
      const timer = setTimeout(playBot, 300);
      return () => clearTimeout(timer);
    }
  }, [state, settings, isProcessing]);

  const handleMove = async (uci: string) => {
    if (!state || isProcessing) return;
    
    setIsProcessing(true);
    setError(null);
    setDropPiece(null);
    try {
      const newState = await makeMove(state, uci);
      setState(newState);
    } catch (err: any) {
      setError(err.message);
    } finally {
      setIsProcessing(false);
    }
  };

  const handleDrop = async (square: string) => {
    if (!state || !dropPiece || isProcessing) return;
    
    setIsProcessing(true);
    setError(null);
    try {
      const newState = await makeDrop(state, dropPiece, square);
      setState(newState);
      setDropPiece(null);
    } catch (err: any) {
      setError(err.message);
    } finally {
      setIsProcessing(false);
    }
  };

  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'Escape') setDropPiece(null);
    };
    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, []);

  if (!serverOk) {
    return <div>Waiting for server... Make sure the backend is running.</div>;
  }

  const isPlayerTurn = state?.turn === settings.playerColor || settings.opponent === 'hotseat';

  return (
    <div className="app">
      <h1>Strange Chess</h1>
      
      {error && <div style={{ color: 'red', marginBottom: '1rem' }}>{error}</div>}

      <div className="main-layout">
        <div className="controls panel">
          <select value={settings.mode} onChange={(e) => setSettings({...settings, mode: e.target.value as any})}>
            <option value="classic">Classic Mode</option>
            <option value="variant">Variant Mode</option>
          </select>
          <select value={settings.opponent} onChange={(e) => setSettings({...settings, opponent: e.target.value as any})}>
            <option value="bot">Vs Bot</option>
            <option value="hotseat">Hot-Seat (2P)</option>
          </select>
          {settings.opponent === 'bot' && (
            <>
              <select value={settings.playerColor} onChange={(e) => setSettings({...settings, playerColor: e.target.value as any})}>
                <option value="white">Play as White</option>
                <option value="black">Play as Black</option>
              </select>
              <select value={settings.difficulty} onChange={(e) => setSettings({...settings, difficulty: e.target.value as any})}>
                <option value="easy">Easy</option>
                <option value="medium">Medium</option>
                <option value="hard">Hard</option>
              </select>
            </>
          )}
          <button onClick={handleNewGame} disabled={isProcessing}>New Game</button>
        </div>

        {state && (
          <>
            <div className="status-panel panel">
              <div>
                Turn: <strong>{state.turn}</strong> {state.inCheck && <span style={{color: 'red'}}>(CHECK)</span>}
              </div>
              {state.mode === 'variant' && (
                <div>
                  Points: 🤍 {state.points.white} | 🖤 {state.points.black}
                </div>
              )}
              <div>
                Status: <strong>{state.status.toUpperCase()}</strong>
                {state.winner && ` (${state.winner} wins)`}
              </div>
            </div>

            <div className="board-area">
              <Board 
                state={state} 
                settings={settings}
                onMove={handleMove}
                onDrop={handleDrop}
                dropPiece={dropPiece}
                disabled={
                  isProcessing || 
                  state.status !== 'active' || 
                  (settings.opponent === 'bot' && state.turn !== settings.playerColor)
                }
              />
            </div>

            {state.mode === 'variant' && state.status === 'active' && isPlayerTurn && (
              <div className="shop-panel panel">
                <div style={{textAlign: 'center', marginBottom: '0.5rem'}}><strong>Shop (Click to buy & drop)</strong></div>
                <div className="shop-items">
                  {['Q', 'R', 'B', 'N', 'P'].map(p => {
                    const pieceChar = state.turn === 'white' ? p : p.toLowerCase();
                    const cost = state.config.costs[p === 'P' ? 'pawn' : p === 'N' ? 'knight' : p === 'B' ? 'bishop' : p === 'R' ? 'rook' : 'queen'];
                    const affordable = state.points[state.turn] >= cost;
                    const validDrops = state.legalDrops[p] || [];
                    const canDrop = affordable && validDrops.length > 0;
                    
                    return (
                      <div 
                        key={p} 
                        className={`shop-btn ${canDrop ? 'affordable' : 'disabled'} ${dropPiece === p ? 'selected' : ''}`}
                        onClick={() => {
                          if (canDrop) setDropPiece(dropPiece === p ? null : p);
                        }}
                      >
                        <span style={{fontSize: '2rem'}} className={`piece ${state.turn}`}>{PIECE_GLYPHS[pieceChar]}</span>
                        <small>{cost} pts</small>
                      </div>
                    );
                  })}
                </div>
                {dropPiece && <div style={{color: '#4caf50', textAlign: 'center', marginTop: '0.5rem'}}>Select an empty square to drop {PIECE_GLYPHS[state.turn === 'white' ? dropPiece : dropPiece.toLowerCase()]}. Press Esc to cancel.</div>}
              </div>
            )}
            
            {state.mode === 'variant' && state.events.length > 0 && (
              <div className="event-log panel">
                {state.events.map((e, i) => (
                  <div key={i}>
                    ⚠️ {e.side}'s {PIECE_GLYPHS[e.piece] || e.piece} on {e.square} expired.
                  </div>
                ))}
              </div>
            )}
            
            {state.history.length > 0 && (
              <div className="history panel">
                <strong>Move History:</strong>
                <div>
                  {state.history.map((h, i) => (
                    <span key={i}>
                      {h.side === 'white' ? `${Math.floor(i/2) + 1}. ` : ''}
                      {h.type === 'drop' ? `${h.piece}@${h.square}` : h.uci}{' '}
                    </span>
                  ))}
                </div>
              </div>
            )}
          </>
        )}
      </div>
    </div>
  );
}

export default App;
