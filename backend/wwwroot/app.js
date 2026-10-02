const boardEl = document.querySelector('#board');
const turnLabel = document.querySelector('#turnLabel');
const checkLabel = document.querySelector('#checkLabel');
const movesEl = document.querySelector('#moves');
const hint = document.querySelector('#hint');
const pieceGrid = document.querySelector('#pieceGrid');
const removeButton = document.querySelector('#removeButton');
const doneButton = document.querySelector('#doneButton');

let state = null;
let selected = null;
let editTool = null;
let editing = false;
const glyphs = { P: '♙', N: '♘', B: '♗', R: '♖', Q: '♕', K: '♔', p: '♟', n: '♞', b: '♝', r: '♜', q: '♛', k: '♚' };
const palettePieces = [
  ['P', 'White pawn'], ['N', 'White knight'], ['B', 'White bishop'], ['R', 'White rook'], ['Q', 'White queen'], ['K', 'White king'],
  ['p', 'Black pawn'], ['n', 'Black knight'], ['b', 'Black bishop'], ['r', 'Black rook'], ['q', 'Black queen'], ['k', 'Black king']
];

async function api(path, options = {}) {
  const response = await fetch(path, { headers: { 'Content-Type': 'application/json' }, ...options });
  const data = await response.json();
  if (!response.ok) throw Error(data.error || 'Request failed');
  return data;
}

function squareName(index) {
  return String.fromCharCode(97 + index % 8) + (Math.floor(index / 8) + 1);
}

function renderPalette() {
  pieceGrid.innerHTML = palettePieces.map(([symbol, label]) => `<button class="palette-piece ${editTool === symbol ? 'active' : ''}" draggable="true" data-piece="${symbol}" title="${label}"><span class="piece ${symbol === symbol.toUpperCase() ? 'white' : 'black'}">${glyphs[symbol]}</span></button>`).join('');
  pieceGrid.querySelectorAll('.palette-piece').forEach(button => {
    button.onclick = () => chooseTool(button.dataset.piece);
    button.ondragstart = event => {
      chooseTool(button.dataset.piece);
      event.dataTransfer.setData('text/plain', button.dataset.piece);
    };
  });
  removeButton.classList.toggle('active', editing && editTool === 'remove');
  doneButton.textContent = editing ? 'Done editing' : 'Edit position';
}

function chooseTool(tool) {
  editing = true;
  editTool = tool;
  selected = null;
  renderPalette();
  render();
}

function render() {
  if (!state) return;
  boardEl.innerHTML = '';
  const targets = new Set(selected === null ? [] : state.legalMoves.filter(move => move.startsWith(squareName(selected))).map(move => move.slice(2, 4)));
  for (let display = 0; display < 64; display++) {
    const file = display % 8;
    const rank = 7 - Math.floor(display / 8);
    const index = rank * 8 + file;
    const square = document.createElement('button');
    const piece = state.board[index];
    square.className = `square ${(file + rank) % 2 ? 'dark' : 'light'}`;
    if (selected === index) square.classList.add('selected');
    if (editing && editTool) square.classList.add('edit-target');
    if (targets.has(squareName(index))) square.classList.add(piece ? 'capture' : 'target');
    square.dataset.square = index;
    if (file === 0) square.innerHTML += `<span class="coord">${rank + 1}</span>`;
    if (rank === 0) square.innerHTML += `<span class="coord" style="left:auto;right:6px;bottom:-1px">${String.fromCharCode(97 + file)}</span>`;
    if (piece) {
      const pieceElement = document.createElement('span');
      pieceElement.className = `piece ${piece === piece.toUpperCase() ? 'white' : 'black'}`;
      pieceElement.textContent = glyphs[piece];
      square.append(pieceElement);
    }
    square.onclick = () => clickSquare(index);
    square.ondragover = event => { if (editing && editTool && editTool !== 'remove') event.preventDefault(); };
    square.ondrop = event => {
      event.preventDefault();
      const pieceToPlace = event.dataTransfer.getData('text/plain');
      if (pieceToPlace) editSquare(index, pieceToPlace);
    };
    boardEl.append(square);
  }
  turnLabel.textContent = editing ? 'position editor' : `${state.turn} to move`;
  checkLabel.textContent = state.inCheck ? 'CHECK' : '';
  movesEl.innerHTML = state.moves.map(move => `<li>${move}</li>`).join('');
  document.querySelector('#moveCount').textContent = `${state.moves.length} moves`;
  hint.textContent = editing ? (editTool === 'remove' ? 'Click a square to remove its piece.' : 'Click or drag the selected piece onto a square.') : 'Select a piece, then select its destination.';
}

function clickSquare(index) {
  if (!state) return;
  if (editing && editTool) {
    editSquare(index, editTool === 'remove' ? null : editTool);
    return;
  }
  const name = squareName(index);
  if (selected !== null) {
    const match = state.legalMoves.find(move => move.startsWith(squareName(selected) + name));
    if (match) { playMove(match); return; }
  }
  if (state.board[index] && (state.turn === 'white') === (state.board[index] === state.board[index].toUpperCase())) selected = index;
  else selected = null;
  render();
}

async function editSquare(square, piece) {
  try {
    state = await api('/api/game/edit', { method: 'POST', body: JSON.stringify({ square, piece }) });
    selected = null;
    render();
  } catch (error) {
    hint.textContent = error.message;
  }
}

async function playMove(uci) {
  selected = null;
  try {
    state = await api('/api/game/move', { method: 'POST', body: JSON.stringify({ uci }) });
    render();
  } catch (error) {
    hint.textContent = error.message;
    render();
  }
}

async function refresh() { state = await api('/api/game'); render(); }

removeButton.onclick = () => chooseTool('remove');
doneButton.onclick = () => { editing = !editing; editTool = null; selected = null; renderPalette(); render(); };
document.querySelector('#resetButton').onclick = async () => { state = await api('/api/game/reset', { method: 'POST', body: '{}' }); editing = false; editTool = null; selected = null; renderPalette(); render(); };
document.querySelector('#engineButton').onclick = async event => {
  const button = event.currentTarget;
  button.disabled = true;
  button.textContent = 'Thinking...';
  try {
    state = await api('/api/game/engine', { method: 'POST', body: JSON.stringify({ depth: +document.querySelector('#depth').value, timeMs: +document.querySelector('#time').value }) });
    selected = null;
    render();
  } catch (error) { hint.textContent = error.message; }
  finally { button.disabled = false; button.innerHTML = '<span>✦</span> Ask engine to move'; }
};
document.querySelector('#loadFen').onclick = async () => {
  const fen = document.querySelector('#fen').value.trim();
  if (!fen) return;
  try { state = await api('/api/game/reset', { method: 'POST', body: JSON.stringify({ fen }) }); editing = false; editTool = null; selected = null; renderPalette(); render(); }
  catch (error) { hint.textContent = error.message; }
};
document.querySelector('#depth').oninput = event => document.querySelector('#depthValue').textContent = event.target.value;
document.querySelector('#time').oninput = event => document.querySelector('#timeValue').textContent = `${event.target.value} ms`;

renderPalette();
refresh().catch(error => hint.textContent = error.message);
