using System.Collections.Generic;
using UnityEngine;

// Surface creases of the voxel union. Coplanar neighboring faces contribute
// the same normal to a shared edge, so their block-to-block seam is omitted.
public static class AerTargetOutline
{
    public static Mesh Build(int[,] heights)
    {
        var normals=new Dictionary<(Vector3Int,Vector3Int),Vector3Int>();
        var creases=new HashSet<(Vector3Int,Vector3Int)>();
        var corners=new[]{new Vector3Int(0,0,0),new Vector3Int(1,0,0),new Vector3Int(1,1,0),new Vector3Int(0,1,0),new Vector3Int(0,0,1),new Vector3Int(1,0,1),new Vector3Int(1,1,1),new Vector3Int(0,1,1)};
        int[][] faces={new[]{0,3,2,1},new[]{5,6,7,4},new[]{4,7,3,0},new[]{1,2,6,5},new[]{3,7,6,2},new[]{4,0,1,5}};
        var directions=new[]{new Vector3Int(0,0,-1),new Vector3Int(0,0,1),new Vector3Int(-1,0,0),new Vector3Int(1,0,0),new Vector3Int(0,1,0),new Vector3Int(0,-1,0)};
        bool Occupied(Vector3Int p)=>p.x>=0&&p.x<5&&p.z>=0&&p.z<5&&p.y>=0&&p.y<heights[p.x,4-p.z];
        bool Before(Vector3Int a,Vector3Int b)=>a.x!=b.x?a.x<b.x:a.y!=b.y?a.y<b.y:a.z<b.z;
        for(int x=0;x<5;x++)for(int z=0;z<5;z++)for(int h=0;h<heights[x,4-z];h++)
        {
            var origin=new Vector3Int(x,h,z);
            for(int f=0;f<6;f++)
            {
                if(Occupied(origin+directions[f]))continue;
                for(int i=0;i<4;i++)
                {
                    var a=origin+corners[faces[f][i]];var b=origin+corners[faces[f][(i+1)%4]];
                    var key=Before(a,b)?(a,b):(b,a);
                    if(normals.TryGetValue(key,out var previous)){if(previous!=directions[f])creases.Add(key);}
                    else normals.Add(key,directions[f]);
                }
            }
        }
        var vertices=new List<Vector3>();var indices=new List<int>();
        foreach(var crease in creases)
        {
            indices.Add(vertices.Count);vertices.Add((Vector3)crease.Item1+new Vector3(-.5f,0,-.5f));
            indices.Add(vertices.Count);vertices.Add((Vector3)crease.Item2+new Vector3(-.5f,0,-.5f));
        }
        var mesh=new Mesh{name="Target exterior creases"};mesh.SetVertices(vertices);mesh.SetIndices(indices,MeshTopology.Lines,0);mesh.RecalculateBounds();return mesh;
    }
}
