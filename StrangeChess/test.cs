using System;

class Program {
    static void Main() {
        ulong blockers = 0;
        int square = 7;
        
        ulong attacks = 0UL;
        int rank = square / 8;
        int file = square % 8;

        for (int r = rank + 1; r < 8; r++) {
            ulong sqMask = 1UL << (r * 8 + file);
            attacks |= sqMask;
            if ((blockers & sqMask) != 0) break;
        }
        for (int r = rank - 1; r >= 0; r--) {
            ulong sqMask = 1UL << (r * 8 + file);
            attacks |= sqMask;
            if ((blockers & sqMask) != 0) break;
        }
        for (int f = file + 1; f < 8; f++) {
            ulong sqMask = 1UL << (rank * 8 + f);
            attacks |= sqMask;
            if ((blockers & sqMask) != 0) break;
        }
        for (int f = file - 1; f >= 0; f--) {
            ulong sqMask = 1UL << (rank * 8 + f);
            attacks |= sqMask;
            if ((blockers & sqMask) != 0) break;
        }

        Console.WriteLine(Convert.ToString((long)attacks, 2).PadLeft(64, '0'));
    }
}
