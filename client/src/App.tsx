import { useEffect, useState, useCallback } from 'react';
import { Board } from './Board';
import type { GameState, GameSettings } from './types';
import { DIFFICULTY_PRESETS } from './types';
import { newGame, makeMove, botMove, healthCheck } from './api';
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

  useEffect(() => {
    healthCheck().then(setServerOk);
  }, []);

  const handleNewGame = useCallback(async () => {
    setIsProcessing(true);
    setError(null);
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

  // Handle Bot Turns
  useEffect(() => {
    if (!state || state.status !== 'active') return;
    if (settings.opponent !== 'bot') return;
    
    // If it's the bot's turn
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
      
      // Add a slight delay for better UX
      const timer = setTimeout(playBot, 300);
      return () => clearTimeout(timer);
    }
  }, [state, settings, isProcessing]);

  const handleMove = async (uci: string) => {
    if (!state || isProcessing) return;
    
    setIsProcessing(true);
    setError(null);
    try {
      const newState = await makeMove(state, uci);
      setState(newState);
    } catch (err: any) {
      setError(err.message);
    } finally {
      setIsProcessing(false);
    }
  };

  if (!serverOk) {
    return <div>Waiting for server... Make sure the backend is running.</div>;
  }

  return (
    <div className="app">
      <h1>Strange Chess</h1>
      
      <div className="controls">
        <select 
          value={settings.playerColor} 
          onChange={(e) => setSettings({...settings, playerColor: e.target.value as 'white'|'black'})}
        >
          <option value="white">Play as White</option>
          <option value="black">Play as Black</option>
        </select>

        <select 
          value={settings.difficulty} 
          onChange={(e) => setSettings({...settings, difficulty: e.target.value as 'easy'|'medium'|'hard'})}
        >
          <option value="easy">Easy</option>
          <option value="medium">Medium</option>
          <option value="hard">Hard</option>
        </select>

        <button onClick={handleNewGame} disabled={isProcessing}>New Game</button>
      </div>

      {error && <div style={{ color: 'red', marginBottom: '1rem' }}>{error}</div>}

      {state && (
        <div className="board-container">
          <div className="status-panel">
            <div>
              Turn: <strong>{state.turn}</strong> {state.inCheck && <span style={{color: 'red'}}>(CHECK)</span>}
            </div>
            <div>
              Status: <strong>{state.status.toUpperCase()}</strong>
              {state.winner && ` (${state.winner} wins)`}
            </div>
          </div>

          <Board 
            state={state} 
            settings={settings}
            onMove={handleMove}
            disabled={
              isProcessing || 
              state.status !== 'active' || 
              (settings.opponent === 'bot' && state.turn !== settings.playerColor)
            }
          />
          
          {state.history.length > 0 && (
            <div className="history">
              <strong>Move History:</strong>
              <div>
                {state.history.map((h, i) => (
                  <span key={i}>
                    {h.side === 'white' ? `${Math.floor(i/2) + 1}. ` : ''}
                    {h.uci}{' '}
                  </span>
                ))}
              </div>
            </div>
          )}
        </div>
      )}
    </div>
  );
}

export default App;
