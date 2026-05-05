// Procedural maze generator using the Recursive Backtracker (DFS) algorithm.
// Menu: TreasureQuest > Generate Procedural Maze
// Re-running is safe: clears the previous maze first.
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public static class MazeBuilder
{
    // ---- Configurable settings ----------------------------------------
    const int   COLS              = 20;    // cells east-west
    const int   ROWS              = 20;    // cells north-south
    const float CELL              = 3f;    // metres per cell (matches Wall_3M / Floor_3M at scale 1)
    const float MAZE_SCALE        = 2f;    // uniform scale applied to every maze container
    const int   SEED              = 0;     // 0 = random seed each run
    const float DOOR_CHANCE       = 0.12f; // fraction of interior passages becoming Wall_Door
    const float AMBIENT_LAMP_RATE = 0.18f; // ambient random Wall_Lamp overlay on interior walls
    const bool  PLACE_PILLARS     = true;
    const bool  PLACE_GROUND      = true;
    const int   CHEST_COUNT       = 5;      // number of chests to scatter in the maze

    // Wall_3M is 4 m tall (local).  The lamp sits at ~62 % wall height.
    // LAMP_FACE_OFFSET pulls the pivot off the wall centre and onto the
    // corridor face (wall half-thickness ≈ 0.185 m → use 0.20 m).
    // Wall_Light geometry self-positions near the top inside the FBX, so
    // it shares the wall's y=0 base and needs no face offset.
    // Wall_Lamp mesh: Y ∈ [−0.181, +0.181] centred at pivot, Z ∈ [0, 0.1] protrudes outward.
    // LAMP_Y = 2.5 → world y = 5.0 m ≈ 62 % of 8 m wall (first grey brick above centre).
    // LAMP_FACE_OFFSET = 0.20 → lamp back clears wall front-face (half-thickness 0.185 m) by ~1.5 cm.
    const float LAMP_Y           = 3.0f;   // local Y above wall base
    const float LAMP_FACE_OFFSET = 0.20f;  // local offset away from wall face

    // ---- Prefab paths ----------------------------------------------------
    const string PR = "Assets/Maze/Prefabs/";
    const string P_WALL    = PR + "Wall_3M.prefab";
    const string P_FLOOR   = PR + "Floor_3M.prefab";
    const string P_PILLAR  = PR + "Pilar.prefab";
    const string P_LAMP    = PR + "Wall_Lamp.prefab";
    const string P_LIGHT   = PR + "Wall_Light.prefab";
    const string P_SWITCH  = PR + "Switch.prefab";
    const string P_DOOR    = PR + "Wall_Door.prefab";
    const string P_CHEST   = "Assets/Prefabs/LootChest.prefab";

    // ---- Maze grid -------------------------------------------------------
    static bool[,] hLine, vLine, visited;
    static System.Random rng;
    static int entryCol, exitCol;

    // tagH/tagV values: 0 = plain Wall_3M, 1 = Wall_Lamp, 2 = Wall_Light, 3 = Wall_Door
    static int[,] tagH, tagV;

    static GameObject pfbWall, pfbFloor, pfbPillar, pfbLamp, pfbLight, pfbSwitch, pfbDoor, pfbChest;

    // ---- Menu actions ----------------------------------------------------

    [MenuItem("TreasureQuest/Generate Procedural Maze")]
    static void Generate()
    {
        if (!LoadPrefabs()) return;

        int seed = (SEED != 0) ? SEED : (int)System.DateTime.Now.Ticks;
        rng = new System.Random(seed);

        hLine   = new bool[ROWS + 1, COLS];
        vLine   = new bool[ROWS, COLS + 1];
        visited = new bool[ROWS, COLS];
        Fill(hLine, true);
        Fill(vLine, true);

        Backtrack(0, 0);

        entryCol = COLS / 2;
        exitCol  = COLS / 2;
        hLine[0,    entryCol] = false;
        hLine[ROWS, exitCol]  = false;

        // Decide which walls get lamps / lights based on adjacent dead-ends
        AssignSpecialWalls();

        ClearScene();

        // Create containers and apply uniform scale so every child inherits it.
        // Children are placed with localPosition (SetParent worldPositionStays=false)
        // so CELL-grid local coords are correct and the parent scale doubles everything.
        var terrain = new GameObject("MazeTerrain");
        var walls   = new GameObject("MazeWalls");
        var doors   = new GameObject("MazeDoors");
        var scale   = new Vector3(MAZE_SCALE, MAZE_SCALE, MAZE_SCALE);
        terrain.transform.localScale = scale;
        walls.transform.localScale   = scale;
        doors.transform.localScale   = scale;

        Undo.RegisterCreatedObjectUndo(terrain, "MazeGen terrain");
        Undo.RegisterCreatedObjectUndo(walls,   "MazeGen walls");
        Undo.RegisterCreatedObjectUndo(doors,   "MazeGen doors");

        if (PLACE_GROUND) PlaceGround(terrain.transform);
        PlaceFloor(terrain.transform);
        PlaceWalls(walls.transform);
        PlaceDoors(doors.transform);
        if (PLACE_PILLARS) PlacePillars(walls.transform);
        PlaceSwitch(terrain.transform);
        SpawnChests();

        // Spawn position is in world space (local * MAZE_SCALE)
        float spawnX = (entryCol + 0.5f) * CELL * MAZE_SCALE;
        Debug.Log("[MazeGen] Seed=" + seed + "  Grid=" + COLS + "x" + ROWS +
                  "  WorldSize=" + (COLS * CELL * MAZE_SCALE) + "x" + (ROWS * CELL * MAZE_SCALE) + "m\n" +
                  "Spawn at (" + spawnX + ", 1, " + (-CELL * MAZE_SCALE) + ")");
    }

    [MenuItem("TreasureQuest/Clear Maze")]
    static void ClearMenu()
    {
        ClearScene();
        Debug.Log("[MazeGen] Cleared.");
    }

    // ---- Recursive backtracker -------------------------------------------

    static void Backtrack(int sr, int sc)
    {
        var stack = new Stack<(int r, int c)>();
        visited[sr, sc] = true;
        stack.Push((sr, sc));

        int[] dr = {  1, -1,  0,  0 };
        int[] dc = {  0,  0,  1, -1 };

        while (stack.Count > 0)
        {
            var (r, c) = stack.Peek();
            var avail = new List<int>();

            for (int d = 0; d < 4; d++)
            {
                int nr = r + dr[d], nc = c + dc[d];
                if (nr >= 0 && nr < ROWS && nc >= 0 && nc < COLS && !visited[nr, nc])
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
            else
            {
                stack.Pop();
            }
        }
    }

    // ---- Designate special wall placements -------------------------------

    // For every dead-end cell (cells with exactly one open passage):
    //   * Wall_Light on the wall directly OPPOSITE the open passage (lights the alcove)
    //   * Wall_Lamp on the two SIDE walls of the dead-end (corridor-facing torches)
    // Remaining interior closed walls: small chance of an ambient Wall_Lamp.
    static void AssignSpecialWalls()
    {
        tagH = new int[ROWS + 1, COLS];
        tagV = new int[ROWS, COLS + 1];

        for (int r = 0; r < ROWS; r++)
        {
            for (int c = 0; c < COLS; c++)
            {
                bool nOpen = !hLine[r + 1, c];
                bool sOpen = !hLine[r,     c];
                bool eOpen = !vLine[r, c + 1];
                bool wOpen = !vLine[r, c    ];

                int openCount = (nOpen ? 1 : 0) + (sOpen ? 1 : 0) + (eOpen ? 1 : 0) + (wOpen ? 1 : 0);
                if (openCount != 1) continue; // only true dead ends

                // Wall_Light on the wall opposite the open passage
                if (nOpen) TagH(r,     c, 2); // S wall (opposite of N opening)
                if (sOpen) TagH(r + 1, c, 2); // N wall
                if (eOpen) TagV(r, c    , 2); // W wall
                if (wOpen) TagV(r, c + 1, 2); // E wall

                // Wall_Lamp on the two side walls (perpendicular to the passage)
                if (nOpen || sOpen)
                {
                    TagV(r, c    , 1);  // W wall of cell
                    TagV(r, c + 1, 1);  // E wall of cell
                }
                if (eOpen || wOpen)
                {
                    TagH(r,     c, 1);  // S wall
                    TagH(r + 1, c, 1);  // N wall
                }
            }
        }

        // Sprinkle ambient lamps on the rest of the interior closed walls
        for (int r = 1; r < ROWS; r++) // interior horizontal lines (skip outer)
            for (int c = 0; c < COLS; c++)
                if (hLine[r, c] && tagH[r, c] == 0 && rng.NextDouble() < AMBIENT_LAMP_RATE)
                    tagH[r, c] = 1;

        for (int r = 0; r < ROWS; r++)
            for (int c = 1; c < COLS; c++) // interior vertical lines (skip outer)
                if (vLine[r, c] && tagV[r, c] == 0 && rng.NextDouble() < AMBIENT_LAMP_RATE)
                    tagV[r, c] = 1;
    }

    // Boundary walls (r==0, r==ROWS, c==0, c==COLS) are NEVER tagged so the
    // outer perimeter is always solid Wall_3M with only the entrance/exit gaps.
    static void TagH(int r, int c, int tag)
    {
        if (r <= 0 || r >= ROWS) return;     // skip south & north boundaries
        if (c < 0 || c >= COLS) return;
        if (!hLine[r, c]) return;
        if (tagH[r, c] >= tag) return;
        tagH[r, c] = tag;
    }

    static void TagV(int r, int c, int tag)
    {
        if (c <= 0 || c >= COLS) return;     // skip west & east boundaries
        if (r < 0 || r >= ROWS) return;
        if (!vLine[r, c]) return;
        if (tagV[r, c] >= tag) return;
        tagV[r, c] = tag;
    }

    // ---- Scene building --------------------------------------------------

    static void PlaceGround(Transform parent)
    {
        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        float w = COLS * CELL;
        float h = ROWS * CELL;
        // Parent has scale MAZE_SCALE, so divide the plane's local scale so the
        // world footprint exactly covers the maze (Unity Plane = 10 units wide at scale 1).
        ground.transform.SetParent(parent, false);
        ground.transform.localPosition = new Vector3(w * 0.5f, -0.05f, h * 0.5f);
        ground.transform.localScale    = new Vector3(w * (0.1f / 1f), 1f, h * (0.1f / 1f));
    }

    static void PlaceFloor(Transform parent)
    {
        for (int r = 0; r < ROWS; r++)
            for (int c = 0; c < COLS; c++)
                Inst(parent, pfbFloor, (c + 1) * CELL, 0f, r * CELL, 0f);
    }

    // Every closed wall gets a solid Wall_3M (so the maze is truly enclosed).
    // Overlays are placed on top:
    //   Wall_Lamp  – raised LAMP_Y above base, nudged LAMP_FACE_OFFSET off the
    //                wall face so it sits on the corridor-facing surface.
    //                Horizontal walls: offset in +Z.  Vertical (90°Y): offset
    //                in -X (because 90°Y maps local +Z → parent -X).
    //   Wall_Light – y=0, no face offset; FBX geometry self-positions at top.
    static void PlaceWalls(Transform parent)
    {
        // Horizontal walls
        for (int r = 0; r <= ROWS; r++)
        {
            for (int c = 0; c < COLS; c++)
            {
                if (!hLine[r, c]) continue;
                float px = (c + 1) * CELL, pz = r * CELL;

                Inst(parent, pfbWall, px, 0f, pz, 0f);

                int tag = tagH[r, c];
                GameObject overlay = TagToOverlay(tag);
                if (overlay != null)
                {
                    bool isLamp = tag == 1;
                    Inst(parent, overlay,
                         isLamp ? px - CELL * 0.5f : px,
                         isLamp ? LAMP_Y : 0f,
                         isLamp ? pz + LAMP_FACE_OFFSET : pz,
                         0f);
                }
            }
        }

        // Vertical walls
        for (int r = 0; r < ROWS; r++)
        {
            for (int c = 0; c <= COLS; c++)
            {
                if (!vLine[r, c]) continue;
                float px = c * CELL, pz = r * CELL;

                Inst(parent, pfbWall, px, 0f, pz, 90f);

                int tag = tagV[r, c];
                GameObject overlay = TagToOverlay(tag);
                if (overlay != null)
                {
                    bool isLamp = tag == 1;
                    Inst(parent, overlay,
                         isLamp ? px - LAMP_FACE_OFFSET : px,
                         isLamp ? LAMP_Y : 0f,
                         isLamp ? pz + CELL * 0.5f : pz,
                         isLamp ? 270f : 90f);
                }
            }
        }
    }

    static GameObject TagToOverlay(int tag)
    {
        if (tag == 1 && pfbLamp  != null) return pfbLamp;
        if (tag == 2 && pfbLight != null) return pfbLight;
        return null;
    }

    // Wall_Door at entrance, exit, and random interior passages.
    static void PlaceDoors(Transform parent)
    {
        if (pfbDoor == null) return;

        // Entrance (south boundary opening)
        Inst(parent, pfbDoor, (entryCol + 1) * CELL, 0f, 0f, 0f);
        // Exit (north boundary opening)
        Inst(parent, pfbDoor, (exitCol + 1) * CELL, 0f, ROWS * CELL, 0f);

        // Random interior passages get a door arch
        for (int r = 1; r < ROWS; r++)
        {
            for (int c = 0; c < COLS; c++)
            {
                if (hLine[r, c]) continue;
                if (rng.NextDouble() < DOOR_CHANCE)
                    Inst(parent, pfbDoor, (c + 1) * CELL, 0f, r * CELL, 0f);
            }
        }
        for (int r = 0; r < ROWS; r++)
        {
            for (int c = 1; c < COLS; c++)
            {
                if (vLine[r, c]) continue;
                if (rng.NextDouble() < DOOR_CHANCE)
                    Inst(parent, pfbDoor, c * CELL, 0f, r * CELL, 90f);
            }
        }
    }

    static void PlacePillars(Transform parent)
    {
        for (int r = 0; r <= ROWS; r++)
        {
            for (int c = 0; c <= COLS; c++)
            {
                int wallCount = 0;
                if (c < COLS && hLine[r, c])         wallCount++;
                if (c > 0    && hLine[r, c - 1])     wallCount++;
                if (r < ROWS && vLine[r, c])         wallCount++;
                if (r > 0    && vLine[r - 1, c])     wallCount++;
                if (wallCount >= 2)
                    Inst(parent, pfbPillar, c * CELL, 0f, r * CELL, 0f);
            }
        }
    }

    static void PlaceSwitch(Transform parent)
    {
        if (pfbSwitch == null) return;

        int[,] dist = new int[ROWS, COLS];
        Fill(dist, -1);

        var queue = new Queue<(int r, int c)>();
        dist[0, entryCol] = 0;
        queue.Enqueue((0, entryCol));

        int[] dr = {  1, -1,  0,  0 };
        int[] dc = {  0,  0,  1, -1 };
        int   fr = 0, fc = entryCol;

        while (queue.Count > 0)
        {
            var (r, c) = queue.Dequeue();
            if (dist[r, c] > dist[fr, fc]) { fr = r; fc = c; }
            for (int d = 0; d < 4; d++)
            {
                int nr = r + dr[d], nc = c + dc[d];
                if (nr < 0 || nr >= ROWS || nc < 0 || nc >= COLS || dist[nr, nc] >= 0) continue;
                bool passage = d == 0 ? !hLine[r + 1, c]
                             : d == 1 ? !hLine[r,     c]
                             : d == 2 ? !vLine[r, c + 1]
                             :          !vLine[r, c    ];
                if (!passage) continue;
                dist[nr, nc] = dist[r, c] + 1;
                queue.Enqueue((nr, nc));
            }
        }

        float lx = (fc + 0.5f) * CELL, lz = (fr + 0.5f) * CELL;
        Inst(parent, pfbSwitch, lx, 0f, lz, 0f);
        Debug.Log("[MazeGen] Goal Switch at cell (" + fr + "," + fc + ")  dist=" + dist[fr, fc]);
    }

    // Scatter CHEST_COUNT chests on random accessible floor cells.
    // Each chest gets its own world-space position (no scaled parent) so
    // ChestCollectible distance checks work correctly at runtime.
    static void SpawnChests()
    {
        if (pfbChest == null)
        {
            Debug.LogWarning("[MazeGen] LootChest prefab not found at " + P_CHEST + " — skipping chest spawn.");
            return;
        }

        // Collect every interior cell (skip row 0 = entry area)
        var cells = new List<(int r, int c)>();
        for (int r = 2; r < ROWS - 1; r++)
            for (int c = 0; c < COLS; c++)
                cells.Add((r, c));

        // Fisher-Yates shuffle
        for (int i = cells.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            var tmp = cells[i]; cells[i] = cells[j]; cells[j] = tmp;
        }

        var container = new GameObject("MazeChests");
        Undo.RegisterCreatedObjectUndo(container, "MazeGen chests");

        const int MIN_CELL_GAP = 3; // chests must be at least this many cells apart
        var placed_cells = new List<(int r, int c)>();

        int placed = 0;
        foreach (var (r, c) in cells)
        {
            if (placed >= CHEST_COUNT) break;

            // Reject if too close to an already-placed chest
            bool tooClose = false;
            foreach (var (pr, pc) in placed_cells)
            {
                if (Mathf.Abs(r - pr) + Mathf.Abs(c - pc) < MIN_CELL_GAP)
                { tooClose = true; break; }
            }
            if (tooClose) continue;
            placed_cells.Add((r, c));

            // World-space cell centre (parent scale already applied via multiply)
            float wx = (c * CELL + CELL * 0.5f) * MAZE_SCALE;
            float wz = (r * CELL + CELL * 0.5f) * MAZE_SCALE;

            var go = (GameObject)PrefabUtility.InstantiatePrefab(pfbChest);
            go.transform.SetParent(container.transform, true);
            go.transform.position    = new Vector3(wx, 0.8f, wz);
            go.transform.rotation    = Quaternion.Euler(0f, 180f, 0f);
            go.transform.localScale  = new Vector3(100f, 100f, 100f);
            go.name = "LootChest_" + placed;
            Undo.RegisterCreatedObjectUndo(go, "MazeGen chest");
            placed++;
        }

        Debug.Log("[MazeGen] Spawned " + placed + " chests.");
    }

    // ---- Utilities -------------------------------------------------------

    // Instantiate prefab, parent it (world-position-stays=false so local coords
    // are preserved and MAZE_SCALE on the parent doubles everything in world space).
    static GameObject Inst(Transform parent, GameObject prefab, float lx, float ly, float lz, float eulerY)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        go.transform.SetParent(parent, false);
        go.transform.localPosition    = new Vector3(lx, ly, lz);
        go.transform.localEulerAngles = new Vector3(0f, eulerY, 0f);
        return go;
    }

    static void ClearScene()
    {
        foreach (string n in new[] { "MazeTerrain", "MazeWalls", "MazeDoors", "MazeChests" })
        {
            var go = GameObject.Find(n);
            if (go != null) Undo.DestroyObjectImmediate(go);
        }
    }

    static bool LoadPrefabs()
    {
        pfbWall   = Load(P_WALL);
        pfbFloor  = Load(P_FLOOR);
        pfbPillar = Load(P_PILLAR);
        pfbLamp   = Load(P_LAMP);
        pfbLight  = Load(P_LIGHT);
        pfbSwitch = Load(P_SWITCH);
        pfbDoor   = Load(P_DOOR);
        pfbChest  = Load(P_CHEST);

        if (pfbWall == null || pfbFloor == null)
        {
            Debug.LogError("[MazeGen] Required prefab missing - aborting.");
            return false;
        }
        if (pfbDoor == null) Debug.LogWarning("[MazeGen] Wall_Door missing - using empty passages.");
        return true;
    }

    static GameObject Load(string path)
    {
        var pfb = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (pfb == null) Debug.LogWarning("[MazeGen] Missing: " + path);
        return pfb;
    }

    static void Fill(bool[,] arr, bool val)
    {
        for (int i = 0; i < arr.GetLength(0); i++)
            for (int j = 0; j < arr.GetLength(1); j++)
                arr[i, j] = val;
    }

    static void Fill(int[,] arr, int val)
    {
        for (int i = 0; i < arr.GetLength(0); i++)
            for (int j = 0; j < arr.GetLength(1); j++)
                arr[i, j] = val;
    }
}
