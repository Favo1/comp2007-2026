// Runtime maze generator — mirrors MazeBuilder editor tool using Instantiate().
// Attach to a persistent GameObject in MainGame scene.
// On Start() it auto-generates a fresh maze, so every scene reload gets a new maze.
using UnityEngine;
using System.Collections.Generic;

public class MazeGenerator : MonoBehaviour
{
    [Header("Grid")]
    public int   cols       = 20;
    public int   rows       = 20;
    public float cell       = 3f;
    public float mazeScale  = 2f;
    public int   seed       = 0;      // 0 = random each run

    [Header("Prefabs — Maze")]
    public GameObject wallPrefab;
    public GameObject floorPrefab;
    public GameObject pillarPrefab;
    public GameObject lampPrefab;
    public GameObject lightPrefab;
    public GameObject switchPrefab;
    public GameObject doorPrefab;

    [Header("Prefabs — Chest")]
    public GameObject chestPrefab;
    public int   chestCount    = 5;
    public float chestY        = 0.8f;

    [Header("Tuning")]
    public bool  placePillars    = true;
    public bool  placeGround     = true;
    public float doorChance      = 0.12f;
    public float ambientLampRate = 0.18f;
    public float lampY           = 3.0f;
    public float lampFaceOffset  = 0.20f;

    // ── grid state ──────────────────────────────────────────────────────────
    bool[,] hLine, vLine, visited;
    int[,]  tagH,  tagV;
    System.Random rng;
    int entryCol, exitCol;

    void Start() => Generate();

    // ── public entry point ──────────────────────────────────────────────────
    public void Generate()
    {
        int s = seed != 0 ? seed : (int)System.DateTime.Now.Ticks;
        rng = new System.Random(s);

        hLine   = new bool[rows + 1, cols];
        vLine   = new bool[rows, cols + 1];
        visited = new bool[rows, cols];
        Fill(hLine, true);
        Fill(vLine, true);

        Backtrack(0, 0);

        entryCol = cols / 2;
        exitCol  = cols / 2;
        hLine[0,    entryCol] = false;   // south boundary opening (entry)
        hLine[rows, exitCol]  = false;   // north boundary opening (exit)

        // Force a clear corridor into/out of the maze so the entry and exit
        // are never blocked by side walls forming a dead-end pocket.
        hLine[1,        entryCol] = false;   // open north wall of entry cell
        hLine[rows - 1, exitCol]  = false;   // open south wall of exit cell

        AssignSpecialWalls();

        // Destroy any leftover maze objects from a previous generation or editor build
        foreach (string n in new[] { "MazeTerrain", "MazeWalls", "MazeDoors", "MazeChests" })
        {
            var old = GameObject.Find(n);
            if (old != null) Destroy(old);
        }

        var scale     = new Vector3(mazeScale, mazeScale, mazeScale);
        var terrain   = new GameObject("MazeTerrain");
        var walls     = new GameObject("MazeWalls");
        var doors     = new GameObject("MazeDoors");
        terrain.transform.localScale = scale;
        walls.transform.localScale   = scale;
        doors.transform.localScale   = scale;

        if (placeGround) PlaceGround(terrain.transform);
        PlaceFloor(terrain.transform);
        PlaceWalls(walls.transform);
        PlaceDoors(doors.transform);
        if (placePillars) PlacePillars(walls.transform);
        PlaceSwitch(terrain.transform);
        SpawnChests();

        Debug.Log("[MazeGen] Runtime seed=" + s);
    }

    // ── recursive backtracker ───────────────────────────────────────────────
    void Backtrack(int sr, int sc)
    {
        var stack = new Stack<(int r, int c)>();
        visited[sr, sc] = true;
        stack.Push((sr, sc));
        int[] dr = { 1, -1, 0, 0 };
        int[] dc = { 0, 0, 1, -1 };

        while (stack.Count > 0)
        {
            var (r, c) = stack.Peek();
            var avail = new List<int>();
            for (int d = 0; d < 4; d++)
            {
                int nr = r + dr[d], nc = c + dc[d];
                if (nr >= 0 && nr < rows && nc >= 0 && nc < cols && !visited[nr, nc])
                    avail.Add(d);
            }
            if (avail.Count > 0)
            {
                int d  = avail[rng.Next(avail.Count)];
                int nr = r + dr[d], nc = c + dc[d];
                switch (d)
                {
                    case 0: hLine[r + 1, c] = false; break;
                    case 1: hLine[r,     c] = false; break;
                    case 2: vLine[r, c + 1] = false; break;
                    case 3: vLine[r, c    ] = false; break;
                }
                visited[nr, nc] = true;
                stack.Push((nr, nc));
            }
            else stack.Pop();
        }
    }

    // ── wall tagging ────────────────────────────────────────────────────────
    void AssignSpecialWalls()
    {
        tagH = new int[rows + 1, cols];
        tagV = new int[rows, cols + 1];

        for (int r = 0; r < rows; r++)
        for (int c = 0; c < cols; c++)
        {
            bool nO = !hLine[r + 1, c], sO = !hLine[r, c];
            bool eO = !vLine[r, c + 1], wO = !vLine[r, c];
            int  op = (nO?1:0)+(sO?1:0)+(eO?1:0)+(wO?1:0);
            if (op != 1) continue;

            if (nO) TagH(r,     c, 2);
            if (sO) TagH(r + 1, c, 2);
            if (eO) TagV(r, c,     2);
            if (wO) TagV(r, c + 1, 2);

            if (nO || sO) { TagV(r, c, 1); TagV(r, c + 1, 1); }
            if (eO || wO) { TagH(r, c, 1); TagH(r + 1, c, 1); }
        }

        for (int r = 1; r < rows; r++)
        for (int c = 0; c < cols; c++)
            if (hLine[r, c] && tagH[r, c] == 0 && rng.NextDouble() < ambientLampRate)
                tagH[r, c] = 1;

        for (int r = 0; r < rows; r++)
        for (int c = 1; c < cols; c++)
            if (vLine[r, c] && tagV[r, c] == 0 && rng.NextDouble() < ambientLampRate)
                tagV[r, c] = 1;
    }

    void TagH(int r, int c, int t)
    {
        if (r <= 0 || r >= rows || c < 0 || c >= cols) return;
        if (!hLine[r, c] || tagH[r, c] >= t) return;
        tagH[r, c] = t;
    }
    void TagV(int r, int c, int t)
    {
        if (c <= 0 || c >= cols || r < 0 || r >= rows) return;
        if (!vLine[r, c] || tagV[r, c] >= t) return;
        tagV[r, c] = t;
    }

    // ── scene building ──────────────────────────────────────────────────────
    void PlaceGround(Transform parent)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Plane);
        g.name = "Ground";
        float w = cols * cell, h = rows * cell;
        g.transform.SetParent(parent, false);
        g.transform.localPosition = new Vector3(w * .5f, -0.05f, h * .5f);
        g.transform.localScale    = new Vector3(w * .1f, 1f, h * .1f);
    }

    void PlaceFloor(Transform parent)
    {
        for (int r = 0; r < rows; r++)
        for (int c = 0; c < cols; c++)
            Inst(parent, floorPrefab, (c + 1) * cell, 0f, r * cell, 0f);
    }

    void PlaceWalls(Transform parent)
    {
        for (int r = 0; r <= rows; r++)
        for (int c = 0; c < cols; c++)
        {
            if (!hLine[r, c]) continue;
            float px = (c + 1) * cell, pz = r * cell;
            Inst(parent, wallPrefab, px, 0f, pz, 0f);
            var ov = TagToOverlay(tagH[r, c]);
            if (ov != null)
            {
                bool lmp = tagH[r, c] == 1;
                Inst(parent, ov,
                     lmp ? px - cell * .5f : px,
                     lmp ? lampY : 0f,
                     lmp ? pz + lampFaceOffset : pz, 0f);
            }
        }

        for (int r = 0; r < rows; r++)
        for (int c = 0; c <= cols; c++)
        {
            if (!vLine[r, c]) continue;
            float px = c * cell, pz = r * cell;
            Inst(parent, wallPrefab, px, 0f, pz, 90f);
            var ov = TagToOverlay(tagV[r, c]);
            if (ov != null)
            {
                bool lmp = tagV[r, c] == 1;
                Inst(parent, ov,
                     lmp ? px - lampFaceOffset : px,
                     lmp ? lampY : 0f,
                     lmp ? pz + cell * .5f : pz,
                     lmp ? 270f : 90f);
            }
        }
    }

    GameObject TagToOverlay(int t)
    {
        if (t == 1) return lampPrefab;
        if (t == 2) return lightPrefab;
        return null;
    }

    void PlaceDoors(Transform parent)
    {
        if (doorPrefab == null) return;
        Inst(parent, doorPrefab, (entryCol + 1) * cell, 0f, 0f,          0f);
        Inst(parent, doorPrefab, (exitCol  + 1) * cell, 0f, rows * cell, 0f);

        for (int r = 1; r < rows; r++)
        for (int c = 0; c < cols; c++)
            if (!hLine[r, c] && rng.NextDouble() < doorChance)
                Inst(parent, doorPrefab, (c + 1) * cell, 0f, r * cell, 0f);

        for (int r = 0; r < rows; r++)
        for (int c = 1; c < cols; c++)
            if (!vLine[r, c] && rng.NextDouble() < doorChance)
                Inst(parent, doorPrefab, c * cell, 0f, r * cell, 90f);
    }

    void PlacePillars(Transform parent)
    {
        for (int r = 0; r <= rows; r++)
        for (int c = 0; c <= cols; c++)
        {
            int w = 0;
            if (c < cols  && hLine[r,     c]) w++;
            if (c > 0     && hLine[r,     c - 1]) w++;
            if (r < rows  && vLine[r,     c]) w++;
            if (r > 0     && vLine[r - 1, c]) w++;
            if (w >= 2) Inst(parent, pillarPrefab, c * cell, 0f, r * cell, 0f);
        }
    }

    void PlaceSwitch(Transform parent)
    {
        if (switchPrefab == null) return;
        var dist = new int[rows, cols];
        Fill(dist, -1);
        var q = new Queue<(int r, int c)>();
        dist[0, entryCol] = 0;
        q.Enqueue((0, entryCol));
        int[] dr = { 1,-1, 0, 0 }, dc = { 0, 0, 1,-1 };
        int fr = 0, fc = entryCol;

        while (q.Count > 0)
        {
            var (r, c) = q.Dequeue();
            if (dist[r, c] > dist[fr, fc]) { fr = r; fc = c; }
            for (int d = 0; d < 4; d++)
            {
                int nr = r + dr[d], nc = c + dc[d];
                if (nr < 0 || nr >= rows || nc < 0 || nc >= cols || dist[nr, nc] >= 0) continue;
                bool p = d == 0 ? !hLine[r+1,c] : d == 1 ? !hLine[r,c] : d == 2 ? !vLine[r,c+1] : !vLine[r,c];
                if (!p) continue;
                dist[nr, nc] = dist[r, c] + 1;
                q.Enqueue((nr, nc));
            }
        }
        Inst(parent, switchPrefab, (fc + .5f) * cell, 0f, (fr + .5f) * cell, 0f);
    }

    void SpawnChests()
    {
        if (chestPrefab == null) return;

        var container = new GameObject("MazeChests");
        var placed    = new List<(int r, int c)>();
        int count     = 0;

        // Exit chest — always first
        PlaceChest(container, rows - 1, exitCol, count++);
        placed.Add((rows - 1, exitCol));

        // Random interior chests
        var cells = new List<(int r, int c)>();
        for (int r = 2; r < rows - 1; r++)
        for (int c = 0; c < cols; c++)
            if (r != rows - 1 || c != exitCol)
                cells.Add((r, c));

        for (int i = cells.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            var tmp = cells[i]; cells[i] = cells[j]; cells[j] = tmp;
        }

        foreach (var (r, c) in cells)
        {
            if (count >= chestCount) break;
            bool near = false;
            foreach (var (pr, pc) in placed)
                if (Mathf.Abs(r - pr) + Mathf.Abs(c - pc) < 3) { near = true; break; }
            if (near) continue;
            placed.Add((r, c));
            PlaceChest(container, r, c, count++);
        }
    }

    void PlaceChest(GameObject container, int r, int c, int index)
    {
        float wx = (c * cell + cell * .5f) * mazeScale;
        float wz = (r * cell + cell * .5f) * mazeScale;
        var go = Instantiate(chestPrefab, new Vector3(wx, chestY, wz),
                             Quaternion.Euler(0f, 180f, 0f), container.transform);
        go.name = index == 0 ? "LootChest_Exit" : "LootChest_" + index;
    }

    // ── helpers ─────────────────────────────────────────────────────────────
    void Inst(Transform parent, GameObject prefab, float lx, float ly, float lz, float ey)
    {
        if (prefab == null) return;
        var go = Instantiate(prefab, parent);
        go.transform.localPosition    = new Vector3(lx, ly, lz);
        go.transform.localEulerAngles = new Vector3(0f, ey, 0f);
    }

    static void Fill(bool[,] a, bool v) { for (int i=0;i<a.GetLength(0);i++) for(int j=0;j<a.GetLength(1);j++) a[i,j]=v; }
    static void Fill(int[,]  a, int  v) { for (int i=0;i<a.GetLength(0);i++) for(int j=0;j<a.GetLength(1);j++) a[i,j]=v; }
}
