using System;
using System.Collections.Generic;
using UnityEngine;

public sealed partial class EveningPrototype
{
    static readonly Color TargetTint=new Color(.48f,.53f,.60f,.065f),TargetEdge=new Color(.24f,.28f,.34f,.92f);
    static readonly Color PredictionTint=new Color(.67f,.51f,.88f,.30f),PredictionEdge=new Color(.56f,.39f,.77f,.48f);
    static readonly Vector3Int[] FaceDirections={new Vector3Int(0,0,-1),new Vector3Int(0,0,1),Vector3Int.left,Vector3Int.right,Vector3Int.up,Vector3Int.down};
    readonly List<GameObject> predictionVisuals=new List<GameObject>(),candidateVisuals=new List<GameObject>();
    readonly Dictionary<GameObject,Mesh> ghostMeshes=new Dictionary<GameObject,Mesh>();
    Material targetMaterial,predictionMaterial,candidateMaterial;
    bool showPrediction=true,orbitMoved;
    int predictionVoxelCount,targetVoxelCount;
    string candidateKey="";

    void InitializeGhosts()
    {
        var shader=Shader.Find("Evening/Ghost");if(shader==null)throw new Exception("Missing planning ghost shader.");
        targetMaterial=new Material(shader){renderQueue=3030};predictionMaterial=new Material(shader){renderQueue=3010};candidateMaterial=new Material(shader){renderQueue=3020};
    }
    public static bool TryPlanningCell(Ray ray,out Vector2Int cell)
    {
        cell=default;
        if(!new Plane(Vector3.up,Vector3.zero).Raycast(ray,out float distance))return false;
        var point=ray.GetPoint(distance);int x=Mathf.FloorToInt(point.x+.5f),z=Mathf.FloorToInt(point.z+.5f);
        cell=new Vector2Int(x,4-z);return EveningBoard.Inside(cell);
    }
    void ResetBoardCamera()
    {yaw=30;elevation=42;distance=Mathf.Max(11,ResultHeight*1.4f+8);FrameCamera();}
    public static bool TryFlatPlanningCell(Vector2 mouse,out Vector2Int cell)
    {
        cell=default;if(!placementBoard.Contains(mouse))return false;
        float size=placementBoard.width/EveningBoard.Size;
        cell=new Vector2Int(Mathf.FloorToInt((mouse.x-placementBoard.x)/size),Mathf.FloorToInt((mouse.y-placementBoard.y)/size));
        return EveningBoard.Inside(cell);
    }
    void UpdateBoardInput(Vector2 mouse)
    {
        float step=Time.unscaledDeltaTime*55;
        if(Input.GetKey(KeyCode.A))yaw-=step;if(Input.GetKey(KeyCode.D))yaw+=step;
        if(Input.GetKey(KeyCode.W))elevation+=step;if(Input.GetKey(KeyCode.S))elevation-=step;
        if(viewport.Contains(mouse))
        {
            distance=Mathf.Clamp(distance-Input.mouseScrollDelta.y*.65f,6,Mathf.Max(80,ResultHeight*4+15));
            if(Input.GetMouseButtonDown(0)){dragging=true;orbitMoved=false;dragStart=dragPrevious=mouse;}
        }
        if(dragging&&Input.GetMouseButton(0))
        {
            if(!orbitMoved&&(mouse-dragStart).sqrMagnitude>36)orbitMoved=true;
            if(orbitMoved){var delta=mouse-dragPrevious;yaw+=delta.x*.32f;elevation+=delta.y*.32f;}
            dragPrevious=mouse;
        }
        if(!Input.GetMouseButton(0))dragging=false;
        FrameCamera();hasCell=!result&&TryFlatPlanningCell(mouse,out hover);
    }
    void RebuildPlanning()
    {
        if(result||world==null||targetMaterial==null)return;
        ClearVisuals();resultCamera.enabled=true;AddBoardBase();
        for(int x=0;x<5;x++)for(int y=0;y<5;y++)
        {
            var p=board.TopAt(new Vector2Int(x,y));if(p==null)continue;
            AddMesh(cube,solid,new Vector3(x,.018f,4-y),new Vector3(.91f,.025f,.91f),new Color(.94f,.915f,.975f));
        }
        BuildTargetGhost();
        var volume=new HashSet<Vector3Int>();foreach(var v in board.Voxels())volume.Add(new Vector3Int(v.X,v.Layer,4-v.Z));
        predictionVoxelCount=volume.Count;
        AddGhostVolume(volume,predictionMaterial,PredictionTint,PredictionEdge,.89f,predictionVisuals);
        foreach(var go in predictionVisuals)go.SetActive(showPrediction);
        if(Stage!=null&&hintIndex>=0)foreach(var c in Stage.HintSteps[hintIndex].Cells())
            AddMesh(edges,candidateMaterial,new Vector3(c.x,.045f,4-c.y),new Vector3(.94f,.035f,.94f),new Color(.13f,.43f,.74f,.85f));
        distance=Mathf.Max(distance,ResultHeight*1.1f+7);FrameCamera();RefreshCandidate3D();
    }
    void AddBoardBase()
    {
        AddMesh(cube,solid,new Vector3(2,-.16f,2),new Vector3(5.2f,.22f,5.2f),new Color(.84f,.83f,.80f));
        for(int x=0;x<5;x++)for(int z=0;z<5;z++)
        {
            AddMesh(cube,solid,new Vector3(x,-.025f,z),new Vector3(.97f,.025f,.97f),new Color(.93f,.92f,.89f));
            AddMesh(edges,edgeMaterial,new Vector3(x,-.01f,z),new Vector3(.97f,.025f,.97f),new Color(.70f,.70f,.67f));
        }
    }
    void BuildTargetGhost()
    {
        if(Stage==null)return;
        var volume=new HashSet<Vector3Int>();
        for(int x=0;x<5;x++)for(int y=0;y<5;y++)for(int layer=0;layer<Stage.Target[x,y];layer++)volume.Add(new Vector3Int(x,layer,4-y));
        targetVoxelCount=volume.Count;AddGhostVolume(volume,targetMaterial,TargetTint,TargetEdge,.995f,targetVisuals);
        var grid=BuildTargetGrid(volume);var gridObject=AddMesh(grid,targetMaterial,Vector3.zero,Vector3.one,new Color(.40f,.44f,.50f,.22f));
        ghostMeshes.Add(gridObject,grid);targetVisuals.Add(gridObject);
        foreach(var go in targetVisuals)go.SetActive(showTarget);
    }
    static Mesh BuildTargetGrid(HashSet<Vector3Int> volume)
    {
        // Deduplicate shared cube edges so inner grid lines keep a uniform faint opacity.
        var segments=new HashSet<(Vector3Int,Vector3Int)>();
        foreach(var cell in volume)for(int axis=0;axis<3;axis++)for(int a=-1;a<=1;a+=2)for(int b=-1;b<=1;b+=2)
        {
            var start=axis==0?new Vector3Int(-1,a,b):axis==1?new Vector3Int(a,-1,b):new Vector3Int(a,b,-1);
            var end=start;end[axis]=1;segments.Add((cell*2+start,cell*2+end));
        }
        var vertices=new List<Vector3>();var indices=new List<int>();
        foreach(var segment in segments)
        {
            indices.Add(vertices.Count);vertices.Add((Vector3)segment.Item1*.5f+Vector3.up*.5f);
            indices.Add(vertices.Count);vertices.Add((Vector3)segment.Item2*.5f+Vector3.up*.5f);
        }
        var mesh=new Mesh{name="Faint target cube boundaries",indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};
        mesh.SetVertices(vertices);mesh.SetIndices(indices.ToArray(),MeshTopology.Lines,0);mesh.RecalculateBounds();return mesh;
    }
    void AddGhostVolume(HashSet<Vector3Int> volume,Material material,Color fill,Color line,float size,List<GameObject> destination)
    {
        if(volume.Count==0)return;
        if(destination!=targetVisuals&&fill.a>0)
        {
            var surface=BuildVolumeSurface(volume);var body=AddMesh(surface,material,Vector3.zero,Vector3.one,fill);
            ghostMeshes.Add(body,surface);destination.Add(body);
        }
        var mesh=BuildVolumeOutline(volume);
        if(destination==targetVisuals){var stroke=BuildOutlineStroke(mesh,.035f);Destroy(mesh);mesh=stroke;}
        var go=AddMesh(mesh,material,Vector3.zero,Vector3.one,line);
        ghostMeshes.Add(go,mesh);destination.Add(go);
    }
    static Mesh BuildVolumeSurface(HashSet<Vector3Int> volume)
    {
        Vector3[] corners={new Vector3(-.5f,-.5f,-.5f),new Vector3(.5f,-.5f,-.5f),new Vector3(.5f,.5f,-.5f),new Vector3(-.5f,.5f,-.5f),new Vector3(-.5f,-.5f,.5f),new Vector3(.5f,-.5f,.5f),new Vector3(.5f,.5f,.5f),new Vector3(-.5f,.5f,.5f)};
        int[][] faces={new[]{0,3,2,1},new[]{5,6,7,4},new[]{4,7,3,0},new[]{1,2,6,5},new[]{3,7,6,2},new[]{4,0,1,5}};
        var vertices=new List<Vector3>();var triangles=new List<int>();
        // Only the union's exterior is drawn; internal cube faces must not accumulate opacity.
        foreach(var cell in volume)for(int f=0;f<6;f++)
        {
            if(volume.Contains(cell+FaceDirections[f]))continue;
            int start=vertices.Count;var center=new Vector3(cell.x,cell.y+.5f,cell.z);
            foreach(int corner in faces[f])vertices.Add(center+corners[corner]);
            triangles.AddRange(new[]{start,start+1,start+2,start,start+2,start+3});
        }
        var mesh=new Mesh{name="Merged translucent surface",indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};
        mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();return mesh;
    }
    static Mesh BuildOutlineStroke(Mesh outline,float thickness)
    {
        var points=outline.vertices;var indices=outline.GetIndices(0);var parts=new CombineInstance[indices.Length/2];
        var box=CubeMesh();
        for(int i=0;i<indices.Length;i+=2)
        {
            var a=points[indices[i]];var b=points[indices[i+1]];var delta=b-a;
            var size=new Vector3(Mathf.Abs(delta.x)+thickness,Mathf.Abs(delta.y)+thickness,Mathf.Abs(delta.z)+thickness);
            parts[i/2]=new CombineInstance{mesh=box,transform=Matrix4x4.TRS((a+b)*.5f,Quaternion.identity,size)};
        }
        var mesh=new Mesh{name="Bold target outline",indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};mesh.CombineMeshes(parts,true,true);Destroy(box);return mesh;
    }
    public static Mesh BuildVolumeOutline(HashSet<Vector3Int> volume)
    {
        Vector3Int[] corners={new Vector3Int(-1,-1,-1),new Vector3Int(1,-1,-1),new Vector3Int(1,1,-1),new Vector3Int(-1,1,-1),new Vector3Int(-1,-1,1),new Vector3Int(1,-1,1),new Vector3Int(1,1,1),new Vector3Int(-1,1,1)};
        int[][] faces={new[]{0,3,2,1},new[]{5,6,7,4},new[]{4,7,3,0},new[]{1,2,6,5},new[]{3,7,6,2},new[]{4,0,1,5}};
        var edgeNormals=new Dictionary<(Vector3Int,Vector3Int),int>();
        foreach(var cell in volume)for(int f=0;f<6;f++)
        {
            if(volume.Contains(cell+FaceDirections[f]))continue;
            for(int i=0;i<4;i++)
            {
                var a=cell*2+corners[faces[f][i]];var b=cell*2+corners[faces[f][(i+1)%4]];
                if(a.x>b.x||a.x==b.x&&(a.y>b.y||a.y==b.y&&a.z>b.z)){var swap=a;a=b;b=swap;}
                var key=(a,b);edgeNormals.TryGetValue(key,out int normals);edgeNormals[key]=normals|(1<<f);
            }
        }
        // Coplanar surface seams have only one normal. Keep only actual shape corners.
        var lines=new Dictionary<(int,int,int),List<int>>();
        foreach(var entry in edgeNormals)
        {
            int normals=entry.Value;if((normals&(normals-1))==0)continue;
            var a=entry.Key.Item1;var b=entry.Key.Item2;int axis=a.x!=b.x?0:a.y!=b.y?1:2;
            var key=axis==0?(axis,a.y,a.z):axis==1?(axis,a.x,a.z):(axis,a.x,a.y);
            if(!lines.TryGetValue(key,out var starts)){starts=new List<int>();lines[key]=starts;}starts.Add(a[axis]);
        }
        var vertices=new List<Vector3>();var indices=new List<int>();
        foreach(var entry in lines)
        {
            var starts=entry.Value;starts.Sort();int axis=entry.Key.Item1;
            for(int i=0;i<starts.Count;i++)
            {
                int start=starts[i],end=start+2;while(i+1<starts.Count&&starts[i+1]==end){i++;end+=2;}
                Vector3 a=axis==0?new Vector3(start,entry.Key.Item2,entry.Key.Item3):axis==1?new Vector3(entry.Key.Item2,start,entry.Key.Item3):new Vector3(entry.Key.Item2,entry.Key.Item3,start);
                var b=a;b[axis]=end;indices.Add(vertices.Count);vertices.Add(a*.5f+Vector3.up*.5f);indices.Add(vertices.Count);vertices.Add(b*.5f+Vector3.up*.5f);
            }
        }
        var mesh=new Mesh{name="Merged volume outline",indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};
        mesh.SetVertices(vertices);mesh.SetIndices(indices.ToArray(),MeshTopology.Lines,0);mesh.RecalculateBounds();return mesh;
    }
    void ReleaseGhostMesh(GameObject go)
    {if(ghostMeshes.TryGetValue(go,out var mesh)){ghostMeshes.Remove(go);Destroy(mesh);}}
    void ClearCandidate3D()
    {
        foreach(var go in candidateVisuals)if(go!=null){ReleaseGhostMesh(go);visuals.Remove(go);go.SetActive(false);Destroy(go);}candidateVisuals.Clear();candidateKey="";
        foreach(var go in predictionVisuals)if(go!=null)go.SetActive(showPrediction);
    }
    void RefreshCandidate3D()
    {
        if(result||held<0||!hasCell){if(candidateVisuals.Count>0)ClearCandidate3D();return;}
        var cells=Candidate();string key=held+":"+showPrediction+":"+string.Join(";",Array.ConvertAll(cells,c=>c.ToString()));if(key==candidateKey)return;
        ClearCandidate3D();candidateKey=key;bool valid=EveningBoard.ValidCells(cells);var heights=board.Heights();var extra=new HashSet<Vector3Int>();
        foreach(var voxel in board.Voxels())extra.Add(new Vector3Int(voxel.X,voxel.Layer,4-voxel.Z));
        foreach(var c in cells)if(EveningBoard.Inside(c))
        {
            extra.Add(new Vector3Int(c.x,heights[c.x,c.y],4-c.y));
            candidateVisuals.Add(AddMesh(edges,candidateMaterial,new Vector3(c.x,.055f,4-c.y),new Vector3(.94f,.04f,.94f),valid?new Color(.34f,.22f,.53f,.95f):new Color(.85f,.15f,.12f,.9f)));
        }
        foreach(var go in predictionVisuals)if(go!=null)go.SetActive(false);
        if(showPrediction)AddGhostVolume(extra,candidateMaterial,valid?PredictionTint:new Color(.9f,.2f,.18f,.30f),valid?new Color(.56f,.36f,.79f,.65f):new Color(.85f,.15f,.12f,.8f),1,candidateVisuals);
    }
    void TogglePrediction()
    {showPrediction=!showPrediction;foreach(var go in predictionVisuals)if(go!=null)go.SetActive(showPrediction);candidateKey="";RefreshCandidate3D();}
    void DisposeGhostResources()
    {
        foreach(var mesh in ghostMeshes.Values)if(mesh!=null)Destroy(mesh);ghostMeshes.Clear();
        if(targetMaterial!=null)Destroy(targetMaterial);if(predictionMaterial!=null)Destroy(predictionMaterial);if(candidateMaterial!=null)Destroy(candidateMaterial);
    }
}
