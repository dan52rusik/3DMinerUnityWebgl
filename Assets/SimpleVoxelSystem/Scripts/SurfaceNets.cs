using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public class SurfaceNets : MonoBehaviour
{
    public MeshFilter meshFilter;
    [Space]
    public int size;
    public const float threshold = 0;
    [Space]
    public Vector3 spherePosition = new Vector3(16, 16, 16);
    public int sphereRadius = 16;

    Chunk chunk;

    private void Start()
    {
        var watch = new System.Diagnostics.Stopwatch();
        watch.Start();
        PopulateChunk();
        Mesh mesh = GenerateChunk();
        meshFilter.mesh = mesh;
    }

    /// <summary>
    /// Fills the voxel grid with values
    /// </summary>
    public void PopulateChunk()
    {
        chunk = new Chunk(size);
        chunk.position = transform.position;

        for (int x = 0; x < chunk.size; x++)
        {
            for (int y = 0; y < chunk.size; y++)
            {
                for (int z = 0; z < chunk.size; z++)
                {
                    Vector3 position = new Vector3(x, y, z) + chunk.position;

                    //I used a sphere for example, if you want more check out: https://iquilezles.org/articles/distfunctions/
                    float sphere = (spherePosition - new Vector3(position.x, position.y, position.z)).magnitude - sphereRadius;
                    chunk[new Vector3Int(x, y, z)] = sphere;
                }
            }
        }
    }

    /// <summary>
    /// Generates the chunk mesh
    /// </summary>
    public Mesh GenerateChunk()
    {
        List<int> signIndexes = new List<int>();
        List<Vector3> vertices = new List<Vector3>();
        Dictionary<Vector3Int, int> cubeToVertex = new Dictionary<Vector3Int, int>();
        List<Edge> edges = new List<Edge>();
        List<int> triangles = new List<int> ();

        Vector3Int e1, e2;
        float v1, v2, d1, d2;
        Vector3 p1, p2, point, finalPoint;
        Vector3Int q1, q2, q3, q4;

        //Loop through all voxels in chunk
        for (int x = 0; x < chunk.size; x++)
        {
            for (int y = 0; y < chunk.size; y++)
            {
                for (int z = 0; z < chunk.size; z++)
                {
                    signIndexes.Clear();
                    Vector3Int pos = new Vector3Int(x, y, z);

                    //Check edges
                    for (int i = 0; i < 12; i++)
                    {
                        e1 = pos + edgePositions[i][0];
                        e2 = pos + edgePositions[i][1];

                        if (e1.x >= chunk.size || e1.y >= chunk.size || e1.z >= chunk.size)
                            continue;

                        if (e2.x >= chunk.size || e2.y >= chunk.size || e2.z >= chunk.size)
                            continue;

                        v1 = chunk[pos + edgePositions[i][0]];
                        v2 = chunk[pos + edgePositions[i][1]];


                        //If theres a sign change, add edge index and edge to list
                        if (CheckEdgeSignChange(v1, v2))
                        {
                            signIndexes.Add(i);
                            edges.Add(new Edge(pos + edgePositions[i][0], pos + edgePositions[i][1], v1 > threshold));
                        }
                    }

                    //get point on surface for vertex
                    if (signIndexes.Count > 0)
                    {
                        finalPoint = Vector3.zero;

                        //loop through crossing edges and average surface points
                        for (int i = 0; i < signIndexes.Count; i++)
                        {
                            p1 = pos + edgePositions[signIndexes[i]][0];
                            p2 = pos + edgePositions[signIndexes[i]][1];

                            d1 = chunk[pos + edgePositions[signIndexes[i]][0]];
                            d2 = chunk[pos + edgePositions[signIndexes[i]][1]];

                            //Linear interpolation between the two edge points. Simalar to lerp with T = (d1 / (d1 - d2))
                            point = p1 + (p2 - p1) * (d1 / (d1 - d2));

                            finalPoint += point;
                        }

                        //Average points and add to vertex list

                        finalPoint /= signIndexes.Count;

                        vertices.Add(finalPoint);
                        cubeToVertex.Add(pos, vertices.Count - 1);
                    }
                }
            }
        }

        /* Quad visualization
         * q2    q1
         * 
         * 
         * q4    q3
         */

        //Loop through sign edges and generate quads
        foreach (Edge edge in edges)
        {
            Vector3Int edgeDir = edge.p2 - edge.p1;

            if (edge.p1.x == 0 || edge.p1.y == 0 || edge.p1.z == 0)
                continue;

            if (edge.p2.x == 0 || edge.p2.y == 0 || edge.p2.z == 0)
                continue;

            if (edgeDir == Vector3Int.forward)
            {
                q1 = edge.p1;
                q2 = edge.p1 + Vector3Int.left;
                q3 = edge.p1 + Vector3Int.down;
                q4 = edge.p1 + Vector3Int.left + Vector3Int.down;

                //Reverse face depening on sign
                if (!edge.sign)
                {
                    triangles.Add(cubeToVertex[q1]);
                    triangles.Add(cubeToVertex[q2]);
                    triangles.Add(cubeToVertex[q3]);
                    triangles.Add(cubeToVertex[q3]);
                    triangles.Add(cubeToVertex[q2]);
                    triangles.Add(cubeToVertex[q4]);
                }
                else
                {
                    triangles.Add(cubeToVertex[q3]);
                    triangles.Add(cubeToVertex[q2]);
                    triangles.Add(cubeToVertex[q1]);
                    triangles.Add(cubeToVertex[q4]);
                    triangles.Add(cubeToVertex[q2]);
                    triangles.Add(cubeToVertex[q3]);
                }
            }

            if (edgeDir == Vector3Int.right)
            {
                q1 = edge.p1;
                q2 = edge.p1 + Vector3Int.back;
                q3 = edge.p1 + Vector3Int.down;
                q4 = edge.p1 + Vector3Int.back + Vector3Int.down;

                if (!edge.sign)
                {
                    triangles.Add(cubeToVertex[q3]);
                    triangles.Add(cubeToVertex[q2]);
                    triangles.Add(cubeToVertex[q1]);
                    triangles.Add(cubeToVertex[q4]);
                    triangles.Add(cubeToVertex[q2]);
                    triangles.Add(cubeToVertex[q3]);
                }
                else
                {
                    triangles.Add(cubeToVertex[q1]);
                    triangles.Add(cubeToVertex[q2]);
                    triangles.Add(cubeToVertex[q3]);
                    triangles.Add(cubeToVertex[q3]);
                    triangles.Add(cubeToVertex[q2]);
                    triangles.Add(cubeToVertex[q4]);
                }
            }

            if(edgeDir == Vector3Int.up)
            {
                q1 = edge.p1;
                q2 = edge.p1 + Vector3Int.left;
                q3 = edge.p1 + Vector3Int.back;
                q4 = edge.p1 + Vector3Int.left + Vector3Int.back;

                if (!edge.sign)
                {
                    triangles.Add(cubeToVertex[q3]);
                    triangles.Add(cubeToVertex[q2]);
                    triangles.Add(cubeToVertex[q1]);
                    triangles.Add(cubeToVertex[q4]);
                    triangles.Add(cubeToVertex[q2]);
                    triangles.Add(cubeToVertex[q3]);
                }
                else
                {
                    triangles.Add(cubeToVertex[q1]);
                    triangles.Add(cubeToVertex[q2]);
                    triangles.Add(cubeToVertex[q3]);
                    triangles.Add(cubeToVertex[q3]);
                    triangles.Add(cubeToVertex[q2]);
                    triangles.Add(cubeToVertex[q4]);
                }
            }
        }

        //Basic mesh creation
        Mesh mesh = new Mesh();
        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.RecalculateBounds();
        mesh.RecalculateNormals();

        return mesh;
    }

    //Check if the SDF between two points is a sign change
    public bool CheckEdgeSignChange(float p1, float p2)
    {
        if (p1 > threshold != p2 > threshold)
            return true;

        return false;
    }

    //Cached edges
    public readonly Vector3Int[][] edgePositions = new Vector3Int[12][]
    {
        new Vector3Int[2] { new Vector3Int(0,0,0), new Vector3Int(0,0,1)},
        new Vector3Int[2] { new Vector3Int(0,1,0), new Vector3Int(0,1,1)},
        new Vector3Int[2] { new Vector3Int(1,0,0), new Vector3Int(1,0,1)},
        new Vector3Int[2] { new Vector3Int(1,1,0), new Vector3Int(1,1,1)},
        new Vector3Int[2] { new Vector3Int(0,0,0), new Vector3Int(1,0,0)},
        new Vector3Int[2] { new Vector3Int(0,1,0), new Vector3Int(1,1,0)},
        new Vector3Int[2] { new Vector3Int(0,0,1), new Vector3Int(1,0,1)},
        new Vector3Int[2] { new Vector3Int(0,1,1), new Vector3Int(1,1,1)},
        new Vector3Int[2] { new Vector3Int(0,0,0), new Vector3Int(0,1,0)},
        new Vector3Int[2] { new Vector3Int(0,1,1), new Vector3Int(0,1,1)},
        new Vector3Int[2] { new Vector3Int(1,0,0), new Vector3Int(1,1,0)},
        new Vector3Int[2] { new Vector3Int(1,0,1), new Vector3Int(1,1,1)}
    };

    public class Edge
    {
        public Vector3Int p1; 
        public Vector3Int p2;
        public bool sign;

        public Edge(Vector3Int point1, Vector3Int point2, bool sign)
        {
            p1 = point1;
            p2 = point2;
            this.sign = sign;
        }
    }
}

public class Chunk
{
    public Vector3 position;
    public float[] voxels;
    public int size;
    
    public Chunk(int size)
    {
        this.size = size;
        voxels = new float[size * size * size];
    }

    public float this[Vector3Int point]
    {
        get { return voxels[PositionToIndex(point.x, point.y, point.z)]; }
        set { voxels[PositionToIndex(point.x, point.y, point.z)] = value; }
    }

    public int PositionToIndex(int x, int y, int z)
    {
        return x + y * size + z * size * size;
    }

    public int3 IndexToPosition(int index)
    {
        return new int3
        {
            z = index % size,
            y = (index / size) % size,
            x = index / (size * size)
        };
    }
}
