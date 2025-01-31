using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FloorGenerator : MonoBehaviour
{
    public GameObject streetPrefab; // Street prefab

    public GameObject potholePrefab; // Street prefab
    public GameObject crackedPrefab; // Dirt prefab
    public GameObject dirtPrefab; // Dirt prefab
    public GameObject closedPrefab; // Array of building prefabs
    public GameObject[] buildingPrefabs; // Array of building prefabs
    public GameObject walkerPrefab; // Walking person prefab
    public GameObject[] obstaclePrefabs; // Array of obstacle prefabs (e.g., trees, benches)
    public GameObject footprintPrefab; // Footprint effect prefab


    public int[,] floorMatrix = new int[,] {
   {1,2}
};

    public float cellSize = 1.0f; // Fixed grid cell size
    public float walkerYOffset = 0.2f; // Offset for walker to hover above the ground

    private List<Vector2Int> walkableTiles = new List<Vector2Int>();
    private List<Vector2Int> obstaclePositions = new List<Vector2Int>();
    private GameObject walker;

    private Queue<Vector2Int> agentMovementQueue = new Queue<Vector2Int>(); // Queue for agent steps

    private bool isMoving = false;

    void Start()
    {
        GenerateFloor();             // Instantiate tiles
        PlaceBuildings();           // Spawn the walking person
    }


    public void BuildCityFromMatrix(int[,] matrix)
    {
        // Clear existing objects in the scene (optional, for regeneration)
        foreach (Transform child in transform)
        {
            Destroy(child.gameObject);
        }


        // Update floorMatrix and regenerate the environment
        floorMatrix = matrix;


        GenerateFloor();             // Instantiate tiles
                                     // Place obstacles on the sidewalks

        PlaceBuildings();

        Debug.Log("City generated from matrix.");
    }


    void PlaceBuildings()
    {
        bool[,] visited = new bool[floorMatrix.GetLength(0), floorMatrix.GetLength(1)];

        int regionCount = 0; // Count the number of distinct regions

        for (int x = 0; x < floorMatrix.GetLength(0); x++)
        {
            for (int z = 0; z < floorMatrix.GetLength(1); z++)
            {
                if (floorMatrix[x, z] == -10 && !visited[x, z])
                {
                    // Find all connected tiles for this building
                    List<Vector2Int> buildingTiles = GetConnectedTiles(x, z, visited);

                    if (buildingTiles.Count > 0)
                    {
                        regionCount++;
                        Debug.Log($"Region {regionCount}: {buildingTiles.Count} tiles");
                        PlaceBuilding(buildingTiles);
                    }
                }
            }
        }

        Debug.Log($"Total building regions: {regionCount}");
    }



    List<Vector2Int> GetConnectedTiles(int startX, int startZ, bool[,] visited)
    {
        List<Vector2Int> connectedTiles = new List<Vector2Int>();
        Queue<Vector2Int> toVisit = new Queue<Vector2Int>();
        toVisit.Enqueue(new Vector2Int(startX, startZ));

        while (toVisit.Count > 0)
        {
            Vector2Int current = toVisit.Dequeue();

            // Skip already visited tiles
            if (visited[current.x, current.y])
                continue;

            // Mark current tile as visited
            visited[current.x, current.y] = true;
            connectedTiles.Add(current);

            // Debug log for visualization
            Debug.DrawRay(new Vector3(current.x * cellSize, 0.5f, current.y * cellSize), Vector3.up, Color.red, 5);

            // Check neighbors
            Vector2Int[] neighbors = {
            new Vector2Int(current.x + 1, current.y),
            new Vector2Int(current.x - 1, current.y),
            new Vector2Int(current.x, current.y + 1),
            new Vector2Int(current.x, current.y - 1)
        };

            foreach (var neighbor in neighbors)
            {
                if (neighbor.x >= 0 && neighbor.x < floorMatrix.GetLength(0) &&
                    neighbor.y >= 0 && neighbor.y < floorMatrix.GetLength(1) &&
                    floorMatrix[neighbor.x, neighbor.y] == -10 && !visited[neighbor.x, neighbor.y])
                {
                    toVisit.Enqueue(neighbor);
                }
            }
        }

        return connectedTiles;
    }

    void PlaceBuilding(List<Vector2Int> buildingTiles)
    {
        // Calculate the bounds of the building
        int minX = int.MaxValue, minZ = int.MaxValue, maxX = int.MinValue, maxZ = int.MinValue;

        foreach (Vector2Int tile in buildingTiles)
        {
            minX = Mathf.Min(minX, tile.x);
            minZ = Mathf.Min(minZ, tile.y);
            maxX = Mathf.Max(maxX, tile.x);
            maxZ = Mathf.Max(maxZ, tile.y);
        }

        // Calculate the center and size of the building based on the grid
        float width = (maxX - minX + 1) * cellSize;
        float depth = (maxZ - minZ + 1) * cellSize;
        Vector3 center = new Vector3((minX + maxX + 1) / 2f * cellSize, 0, (minZ + maxZ + 1) / 2f * cellSize);

        // Choose a random building prefab
        GameObject buildingPrefab = buildingPrefabs[Random.Range(0, buildingPrefabs.Length)];

        // Instantiate the building
        GameObject building = Instantiate(buildingPrefab, center, Quaternion.identity, this.transform);

        // Try to get the Renderer (check child objects if needed)
        Renderer renderer = building.GetComponentInChildren<Renderer>();
        if (renderer != null)
        {
            // Calculate the original size of the prefab
            Vector3 prefabOriginalSize = renderer.bounds.size;

            // Adjust the scale to fit the grid dimensions exactly
            Vector3 scaleAdjustment = building.transform.localScale;

            // Ensure proper scaling based on prefab's current dimensions
            scaleAdjustment.x = (width / prefabOriginalSize.x) * scaleAdjustment.x;
            scaleAdjustment.z = (depth / prefabOriginalSize.z) * scaleAdjustment.z;

            building.transform.localScale = scaleAdjustment;

            Debug.Log($"Scaled building to fit width: {width}, depth: {depth}");
        }
        else
        {
            Debug.LogWarning($"Prefab {buildingPrefab.name} has no Renderer! Using default scaling.");
            building.transform.localScale = new Vector3(width / cellSize, 1, depth / cellSize);
        }

        // Ensure the building stays within the grid bounds
        building.transform.position = center;
    }


    // Instantiate tiles based on the floorMatrix
    void GenerateFloor()
    {
        for (int x = 0; x < floorMatrix.GetLength(0); x++)
        {
            for (int z = 0; z < floorMatrix.GetLength(1); z++)
            {
                GameObject prefab = GetPrefabForValue(floorMatrix[x, z]);
                if (prefab != null)
                {
                    Vector3 position = new Vector3(x * cellSize, 0, z * cellSize);
                    Instantiate(prefab, position, Quaternion.identity, this.transform);

                    // Store walkable tiles for the walker
                    if (floorMatrix[x, z] == 1)
                    {
                        walkableTiles.Add(new Vector2Int(x, z));
                    }
                }
            }
        }
    }

    // Map matrix values to prefabs
    GameObject GetPrefabForValue(int value)
    {
        switch (value)
        {
            case 1: return streetPrefab;  // Street
            case 2: return dirtPrefab;   // Sidewalk
            case 4: return crackedPrefab;   // Sidewalk
            case 5: return potholePrefab;   // Sidewalk
            case -1: return closedPrefab; // Closed building
            default: return null;         // Obstacles (`3`) are handled separately
        }
    }

    public void SpawnWalker(Vector2Int position)
    {
        if (walker == null) // Only instantiate the walker if it doesn't already exist
        {
            Vector3 startPosition = new Vector3(position.x * cellSize, 0, position.y * cellSize);
            startPosition.y = GetGroundYAtPosition(startPosition) + walkerYOffset; // Adjust for ground height
            walker = Instantiate(walkerPrefab, startPosition, Quaternion.identity);
            Debug.Log($"Walker spawned at: {position}");
        }
        else
        {
            // If the walker already exists, smoothly move it to the new position
            Vector3 newPosition = new Vector3(position.x * cellSize, 0, position.y * cellSize);
            newPosition.y = GetGroundYAtPosition(newPosition) + walkerYOffset;
            StartCoroutine(SmoothMoveWalker(newPosition));
        }
    }
    private IEnumerator SmoothMoveWalker(Vector3 targetPosition)
    {
        float elapsedTime = 0f;
        float moveDuration = 0.75f; // Duration for the smooth movement
        Vector3 startPosition = walker.transform.position;

        // Calculate direction to face
        Vector3 direction = (targetPosition - startPosition).normalized;
        if (direction != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(new Vector3(direction.x, 0, direction.z));
            float rotationSpeed = 10f; // Adjust for smoother rotation

            // Smoothly rotate towards the target direction
            while (Quaternion.Angle(walker.transform.rotation, targetRotation) > 1f)
            {
                walker.transform.rotation = Quaternion.Slerp(walker.transform.rotation, targetRotation, Time.deltaTime * rotationSpeed);
                yield return null;
            }
        }

        // Smoothly move the walker to the new position
        while (elapsedTime < moveDuration)
        {
            walker.transform.position = Vector3.Lerp(startPosition, targetPosition, elapsedTime / moveDuration);
            elapsedTime += Time.deltaTime;
            yield return null; // Wait for the next frame
        }

        walker.transform.position = targetPosition; // Ensure final position

        // Create a footprint at the final position
        if (footprintPrefab != null)
        {
            Instantiate(footprintPrefab, new Vector3(targetPosition.x, GetGroundYAtPosition(targetPosition), targetPosition.z), Quaternion.identity);
        }

        Debug.Log($"Walker moved smoothly to: {targetPosition}");
    }



    // Get valid neighboring tiles for walking
    List<Vector2Int> GetValidNeighbors(Vector2Int currentTile)
    {
        List<Vector2Int> neighbors = new List<Vector2Int>();
        Vector2Int[] directions = {
            new Vector2Int(0, 1),  // Up
            new Vector2Int(0, -1), // Down
            new Vector2Int(-1, 0), // Left
            new Vector2Int(1, 0)   // Right
        };

        foreach (Vector2Int direction in directions)
        {
            Vector2Int neighbor = currentTile + direction;
            if (neighbor.x >= 0 && neighbor.x < floorMatrix.GetLength(0) &&
                neighbor.y >= 0 && neighbor.y < floorMatrix.GetLength(1) &&
                floorMatrix[neighbor.x, neighbor.y] == 1) // Only walkable tiles
            {
                neighbors.Add(neighbor);
            }
        }

        return neighbors;
    }

    // Determine the Y position of the ground using raycasting
    float GetGroundYAtPosition(Vector3 position)
    {
        RaycastHit hit;
        if (Physics.Raycast(new Vector3(position.x, 10f, position.z), Vector3.down, out hit, Mathf.Infinity))
        {
            return hit.point.y;
        }
        return 0f;
    }

}

