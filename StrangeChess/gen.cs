using System;
using System.Collections.Generic;

class Generator {
    static ulong Random64(Random rnd) {
        ulong u1 = (ulong)rnd.Next() & 0xFFFF;
        ulong u2 = (ulong)rnd.Next() & 0xFFFF;
        ulong u3 = (ulong)rnd.Next() & 0xFFFF;
        ulong u4 = (ulong)rnd.Next() & 0xFFFF;
        return u1 | (u2 << 16) | (u3 << 32) | (u4 << 48);
    }
    static ulong Random64FewBits(Random rnd) {
        return Random64(rnd) & Random64(rnd) & Random64(rnd);
    }

    static void Main() {
        int[] rookBitCount = {
            12, 11, 11, 11, 11, 11, 11, 12,
            11, 10, 10, 10, 10, 10, 10, 11,
            11, 10, 10, 10, 10, 10, 10, 11,
            11, 10, 10, 10, 10, 10, 10, 11,
            11, 10, 10, 10, 10, 10, 10, 11,
            11, 10, 10, 10, 10, 10, 10, 11,
            11, 10, 10, 10, 10, 10, 10, 11,
            12, 11, 11, 11, 11, 11, 11, 12
        };
        ulong[] magics = new ulong[64];
        Random rnd = new Random(1337);

        for (int sq = 0; sq < 64; sq++) {
            ulong mask = 0;
            int r = sq / 8, c = sq % 8;
            for (int i = 0; i < 8; i++) {
                if (i != r) mask |= 1UL << (i * 8 + c);
                if (i != c) mask |= 1UL << (r * 8 + i);
            }
            mask &= ~(1UL << (0 * 8 + c));
            mask &= ~(1UL << (7 * 8 + c));
            mask &= ~(1UL << (r * 8 + 0));
            mask &= ~(1UL << (r * 8 + 7));

            List<ulong> subsets = new List<ulong>();
            ulong sub = 0;
            do {
                subsets.Add(sub);
                sub = (sub - mask) & mask;
            } while (sub != 0);

            // Generate slow attacks
            ulong[] attacks = new ulong[subsets.Count];
            for (int i = 0; i < subsets.Count; i++) {
                ulong b = subsets[i];
                ulong att = 0;
                for (int rank = r + 1; rank < 8; rank++) { ulong m = 1UL << (rank * 8 + c); att |= m; if ((b & m) != 0) break; }
                for (int rank = r - 1; rank >= 0; rank--) { ulong m = 1UL << (rank * 8 + c); att |= m; if ((b & m) != 0) break; }
                for (int file = c + 1; file < 8; file++) { ulong m = 1UL << (r * 8 + file); att |= m; if ((b & m) != 0) break; }
                for (int file = c - 1; file >= 0; file--) { ulong m = 1UL << (r * 8 + file); att |= m; if ((b & m) != 0) break; }
                attacks[i] = att;
            }

            int bits = 64 - rookBitCount[sq];
            bool found = false;
            ulong[] used = new ulong[1 << rookBitCount[sq]];
            while (!found) {
                ulong magic = Random64FewBits(rnd);
                if (((mask * magic) & 0xFF00000000000000UL) < 0xFF00000000000000UL) continue; // Optimization

                Array.Clear(used, 0, used.Length);
                bool fail = false;
                for (int i = 0; i < subsets.Count; i++) {
                    int index = (int)((subsets[i] * magic) >> bits);
                    if (used[index] == 0) {
                        used[index] = attacks[i] == 0 ? ulong.MaxValue : attacks[i]; // avoid 0 collision
                    } else if (used[index] != (attacks[i] == 0 ? ulong.MaxValue : attacks[i])) { // Constructive collision OK
                        fail = true;
                        break;
                    }
                }
                if (!fail) {
                    magics[sq] = magic;
                    Console.Write("0x" + magic.ToString("X") + "UL, ");
                    found = true;
                }
            }
        }
    }
}
