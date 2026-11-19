using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed partial class EveningPrototype:MonoBehaviour
{
    readonly EveningBoard board=new EveningBoard();
    readonly Rect viewport=new Rect(36,170,560,480);
    static readonly Rect placementBoard=new Rect(650,180,480,480);
    static readonly Color Ink=new Color(.13f,.14f,.15f),Paper=new Color(.975f,.973f,.96f);
    Camera resultCamera,backgroundCamera;Material solid,edgeMaterial;Mesh cube,edges;Transform world;
    Font font;Texture2D portrait;GUIStyle body,small,title,heading,button;
    readonly List<GameObject> visuals=new List<GameObject>();
    readonly List<KeyValuePair<Transform,int>> growing=new List<KeyValuePair<Transform,int>>();
    bool result,animating,diagnostic,dragging,hasCell;
    int held=-1,anchorIndex;
    Vector2Int[] orientation;
    Vector2Int hover;
    Vector2 dragStart,dragPrevious,scroll;
    float scale=1,yaw=30,elevation=40,distance=14,growthStart,growth;
    Vector2 offset;
    string message="위쪽 목록에서 블록을 골라 평면 보드에 배치해보세요.";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap(){if(FindFirstObjectByType<EveningPrototype>()==null)new GameObject("evening prototype").AddComponent<EveningPrototype>();}
    void Awake()
    {
        Application.runInBackground=true;
        foreach(var c in FindObjectsByType<Camera>(FindObjectsSortMode.None))c.enabled=false;
        backgroundCamera=new GameObject("Flat UI background").AddComponent<Camera>();
        backgroundCamera.orthographic=true;backgroundCamera.cullingMask=0;backgroundCamera.depth=-10;
        backgroundCamera.clearFlags=CameraClearFlags.SolidColor;backgroundCamera.backgroundColor=Paper;
        font=Resources.Load<Font>("Fonts/Malgun");portrait=Resources.Load<Texture2D>("Art/EvePortrait");
        if(font==null)font=Font.CreateDynamicFontFromOSFont("Malgun Gothic",17);
        var shader=Shader.Find("BlockXX/Ink");if(shader==null)throw new Exception("Missing cube shader.");
        solid=new Material(shader);edgeMaterial=new Material(shader);cube=CubeMesh();edges=EdgeMesh();InitializeGhosts();
        world=new GameObject("Result voxels").transform;
        resultCamera=new GameObject("evening result camera").AddComponent<Camera>();
        resultCamera.enabled=true;resultCamera.cullingMask=1<<30;resultCamera.fieldOfView=35;
        resultCamera.clearFlags=CameraClearFlags.SolidColor;resultCamera.backgroundColor=Paper;
        resultCamera.nearClipPlane=.05f;resultCamera.farClipPlane=3000;
        Resize();
        SwitchStage(0);
        var args=Environment.GetCommandLineArgs();
        for(int i=0;i+1<args.Length;i++)if(args[i]=="-evening-checks"){diagnostic=true;StartCoroutine(CheckRuntime(args[i+1]));}
    }
    void Resize()
    {
        scale=Mathf.Min(Screen.width/1440f,Screen.height/900f);offset=new Vector2((Screen.width-1440*scale)/2,(Screen.height-900*scale)/2);
        if(resultCamera!=null)resultCamera.rect=new Rect((offset.x+viewport.x*scale)/Screen.width,1-(offset.y+viewport.yMax*scale)/Screen.height,viewport.width*scale/Screen.width,viewport.height*scale/Screen.height);
    }
    Vector2 Mouse()=>new Vector2((Input.mousePosition.x-offset.x)/scale,(Screen.height-Input.mousePosition.y-offset.y)/scale);
    void Update()
    {
        Resize();TickGrowth();if(diagnostic)return;
        var mouse=Mouse();UpdateBoardInput(mouse);if(result)return;
        if(Input.GetKeyDown(KeyCode.Escape)||Input.GetMouseButtonDown(1))ReturnHeld();
        if(held>=0)
        {
            if(Input.GetKeyDown(KeyCode.Q))Turn(-1);
            if(Input.GetKeyDown(KeyCode.E)||Input.GetKeyDown(KeyCode.R))Turn(1);
            if(Input.GetKeyDown(KeyCode.F))orientation=EveningBoard.Mirror(orientation);
        }
        if(Input.GetKeyDown(KeyCode.Z)&&(Input.GetKey(KeyCode.LeftControl)||Input.GetKey(KeyCode.RightControl)))Undo();
        if(Input.GetMouseButtonDown(0))HandlePlacementClick(mouse);
        RefreshCandidate3D();
    }
    bool HandlePlacementClick(Vector2 mouse)
    {
        if(result||!TryFlatPlanningCell(mouse,out var cell))return false;
        hover=cell;hasCell=true;
        if(held>=0)return Commit(Candidate());
        var piece=board.TopAt(cell);if(piece==null)return false;Pick(piece,cell);return true;
    }
    void OnApplicationFocus(bool focus){if(!focus)dragging=false;}
    void Select(int action)
    {if(!CanSelect(action))return;held=action;orientation=(Vector2Int[])EveningBoard.Shapes[action].Clone();anchorIndex=0;message="블록 선택 · 클릭 배치 / Q·E 회전 / F 대칭";}
    void ReturnHeld(){held=-1;orientation=null;ClearCandidate3D();message="블록을 목록으로 돌려보냈어요.";}
    void Turn(int direction){if(held>=0&&!result)orientation=EveningBoard.Rotate(orientation,direction);}
    Vector2Int[] Candidate()
    {if(held<0||orientation==null)return null;var r=new Vector2Int[orientation.Length];for(int i=0;i<r.Length;i++)r[i]=orientation[i]-orientation[anchorIndex]+hover;return r;}
    bool Commit(Vector2Int[] cells)
    {
        if(result||held<0)return false;
        if(!board.TryPlace(held,cells,out _)){message="블록 전체가 5×5 보드 안에 들어와야 해요.";return false;}
        held=-1;orientation=null;InvalidateAttempt();message="배치 완료 · 연보라색 예상 모양이 갱신됐어요. 행동 시작으로 완성을 확인하세요.";return true;
    }
    void Pick(EveningBoard.Piece p,Vector2Int at)
    {
        if(result||p==null)return;
        board.Remove(p.Id);InvalidateAttempt();held=p.Action;orientation=(Vector2Int[])p.Cells.Clone();EveningBoard.Normalize(orientation);
        anchorIndex=Math.Max(0,Array.IndexOf(p.Cells,at));
        message="블록 회수 · 다른 칸에 놓거나 Esc로 반환하세요.";
    }
    void Undo(){if(result)return;ReturnHeld();board.Undo();InvalidateAttempt();message="마지막에 놓은 블록을 취소했어요.";}
    void ClearBoard(){if(result||animating)return;ReturnHeld();board.Clear();InvalidateAttempt();message="새로운 평면 배치를 만들어보세요.";}
    bool StartResult()
    {
        if(result||held>=0||board.Pieces.Count==0)return false;
        result=true;InvalidateAttempt();animating=true;growth=0;growthStart=Time.unscaledTime;
        BuildResult();resultCamera.enabled=true;TickGrowth();
        message="겹친 칸들이 높이로 변하고 있어요.";return true;
    }
    void BackToPlan()
    {
        result=false;animating=false;growth=0;dragging=false;RebuildPlanning();
        message="배치판으로 돌아왔어요. 왼쪽의 예상 모양을 보며 다시 수정하세요.";
    }
    void TickGrowth()
    {
        if(!result){FrameCamera();return;}
        if(animating)
        {
            float t=Mathf.Clamp01((Time.unscaledTime-growthStart)/1.35f);growth=t*t*(3-2*t);
            foreach(var entry in growing)
            {
                var tr=entry.Key;Vector3 position=tr.position;position.y=(entry.Value+.5f)*growth;tr.position=position;
                tr.localScale=new Vector3(tr.localScale.x,Mathf.Max(.025f,.94f*growth),tr.localScale.z);
            }
            if(t>=1){animating=false;message="겹친 개수 = 큐브의 높이 · 드래그나 WASD로 돌려보세요.";EvaluateStage();}
        }
        FrameCamera();
    }
    void FrameCamera()
    {
        if(resultCamera==null)return;
        resultCamera.orthographic=false;
        var rotation=Quaternion.Euler(elevation,yaw,0);
        var focus=new Vector3(2,ResultHeight*.42f,2);
        resultCamera.transform.SetPositionAndRotation(focus+rotation*Vector3.back*distance,rotation);
    }
    void BuildResult()
    {
        ClearVisuals();
        AddMesh(cube,solid,new Vector3(2,-.16f,2),new Vector3(5.2f,.22f,5.2f),new Color(.84f,.83f,.80f));
        for(int x=0;x<5;x++)for(int z=0;z<5;z++)
        {
            AddMesh(cube,solid,new Vector3(x,-.025f,z),new Vector3(.97f,.025f,.97f),new Color(.93f,.92f,.89f));
            AddMesh(edges,edgeMaterial,new Vector3(x,-.01f,z),new Vector3(.97f,.025f,.97f),new Color(.70f,.70f,.67f));
        }
        foreach(var v in board.Voxels())
        {
            var c=AddMesh(cube,solid,new Vector3(v.X,0,4-v.Z),new Vector3(.94f,.025f,.94f),EveningBoard.Colors[v.Action]);
            var outline=Stage!=null&&v.Layer>=Stage.Target[v.X,v.Z]?new Color(.86f,.18f,.14f):v.Action==4?new Color(.6f,.6f,.62f):Ink;
            var e=AddMesh(edges,edgeMaterial,new Vector3(v.X,0,4-v.Z),new Vector3(.945f,.025f,.945f),outline);
            growing.Add(new KeyValuePair<Transform,int>(c.transform,v.Layer));growing.Add(new KeyValuePair<Transform,int>(e.transform,v.Layer));
        }
        AddTargetOutlines();
    }
    GameObject AddMesh(Mesh mesh,Material material,Vector3 at,Vector3 size,Color color)
    {
        var go=new GameObject("Cube module");go.layer=30;go.transform.SetParent(world);go.transform.position=at;go.transform.localScale=size;
        go.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;
        var props=new MaterialPropertyBlock();props.SetColor("_Color",color);renderer.SetPropertyBlock(props);visuals.Add(go);return go;
    }
    void ClearVisuals(){foreach(var go in visuals)if(go!=null){ReleaseGhostMesh(go);go.SetActive(false);Destroy(go);}visuals.Clear();growing.Clear();targetVisuals.Clear();predictionVisuals.Clear();candidateVisuals.Clear();candidateKey="";predictionVoxelCount=0;targetVoxelCount=0;}
    static Mesh CubeMesh()
    {
        var vertices=new List<Vector3>();var colors=new List<Color>();var triangles=new List<int>();
        var corners=new[]{new Vector3(-.5f,-.5f,-.5f),new Vector3(.5f,-.5f,-.5f),new Vector3(.5f,.5f,-.5f),new Vector3(-.5f,.5f,-.5f),new Vector3(-.5f,-.5f,.5f),new Vector3(.5f,-.5f,.5f),new Vector3(.5f,.5f,.5f),new Vector3(-.5f,.5f,.5f)};
        int[][] faces={new[]{0,3,2,1},new[]{5,6,7,4},new[]{4,7,3,0},new[]{1,2,6,5},new[]{3,7,6,2},new[]{4,0,1,5}};
        float[] shades={.84f,.74f,.79f,.66f,1,.58f};
        for(int f=0;f<6;f++){int n=vertices.Count;foreach(int c in faces[f]){vertices.Add(corners[c]);colors.Add(new Color(shades[f],shades[f],shades[f]));}triangles.AddRange(new[]{n,n+1,n+2,n,n+2,n+3});}
        var mesh=new Mesh();mesh.SetVertices(vertices);mesh.SetColors(colors);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
    }
    static Mesh EdgeMesh()
    {
        var v=new List<Vector3>();var indices=new List<int>();
        for(int axis=0;axis<3;axis++)for(int a=-1;a<=1;a+=2)for(int b=-1;b<=1;b+=2)
        {var p=axis==0?new Vector3(-.5f,a*.5f,b*.5f):axis==1?new Vector3(a*.5f,-.5f,b*.5f):new Vector3(a*.5f,b*.5f,-.5f);var q=p;q[axis]=.5f;indices.Add(v.Count);v.Add(p);indices.Add(v.Count);v.Add(q);}
        var mesh=new Mesh();mesh.SetVertices(v);mesh.SetIndices(indices.ToArray(),MeshTopology.Lines,0);return mesh;
    }
    void Styles()
    {
        if(body!=null)return;
        body=new GUIStyle(GUI.skin.label){font=font,fontSize=17,alignment=TextAnchor.MiddleLeft,normal={textColor=Ink},wordWrap=true};
        small=new GUIStyle(body){fontSize=13};heading=new GUIStyle(body){fontSize=20,fontStyle=FontStyle.Bold};title=new GUIStyle(heading){fontSize=40};
        button=new GUIStyle(body){alignment=TextAnchor.MiddleCenter};
    }
    void Fill(Rect r,Color color){var before=GUI.color;GUI.color=color;GUI.DrawTexture(r,Texture2D.whiteTexture);GUI.color=before;}
    void Border(Rect r,Color color,float width=1){Fill(new Rect(r.x,r.y,r.width,width),color);Fill(new Rect(r.x,r.yMax-width,r.width,width),color);Fill(new Rect(r.x,r.y,width,r.height),color);Fill(new Rect(r.xMax-width,r.y,width,r.height),color);}
    void Label(Rect r,string text,GUIStyle style=null){GUI.Label(r,text,style??body);}
    bool Button(Rect r,string text,bool enabled=true,bool dark=false)
    {
        Fill(r,dark?Ink:Color.white);Border(r,enabled?Ink:new Color(.8f,.8f,.78f));
        var before=GUI.enabled;GUI.enabled=before&&enabled;
        button.normal.textColor=dark?Color.white:enabled?Ink:Color.gray;bool clicked=GUI.Button(r,text,button);GUI.enabled=before;return clicked;
    }
    void Icon(Rect r,int action)
    {
        var shape=EveningBoard.Shapes[action];int w=0,h=0;foreach(var c in shape){w=Math.Max(w,c.x+1);h=Math.Max(h,c.y+1);}
        float size=Mathf.Min(26,Mathf.Min(r.width/w,r.height/h));var origin=r.center-new Vector2(w*size,h*size)/2;
        foreach(var c in shape){var cell=new Rect(origin.x+c.x*size,origin.y+c.y*size,size-2,size-2);Fill(cell,EveningBoard.Colors[action]);Border(cell,Ink);}
    }
    void OnGUI()
    {
        Resize();Styles();var previous=GUI.matrix;GUI.matrix=Matrix4x4.TRS(offset,Quaternion.identity,Vector3.one*scale);
        DrawSplitInterface();GUI.matrix=previous;
    }
    void OnDestroy()
    {
        ClearVisuals();DisposeGhostResources();if(world!=null)Destroy(world.gameObject);if(resultCamera!=null)Destroy(resultCamera.gameObject);if(backgroundCamera!=null)Destroy(backgroundCamera.gameObject);
        if(solid!=null)Destroy(solid);if(edgeMaterial!=null)Destroy(edgeMaterial);if(cube!=null)Destroy(cube);if(edges!=null)Destroy(edges);
    }
}
