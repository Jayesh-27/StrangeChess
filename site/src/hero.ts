import * as THREE from 'three';
import { EffectComposer } from 'three/examples/jsm/postprocessing/EffectComposer.js';
import { RenderPass } from 'three/examples/jsm/postprocessing/RenderPass.js';
import { UnrealBloomPass } from 'three/examples/jsm/postprocessing/UnrealBloomPass.js';
import { OutputPass } from 'three/examples/jsm/postprocessing/OutputPass.js';

// Hero scene: a dark glass board, lathe-turned pieces, and a search tree that keeps
// growing out of a piece. Bright branches are the line the search keeps; dim ones get pruned.

const ACCENT = new THREE.Color('#5cf2ff');
const PRUNED = new THREE.Color('#2a3a52');

function lathe(profile: [number, number][], material: THREE.Material) {
  const pts = profile.map(([r, y]) => new THREE.Vector2(r, y));
  const mesh = new THREE.Mesh(new THREE.LatheGeometry(pts, 40), material);
  mesh.castShadow = true;
  return mesh;
}

const PROFILES: Record<string, [number, number][]> = {
  p: [[0, 0], [0.34, 0], [0.34, 0.08], [0.26, 0.14], [0.16, 0.3], [0.12, 0.48], [0.2, 0.52], [0.13, 0.56], [0.2, 0.66], [0.2, 0.76], [0.12, 0.85], [0, 0.88]],
  r: [[0, 0], [0.38, 0], [0.38, 0.1], [0.28, 0.16], [0.24, 0.6], [0.32, 0.66], [0.32, 0.86], [0.24, 0.86], [0.24, 0.8], [0, 0.8]],
  b: [[0, 0], [0.36, 0], [0.36, 0.08], [0.26, 0.14], [0.15, 0.4], [0.12, 0.62], [0.22, 0.66], [0.13, 0.7], [0.2, 0.82], [0.17, 0.98], [0.06, 1.06], [0.08, 1.1], [0, 1.16]],
  q: [[0, 0], [0.4, 0], [0.4, 0.1], [0.28, 0.16], [0.16, 0.58], [0.26, 0.64], [0.16, 0.7], [0.3, 1.04], [0.2, 1.06], [0.08, 1.14], [0, 1.18]],
  k: [[0, 0], [0.4, 0], [0.4, 0.1], [0.28, 0.16], [0.17, 0.62], [0.27, 0.68], [0.17, 0.74], [0.26, 1.06], [0.12, 1.12], [0, 1.12]],
};

// Knight: a turned base with a blocky head and muzzle on top.
function knight(material: THREE.Material) {
  const g = new THREE.Group();
  g.add(lathe([[0, 0], [0.36, 0], [0.36, 0.08], [0.26, 0.14], [0.2, 0.36], [0, 0.36]], material));
  const neck = new THREE.Mesh(new THREE.BoxGeometry(0.26, 0.5, 0.3), material);
  neck.position.set(0, 0.58, 0.02); neck.rotation.x = -0.25;
  const muzzle = new THREE.Mesh(new THREE.BoxGeometry(0.22, 0.2, 0.42), material);
  muzzle.position.set(0, 0.76, 0.16); muzzle.rotation.x = 0.35;
  for (const m of [neck, muzzle]) { m.castShadow = true; g.add(m); }
  return g;
}

// Fool's mate, the shortest checkmate: 1. f3 e5 2. g4 Qh4#. [type, file, rank, white?]
const BACK = ['r', 'n', 'b', 'q', 'k', 'b', 'n', 'r'];
const SETUP: [string, number, number, boolean][] = [
  ...BACK.map((t, f) => [t, f, 0, true] as [string, number, number, boolean]),
  ...[0, 1, 2, 3, 4, 7].map((f) => ['p', f, 1, true] as [string, number, number, boolean]),
  ['p', 5, 2, true], ['p', 6, 3, true],
  ...BACK.map((t, f) => (t === 'q' ? ['q', 7, 3, false] : [t, f, 7, false]) as [string, number, number, boolean]),
  ...[0, 1, 2, 3, 5, 6, 7].map((f) => ['p', f, 6, false] as [string, number, number, boolean]),
  ['p', 4, 4, false],
];

type Tree = { lines: THREE.LineSegments; nodes: THREE.Points; total: number; born: number };

export function setupHero(canvas: HTMLCanvasElement) {
  const reduced = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
  const renderer = new THREE.WebGLRenderer({ canvas, antialias: true, alpha: false, powerPreference: 'high-performance' });
  renderer.setPixelRatio(Math.min(window.devicePixelRatio, 1.75));
  renderer.toneMapping = THREE.ACESFilmicToneMapping;
  renderer.shadowMap.enabled = true;
  renderer.shadowMap.type = THREE.PCFSoftShadowMap;

  const scene = new THREE.Scene();
  scene.background = new THREE.Color('#05070a');
  scene.fog = new THREE.Fog('#05070a', 14, 30);

  const camera = new THREE.PerspectiveCamera(38, 1, 0.1, 100);
  camera.position.set(0, 8.5, 12.5);
  camera.lookAt(0, 0.6, 0);

  scene.add(new THREE.HemisphereLight('#7fb5ff', '#05070a', 0.6));
  const key = new THREE.DirectionalLight('#ffffff', 2.2);
  key.position.set(5, 10, 4);
  key.castShadow = true;
  key.shadow.mapSize.set(1024, 1024);
  key.shadow.camera.left = -6; key.shadow.camera.right = 6; key.shadow.camera.top = 6; key.shadow.camera.bottom = -6;
  scene.add(key);
  const rim = new THREE.PointLight(ACCENT, 30, 18);
  rim.position.set(-5, 3, -4);
  scene.add(rim);

  const world = new THREE.Group();
  scene.add(world);

  // Board
  const tileGeo = new THREE.BoxGeometry(0.98, 0.14, 0.98);
  const lightMat = new THREE.MeshStandardMaterial({ color: '#1b2433', metalness: 0.4, roughness: 0.35 });
  const darkMat = new THREE.MeshStandardMaterial({ color: '#0b1018', metalness: 0.5, roughness: 0.4 });
  for (let f = 0; f < 8; f++) for (let r = 0; r < 8; r++) {
    const t = new THREE.Mesh(tileGeo, (f + r) % 2 ? lightMat : darkMat);
    t.position.set(f - 3.5, -0.07, 3.5 - r);
    t.receiveShadow = true;
    world.add(t);
  }
  const frame = new THREE.LineSegments(
    new THREE.EdgesGeometry(new THREE.BoxGeometry(8.3, 0.2, 8.3)),
    new THREE.LineBasicMaterial({ color: ACCENT, transparent: true, opacity: 0.55 }),
  );
  frame.position.y = -0.08;
  world.add(frame);
  const grid = new THREE.GridHelper(8, 8, ACCENT, ACCENT);
  (grid.material as THREE.Material).transparent = true;
  (grid.material as THREE.Material).opacity = 0.12;
  grid.position.y = 0.002;
  world.add(grid);

  // Pieces
  const whiteMat = new THREE.MeshStandardMaterial({ color: '#e9eef6', metalness: 0.25, roughness: 0.25 });
  const blackMat = new THREE.MeshStandardMaterial({ color: '#3a4a5e', metalness: 0.55, roughness: 0.3, emissive: '#11707f', emissiveIntensity: 0.55 });
  const squares: THREE.Vector3[] = [];
  for (const [type, f, r, white] of SETUP) {
    const mat = white ? whiteMat : blackMat;
    const m = type === 'n' ? knight(mat) : lathe(PROFILES[type], mat);
    if (type === 'n') m.rotation.y = white ? Math.PI : 0; // knights face the opponent
    m.position.set(f - 3.5, 0, 3.5 - r);
    world.add(m);
    squares.push(m.position.clone());
  }

  // Search trees
  const trees: Tree[] = [];
  function growTree(origin: THREE.Vector3) {
    const pos: number[] = [], col: number[] = [], nodePos: number[] = [], nodeCol: number[] = [];
    const rand = Math.random;
    const walk = (p: THREE.Vector3, depth: number, onPv: boolean, spread: number) => {
      if (depth === 4) return;
      const kids = depth === 0 ? 5 : 3 + Math.floor(rand() * 2);
      const pvKid = Math.floor(rand() * kids);
      for (let i = 0; i < kids; i++) {
        const a = (i / kids) * Math.PI * 2 + rand() * 0.6 + depth;
        const q = new THREE.Vector3(p.x + Math.cos(a) * spread, p.y + 0.9 + rand() * 0.3, p.z + Math.sin(a) * spread);
        const pv = onPv && i === pvKid;
        const pruned = !pv && rand() < 0.45;
        const c = pv ? ACCENT : pruned ? PRUNED : new THREE.Color('#3f7fa0');
        pos.push(p.x, p.y, p.z, q.x, q.y, q.z);
        col.push(c.r, c.g, c.b, c.r, c.g, c.b);
        nodePos.push(q.x, q.y, q.z);
        nodeCol.push(c.r, c.g, c.b);
        if (!pruned) walk(q, depth + 1, pv, spread * 0.55);
      }
    };
    walk(origin.clone().setY(1.2), 0, true, 1.5);
    const lg = new THREE.BufferGeometry();
    lg.setAttribute('position', new THREE.Float32BufferAttribute(pos, 3));
    lg.setAttribute('color', new THREE.Float32BufferAttribute(col, 3));
    lg.setDrawRange(0, 0);
    const lines = new THREE.LineSegments(lg, new THREE.LineBasicMaterial({ vertexColors: true, transparent: true, opacity: 0.9 }));
    const ng = new THREE.BufferGeometry();
    ng.setAttribute('position', new THREE.Float32BufferAttribute(nodePos, 3));
    ng.setAttribute('color', new THREE.Float32BufferAttribute(nodeCol, 3));
    ng.setDrawRange(0, 0);
    const nodes = new THREE.Points(ng, new THREE.PointsMaterial({ size: 0.09, vertexColors: true, transparent: true, opacity: 1 }));
    world.add(lines, nodes);
    trees.push({ lines, nodes, total: pos.length / 3, born: performance.now() });
  }

  // Post-processing
  const composer = new EffectComposer(renderer);
  composer.addPass(new RenderPass(scene, camera));
  const bloom = new UnrealBloomPass(new THREE.Vector2(1, 1), 0.75, 0.45, 0.5);
  composer.addPass(bloom);
  composer.addPass(new OutputPass());

  function resize() {
    const w = canvas.clientWidth, h = canvas.clientHeight;
    if (!w || !h) return;
    renderer.setSize(w, h, false);
    composer.setSize(w, h);
    camera.aspect = w / h;
    const wide = w / h > 1.2 && w > 900;
    camera.position.z = w < 700 ? 16 : wide ? 13.5 : 12.5;
    world.position.x = wide ? 3.8 : 0; // keep the board clear of the headline on wide screens
    world.position.y = h > w ? -2.6 : 0; // and lower it on phones, under the copy
    camera.updateProjectionMatrix();
  }
  new ResizeObserver(resize).observe(canvas);
  resize();

  const mouse = { x: 0, y: 0 };
  window.addEventListener('pointermove', (e) => {
    mouse.x = e.clientX / window.innerWidth - 0.5;
    mouse.y = e.clientY / window.innerHeight - 0.5;
  });

  let visible = true;
  new IntersectionObserver(([entry]) => { visible = entry.isIntersecting; }).observe(canvas);

  let lastSpawn = 0;
  const GROW_MS = 2600, LIFE_MS = 4200;

  function frameTick(now: number) {
    if (now - lastSpawn > 1500) {
      lastSpawn = now;
      growTree(squares[Math.floor(Math.random() * squares.length)]);
    }
    for (let i = trees.length - 1; i >= 0; i--) {
      const t = trees[i];
      const age = now - t.born;
      const n = Math.min(t.total, Math.floor((age / GROW_MS) * t.total));
      t.lines.geometry.setDrawRange(0, n - (n % 2));
      t.nodes.geometry.setDrawRange(0, Math.floor(n / 2));
      const fade = age > GROW_MS ? 1 - (age - GROW_MS) / (LIFE_MS - GROW_MS) : 1;
      (t.lines.material as THREE.LineBasicMaterial).opacity = 0.9 * Math.max(0, fade);
      (t.nodes.material as THREE.PointsMaterial).opacity = Math.max(0, fade);
      if (age > LIFE_MS) {
        world.remove(t.lines, t.nodes);
        t.lines.geometry.dispose(); t.nodes.geometry.dispose();
        (t.lines.material as THREE.Material).dispose(); (t.nodes.material as THREE.Material).dispose();
        trees.splice(i, 1);
      }
    }
    world.rotation.y = now * 0.00006 + mouse.x * 0.35;
    camera.position.y = 8.5 + mouse.y * 1.5;
    camera.lookAt(0, 0.6, 0);
    composer.render();
  }

  if (reduced) {
    lastSpawn = performance.now();
    growTree(squares[2]);
    trees[0].born = performance.now() - GROW_MS;
    frameTick(performance.now());
    return;
  }

  const loop = (now: number) => {
    requestAnimationFrame(loop);
    if (!visible || document.hidden || !canvas.clientWidth) return;
    frameTick(now);
  };
  requestAnimationFrame(loop);
}
