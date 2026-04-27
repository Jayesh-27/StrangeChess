using UnityEngine;

public class Chessboard : MonoBehaviour
{
    private const int TILE_COUNT_X = 8;
    private const int TILE_COUNT_Y = 8;

    private GameObject[,] tiles;
    [SerializeField] private Material tileMaterial;
    [SerializeField] private float tileSize = 1f;

    private Camera currentCamera;
    private Vector2Int currentHover;
    private GameObject selectedPiece;

    private void Awake()
    {
        GenerateAllTiles(tileSize, TILE_COUNT_X, TILE_COUNT_Y);
    }

    private void Start()
    {
        currentCamera = Camera.main;
        currentHover = -Vector2Int.one;
    }

    private void Update()
    {
        if (currentCamera == null) return;

        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = currentCamera.ScreenPointToRay(Input.mousePosition);

            if (Physics.Raycast(ray, out RaycastHit hit, 100))
            {
                // 1. If player clicked a chess piece
                if (hit.collider.CompareTag("Piece"))
                {
                    selectedPiece = hit.collider.gameObject;
                    Debug.Log("Selected Piece: " + selectedPiece.name);
                    return;
                }

                // 2. If player clicked a tile
                Vector2Int hitPosition = LookupTileIndex(hit.transform.gameObject);

                if (hitPosition.x != -1 && hitPosition.y != -1)
                {
                    Debug.Log("Clicked Tile: " + hitPosition.x + ", " + hitPosition.y);

                    if (selectedPiece != null)
                    {
                        MovePieceToTile(selectedPiece, hitPosition.x, hitPosition.y);
                    }
                }
            }
        }
    }

    private void MovePieceToTile(GameObject piece, int x, int y)
    {
        Vector3 newPosition = new Vector3(
            x * tileSize + tileSize / 2,
            piece.transform.position.y,
            y * tileSize + tileSize / 2
        );

        piece.transform.position = newPosition;

        Debug.Log(piece.name + " moved to: " + x + ", " + y);
    }

    private void GenerateAllTiles(float tileSize, int tileCountX, int tileCountY)
    {
        tiles = new GameObject[tileCountX, tileCountY];

        for (int x = 0; x < tileCountX; x++)
        {
            for (int y = 0; y < tileCountY; y++)
            {
                tiles[x, y] = GenerateSingleTile(tileSize, x, y);
            }
        }
    }

    private GameObject GenerateSingleTile(float tileSize, int x, int y)
    {
        GameObject tileObject = new GameObject($"X:{x}, Y:{y}");
        tileObject.transform.parent = transform;

        Mesh mesh = new Mesh();

        tileObject.AddComponent<MeshFilter>().mesh = mesh;
        tileObject.AddComponent<MeshRenderer>().material = tileMaterial;
        tileObject.AddComponent<BoxCollider>();

        Vector3[] vertices = new Vector3[4];
        vertices[0] = new Vector3(x * tileSize, 0, y * tileSize);
        vertices[1] = new Vector3(x * tileSize, 0, (y + 1) * tileSize);
        vertices[2] = new Vector3((x + 1) * tileSize, 0, y * tileSize);
        vertices[3] = new Vector3((x + 1) * tileSize, 0, (y + 1) * tileSize);

        int[] triangles = new int[]
        {
            0, 1, 2,
            2, 1, 3
        };

        Vector2[] uv = new Vector2[4];
        uv[0] = new Vector2(0, 0);
        uv[1] = new Vector2(0, 1);
        uv[2] = new Vector2(1, 0);
        uv[3] = new Vector2(1, 1);

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uv;
        mesh.RecalculateNormals();

        BoxCollider boxCollider = tileObject.GetComponent<BoxCollider>();
        boxCollider.center = new Vector3(tileSize / 2, 0, tileSize / 2);
        boxCollider.size = new Vector3(tileSize, 0.1f, tileSize);

        return tileObject;
    }

    private Vector2Int LookupTileIndex(GameObject hitInfo)
    {
        for (int x = 0; x < TILE_COUNT_X; x++)
        {
            for (int y = 0; y < TILE_COUNT_Y; y++)
            {
                if (tiles[x, y] == hitInfo)
                    return new Vector2Int(x, y);
            }
        }

        return -Vector2Int.one;
    }
}