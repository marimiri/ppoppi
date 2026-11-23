using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public sealed class ProtocolAer:MonoBehaviour
{
    enum ScreenMode {Title,Boot,Map,Puzzle,End}
    ScreenMode screen=ScreenMode.Title;
    readonly EveningBoard board=new EveningBoard();
    EveningBoard placementPreview;
    int previewAction=-1;
    Vector2Int[] previewCells;
    readonly bool[] cleared=new bool[AerStages.All.Length];
    static readonly Color Ink=Hex("C6EBEF"),Blue=Hex("66CAE8"),Sky=Hex("489FD0"),Pink=Hex("F2A6C6"),Muted=Hex("7BA8C5"),Line=Hex("22618C"),Paper=Hex("072A50"),Panel=Hex("0B365F"),Deep=Hex("041C38");
    static readonly Rect Preview=new Rect(38,219,516,414),Grid=new Rect(600,228,440,440);
    static readonly Color FrontEdge=Hex("8FD9FF");
    const float PreviewAlpha=.80f;
    Texture2D sky,triangle;Font korean,chalk,mono;
    GUIStyle pixelTitleLabel;
    Camera backgroundCamera,previewCamera,maskCamera;RenderTexture previewTexture,maskTexture;
    Material fillMat,lineMat,ghostMat,targetDepthMat,targetLineMat,frontEdgeMat;Mesh cube,edge,targetEdges,frontEdge;Transform sceneRoot;
    readonly List<GameObject> modules=new List<GameObject>();
    readonly List<Vector3> destinations=new List<Vector3>();
    GUIStyle label;float uiScale=1;Vector2 margin;
    int stageIndex,held=-1,anchorIndex,viewMode=2,hint=-1;
    int hintPieceId=-1;
    Vector2Int hintAnchor;
    int newMino=-1;
    readonly HashSet<int> knownMinos=new HashSet<int>{4};
    Vector2Int[] orientation;Vector2Int hover;
    bool onGrid,confirmTutorial,success,executing,evaluated,diagnostic,dragging,muted,restored;
    float screenSince,executionSince,restoredSince,yaw=32,elevation=32,zoom=11.7f;
    Vector2 dragLast;string message="블록을 선택하고 평면 코드에 배치하세요.";
    AudioSource audioSource;AudioClip clickSound,completeSound;
    AerStage Stage=>AerStages.All[stageIndex];
    static Color Hex(string s){ColorUtility.TryParseHtmlString("#"+s,out var c);return c;}

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap(){if(FindFirstObjectByType<ProtocolAer>()==null)new GameObject("Protocol Aer").AddComponent<ProtocolAer>();}
    void Awake()
    {
        Application.runInBackground=true;Application.targetFrameRate=60;
        foreach(var cam in FindObjectsByType<Camera>(FindObjectsSortMode.None))cam.enabled=false;
        sky=Resources.Load<Texture2D>("Art/Sky");
        korean=Resources.Load<Font>("Fonts/Pixel");chalk=Resources.Load<Font>("Fonts/ChalkHand");mono=korean;
        if(korean==null)korean=Font.CreateDynamicFontFromOSFont("Malgun Gothic",18);
        if(chalk==null)chalk=korean;if(mono==null)mono=korean;
        triangle=new Texture2D(12,16,TextureFormat.RGBA32,false);triangle.filterMode=FilterMode.Point;
        var pixels=new Color[12*16];for(int y=0;y<16;y++)for(int x=0;x<12;x++)pixels[y*12+x]=x<11-Math.Abs(y-7)?Color.white:Color.clear;
        triangle.SetPixels(pixels);triangle.Apply();
        backgroundCamera=new GameObject("Background").AddComponent<Camera>();backgroundCamera.cullingMask=0;backgroundCamera.depth=-10;backgroundCamera.backgroundColor=Paper;backgroundCamera.clearFlags=CameraClearFlags.SolidColor;
        previewTexture=new RenderTexture(258,207,24){antiAliasing=1,filterMode=FilterMode.Point};previewTexture.Create();
        previewCamera=new GameObject("Code preview").AddComponent<Camera>();previewCamera.targetTexture=previewTexture;previewCamera.cullingMask=1<<30;
        previewCamera.clearFlags=CameraClearFlags.SolidColor;previewCamera.backgroundColor=Deep;previewCamera.orthographic=false;previewCamera.fieldOfView=30;previewCamera.nearClipPlane=.1f;previewCamera.farClipPlane=100;
        maskTexture=new RenderTexture(258,207,24){antiAliasing=1,filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp};maskTexture.Create();
        maskCamera=new GameObject("Target surface edges").AddComponent<Camera>();maskCamera.enabled=false;
        fillMat=new Material(Shader.Find("BlockXX/Ink"));lineMat=new Material(fillMat);ghostMat=new Material(fillMat);
        // Sort transparent previews after the opaque base. Only the visible front
        // surface of each cube contributes, avoiding double-sided alpha buildup.
        ghostMat.renderQueue=3000;ghostMat.SetInt("_ZWrite",0);ghostMat.SetInt("_Cull",2);
        lineMat.renderQueue=3100;lineMat.SetInt("_ZWrite",0);
        targetDepthMat=new Material(Shader.Find("ProtocolAer/TargetDepth"));
        targetLineMat=new Material(lineMat);targetLineMat.SetFloat("_ViewBias",.055f);
        frontEdgeMat=new Material(lineMat);frontEdgeMat.renderQueue=3200;frontEdgeMat.SetInt("_ZTest",(int)UnityEngine.Rendering.CompareFunction.Always);
        cube=CubeMesh();edge=EdgeMesh();frontEdge=FrontEdgeMesh();sceneRoot=new GameObject("Code modules").transform;
        audioSource=gameObject.AddComponent<AudioSource>();clickSound=Tone(620,.045f);completeSound=Tone(1040,.22f);
        Resize();Rebuild();
        var args=Environment.GetCommandLineArgs();for(int i=0;i+1<args.Length;i++)if(args[i]=="-aer-capture"){diagnostic=true;StartCoroutine(Capture(args[i+1]));}
        for(int i=0;i+1<args.Length;i++)if(args[i]=="-aer-outline-capture"){diagnostic=true;StartCoroutine(CaptureOutline(args[i+1]));}
    }
    AudioClip Tone(float frequency,float duration)
    {
        int n=Mathf.RoundToInt(22050*duration);var data=new float[n];
        for(int i=0;i<n;i++){float t=i/22050f;data[i]=Mathf.Sin(2*Mathf.PI*frequency*t)*.07f*Mathf.Sin(Mathf.PI*i/n);}
        var clip=AudioClip.Create("UI signal",n,1,22050,false);clip.SetData(data,0);return clip;
    }
    void Sound(bool done=false){if(!muted)audioSource.PlayOneShot(done?completeSound:clickSound);}
    void Resize(){uiScale=Mathf.Min(Screen.width/1440f,Screen.height/900f);margin=new Vector2((Screen.width-1440*uiScale)/2,(Screen.height-900*uiScale)/2);}
    Vector2 Pointer()=>new Vector2((Input.mousePosition.x-margin.x)/uiScale,(Screen.height-Input.mousePosition.y-margin.y)/uiScale);
    void SetScreen(ScreenMode next){screen=next;screenSince=Time.unscaledTime;held=-1;onGrid=false;dragging=false;}
    void StartGame(){Array.Clear(cleared,0,cleared.Length);knownMinos.Clear();knownMinos.Add(4);newMino=-1;Sound();SetScreen(ScreenMode.Boot);}
    void Update()
    {
        Resize();previewCamera.enabled=screen==ScreenMode.Puzzle;
        if(diagnostic){UpdateGrowth();return;}
        if(screen==ScreenMode.Title){if(Input.anyKeyDown)StartGame();return;}
        if(screen==ScreenMode.Boot){if(Time.unscaledTime-screenSince>2.8f){SetScreen(ScreenMode.Map);confirmTutorial=!cleared[0];}return;}
        if(screen!=ScreenMode.Puzzle)return;
        UpdateGrowth();FrameCamera();
        if(success||executing||newMino>=0)return;
        Vector2 p=Pointer();
        if(Preview.Contains(p))
        {
            if(Input.GetMouseButtonDown(0)){dragging=true;dragLast=p;}
            float wheel=Input.mouseScrollDelta.y;if(wheel!=0){zoom=Mathf.Clamp(zoom-wheel*.4f,7,18);FrameCamera();}
        }
        if(!Input.GetMouseButton(0))dragging=false;
        if(dragging){Vector2 delta=p-dragLast;Orbit(delta.x*.35f,delta.y*.25f);dragLast=p;}
        float dt=Time.unscaledDeltaTime*60;
        float horizontal=(Input.GetKey(KeyCode.D)?dt:0)-(Input.GetKey(KeyCode.A)?dt:0);
        float vertical=(Input.GetKey(KeyCode.W)?dt:0)-(Input.GetKey(KeyCode.S)?dt:0);
        if(horizontal!=0||vertical!=0)Orbit(horizontal,vertical);
        onGrid=Grid.Contains(p);if(onGrid)hover=new Vector2Int((int)((p.x-Grid.x)/88),(int)((p.y-Grid.y)/88));
        if(Input.GetKeyDown(KeyCode.Escape)||Input.GetMouseButtonDown(1))ReturnHeld();
        if(held>=0)
        {
            if(Input.GetKeyDown(KeyCode.Q))Rotate(-1);if(Input.GetKeyDown(KeyCode.E)||Input.GetKeyDown(KeyCode.R))Rotate(1);
            if(Input.GetKeyDown(KeyCode.F))Mirror();
        }
        if(Input.GetKeyDown(KeyCode.Z)&&(Input.GetKey(KeyCode.LeftControl)||Input.GetKey(KeyCode.RightControl)))Undo();
        if(Input.GetMouseButtonDown(0))HandlePuzzleClick(p);
        RefreshPlacementPreview();
    }
    static bool PuzzleControlContains(Vector2 p)
    {
        if(Preview.Contains(p))return true;
        foreach(var r in new[]{new Rect(1291,30,110,36),new Rect(1240,140,160,38),new Rect(38,646,516,37),new Rect(38,738,142,32),new Rect(600,682,138,36),new Rect(750,682,138,36),new Rect(1080,620,320,57),new Rect(1080,691,153,38),new Rect(1247,691,153,38),new Rect(1080,741,320,36)})if(r.Contains(p))return true;
        return false;
    }
    void HandlePuzzleClick(Vector2 point)
    {
        if(screen!=ScreenMode.Puzzle||executing||success||newMino>=0)return;
        onGrid=Grid.Contains(point);
        if(onGrid)
        {
            hover=new Vector2Int((int)((point.x-Grid.x)/88),(int)((point.y-Grid.y)/88));
            if(held>=0)Place();
            else PickUpAt(hover);
        }
        else if(!TrySelectBlockAt(point)&&held>=0&&!PuzzleControlContains(point)){ReturnHeld();Sound();message="";}
    }
    void PickUpAt(Vector2Int cell)
    {
        var piece=board.TopAt(cell);if(piece==null)return;
        orientation=(Vector2Int[])piece.Cells.Clone();anchorIndex=Math.Max(0,Array.IndexOf(piece.Cells,cell));EveningBoard.Normalize(orientation);
        ClearHint();board.Remove(piece.Id);held=piece.Action;evaluated=restored=false;if(viewMode==0)viewMode=2;Rebuild();message="";
    }
    void EnterStage(int index)
    {
        stageIndex=index;board.Clear();held=-1;ClearHint();viewMode=2;success=executing=evaluated=restored=false;
        yaw=32;elevation=32;zoom=11.7f;message=Lesson();SetScreen(ScreenMode.Puzzle);Rebuild();
        newMino=-1;foreach(var step in Stage.Solution)if(!knownMinos.Contains(step.Action)){newMino=step.Action;break;}
    }
    string Lesson()=>stageIndex<3?new[]{"블록을 클릭한 뒤 배치판을 클릭하세요.","같은 칸에 겹치면 높아집니다.","겹침으로 목표 모양을 만드세요."}[stageIndex]:Stage.Weather?"복원도 "+Stage.ClearPercent+"% 이상":"";
    static Rect TrayBlock(int index)=>new Rect(1119+(index%2)*139,249+(index/2)*112,84,84);
    int RemainingStock(int action)=>Stage.LimitedSupply?Math.Max(0,Stage.Remaining(board,action)-(held==action?1:0)):-1;
    static Rect TrayHit(int index){var r=TrayBlock(index);return new Rect(r.x-2,r.y-2,r.width+14,r.height+14);}
    List<int> TrayActions()
    {
        var result=new List<int>();foreach(int action in new[]{4,3,2,1,0})if(Stage.Allows(action))result.Add(action);return result;
    }
    bool TrySelectBlockAt(Vector2 point)
    {
        if(screen!=ScreenMode.Puzzle||executing||success||newMino>=0)return false;
        var actions=TrayActions();for(int i=0;i<actions.Count;i++)if(TrayHit(i).Contains(point)){Select(actions[i]);return held>=0;}
        return false;
    }
    void Select(int action=4)
    {
        if(executing||success||newMino>=0||!Stage.CanPlace(board,action))return;
        ClearHint();held=action;orientation=(Vector2Int[])EveningBoard.Shapes[action].Clone();anchorIndex=0;
        if(viewMode==0)viewMode=2;Rebuild();Sound();message="";
    }
    Vector2Int[] Candidate(){var cells=new Vector2Int[orientation.Length];for(int i=0;i<cells.Length;i++)cells[i]=orientation[i]-orientation[anchorIndex]+hover;return cells;}
    bool Place()
    {
        if(held<0)return false;
        if(!Stage.CanPlace(board,held)||!board.TryPlace(held,Candidate(),out _)){ReturnHeld();Sound();message="";return false;}
        held=-1;ClearHint();evaluated=restored=false;Sound();Rebuild();message="";return true;
    }
    void ClearHint(){hint=-1;hintPieceId=-1;}
    void ReturnHeld(){held=-1;orientation=null;ClearHint();if(placementPreview!=null)Rebuild();}
    void Rotate(int dir){if(held<0)return;orientation=EveningBoard.Rotate(orientation,dir);RefreshPlacementPreview();Sound();message="";}
    void Mirror(){if(held<0)return;orientation=EveningBoard.Mirror(orientation);RefreshPlacementPreview();Sound();message="";}
    void Undo(){if(executing||success)return;ReturnHeld();board.Undo();evaluated=restored=false;Rebuild();Sound();message="";}
    void ResetBoard(){if(executing||success)return;board.Clear();ReturnHeld();hint=-1;evaluated=restored=false;Rebuild();message=Lesson();Sound();}
    void RequestHint()
    {
        if(executing||success||newMino>=0)return;
        onGrid=false;ReturnHeld();var advice=AerHints.Next(Stage,board);
        if(advice.Type==AerHints.Kind.Execute){message="복원 조건을 만족했습니다. 실행하세요.";return;}
        hintAnchor=advice.Anchor;
        if(advice.Type==AerHints.Kind.Move)
        {
            hintPieceId=advice.PieceId;
            message="예시 해법 기준: 빨간 점의 블록을 먼저 옮기거나 반환하세요.";return;
        }
        var step=Stage.Solution[advice.Step];Select(step.Action);
        if(step.Mirrored)orientation=EveningBoard.Mirror(orientation);
        for(int i=0;i<step.Turns;i++)orientation=EveningBoard.Rotate(orientation,1);
        hint=advice.Step;hintAnchor=advice.Anchor;RefreshPlacementPreview();
        bool overlap=false;foreach(var piece in board.Pieces)if(AerHints.SameCells(piece.Cells,step.Cells())){overlap=true;break;}
        message="예시 해법 "+(hint+1)+"/"+Stage.Supply+" · "+EveningBoard.Names[step.Action]+" 미노 · 금색 점에 "+(overlap?"겹쳐 배치하세요.":"배치하세요.");
    }
    void Execute()
    {
        if(executing||held>=0||board.Pieces.Count==0)return;
        if(!Stage.RequiredMinoUsed(board)){message=EveningBoard.Names[Stage.RequiredAction]+" 미노를 사용해보세요.";return;}
        executing=true;evaluated=restored=false;executionSince=Time.unscaledTime;viewMode=2;Rebuild();Sound();
        message="명령을 실행하고 입체 결과를 대조하는 중…";
    }
    void UpdateGrowth()
    {
        if(!executing)return;
        float t=Mathf.Clamp01((Time.unscaledTime-executionSince)/.85f),g=t*t*(3-2*t);
        for(int i=0;i<modules.Count;i++)
        {
            var go=modules[i];if(go==null||go.name!="actual")continue;
            Vector3 at=destinations[i];go.transform.position=new Vector3(at.x,at.y*g,at.z);go.transform.localScale=new Vector3(.91f,Mathf.Max(.02f,.91f*g),.91f);
        }
        if(t<1)return;executing=false;evaluated=true;
        if(Stage.Complete(board))
        {
            success=restored=true;restoredSince=Time.unscaledTime;cleared[stageIndex]=true;
            Rebuild();Sound(true);message="";
        }
        else {message="목표와 다릅니다. 다시 배치하세요.";Rebuild();}
    }
    void Orbit(float horizontal,float vertical)
    {
        // Wrap instead of clamping: both axes can cross the poles and complete
        // any number of revolutions without accumulating large float values.
        yaw=Mathf.Repeat(yaw+horizontal,360);
        elevation=Mathf.Repeat(elevation+vertical+180,360)-180;
        FrameCamera();
    }
    void FrameCamera()
    {
        // Perspective has the usual near-large/far-small relationship. Keep the
        // old framing at the focus plane while zooming by camera distance.
        previewCamera.orthographic=false;previewCamera.fieldOfView=30;
        float distance=zoom*.31f/Mathf.Tan(previewCamera.fieldOfView*.5f*Mathf.Deg2Rad);
        int targetHeight=0;foreach(int height in Stage.Target)targetHeight=Math.Max(targetHeight,height);
        var rot=Quaternion.Euler(elevation,yaw,0);var focus=new Vector3(2,Mathf.Max(.65f,targetHeight*.32f),2);
        previewCamera.transform.SetPositionAndRotation(focus+rot*Vector3.back*distance,rot);
    }
    void LateUpdate()
    {
        if(screen!=ScreenMode.Puzzle||viewMode==1||restored)return;
        maskCamera.CopyFrom(previewCamera);maskCamera.enabled=false;maskCamera.targetTexture=maskTexture;
        maskCamera.cullingMask=1<<29;maskCamera.clearFlags=CameraClearFlags.SolidColor;maskCamera.backgroundColor=Color.clear;
        maskCamera.transform.SetPositionAndRotation(previewCamera.transform.position,previewCamera.transform.rotation);
        var active=RenderTexture.active;maskCamera.Render();RenderTexture.active=active;
    }
    GameObject Module(Mesh mesh,Vector3 at,Vector3 size,Color color,string name="module")
    {
        var go=new GameObject(name);go.layer=30;go.transform.SetParent(sceneRoot,false);go.transform.position=at;go.transform.localScale=size;
        go.AddComponent<MeshFilter>().sharedMesh=mesh;var r=go.AddComponent<MeshRenderer>();r.sharedMaterial=name=="front-edge"?frontEdgeMat:name=="target-mask"?targetDepthMat:name=="target-edges"?targetLineMat:mesh==edge?lineMat:name=="actual"&&!restored?ghostMat:fillMat;
        var props=new MaterialPropertyBlock();props.SetColor("_Color",color);r.SetPropertyBlock(props);modules.Add(go);destinations.Add(at);return go;
    }
    Vector2Int[] ValidPreviewCells()
    {
        if(screen!=ScreenMode.Puzzle||held<0||!onGrid||executing||success||newMino>=0||!Stage.CanPlace(board,held))return null;
        var cells=Candidate();return EveningBoard.ValidCells(cells)?cells:null;
    }
    void RefreshPlacementPreview()
    {
        var cells=ValidPreviewCells();int action=cells==null?-1:held;
        bool same=action==previewAction&&(cells==null)==(previewCells==null);
        if(same&&cells!=null)for(int i=0;i<cells.Length;i++)if(cells[i]!=previewCells[i]){same=false;break;}
        if(!same)Rebuild();
    }
    void BuildPlacementPreview()
    {
        previewCells=ValidPreviewCells();previewAction=previewCells==null?-1:held;placementPreview=null;
        if(previewCells==null)return;
        // Evaluate a temporary plan so overlap and local S/T leveling
        // can be inspected without changing placements or stage completion.
        placementPreview=new EveningBoard();foreach(var piece in board.Pieces)placementPreview.TryPlace(piece.Action,piece.Cells,out _);
        placementPreview.TryPlace(held,previewCells,out _);
    }
    void Rebuild()
    {
        BuildPlacementPreview();
        foreach(var o in modules){o.SetActive(false);Destroy(o);}modules.Clear();destinations.Clear();
        if(targetEdges!=null){Destroy(targetEdges);targetEdges=null;}
        Module(cube,new Vector3(2,-.19f,2),new Vector3(5.15f,.24f,5.15f),Hex("123F68"));
        for(int y=0;y<5;y++)for(int x=0;x<5;x++)
        {
            Module(cube,new Vector3(x,-.045f,4-y),new Vector3(.97f,.06f,.97f),Hex("0D2E50"));
            Module(edge,new Vector3(x,-.01f,4-y),new Vector3(.98f,.02f,.98f),Hex("28648B"));
        }
        // Board row y=4 is world z=0, so its outside edge is z=-0.575.
        // This marker remains tied to that edge while the camera orbits.
        Module(frontEdge,Vector3.zero,Vector3.one,FrontEdge,"front-edge");
        if(viewMode!=1&&!restored)for(int y=0;y<5;y++)for(int x=0;x<5;x++)for(int h=0;h<Stage.Target[x,y];h++)
        {
            // Depth only: hide rear-facing creases without covering the player's
            // translucent result in the main preview.
            var mask=Module(cube,new Vector3(x,h+.5f,4-y),Vector3.one,Color.white,"target-mask");mask.layer=29;
        }
        if(viewMode!=1&&!restored)
        {
            targetEdges=AerTargetOutline.Build(Stage.Target);
            var outline=Module(targetEdges,Vector3.zero,Vector3.one,Color.white,"target-edges");outline.layer=29;
        }
        if(viewMode!=0)foreach(var v in (placementPreview??board).Voxels())
        {
            bool extra=evaluated&&placementPreview==null&&v.Layer>=Stage.Target[v.X,v.Z];Color c=EveningBoard.Colors[v.Action];
            c.a=restored?1:PreviewAlpha;
            Module(cube,new Vector3(v.X,v.Layer+.5f,4-v.Z),Vector3.one*.91f,c,"actual");
            if(extra)Module(edge,new Vector3(v.X,v.Layer+.5f,4-v.Z),Vector3.one*.94f,Pink);
        }
        FrameCamera();
    }
    static Mesh CubeMesh()
    {
        var vertices=new List<Vector3>();var colors=new List<Color>();var triangles=new List<int>();
        var corners=new[]{new Vector3(-.5f,-.5f,-.5f),new Vector3(.5f,-.5f,-.5f),new Vector3(.5f,.5f,-.5f),new Vector3(-.5f,.5f,-.5f),new Vector3(-.5f,-.5f,.5f),new Vector3(.5f,-.5f,.5f),new Vector3(.5f,.5f,.5f),new Vector3(-.5f,.5f,.5f)};
        int[][] faces={new[]{0,3,2,1},new[]{5,6,7,4},new[]{4,7,3,0},new[]{1,2,6,5},new[]{3,7,6,2},new[]{4,0,1,5}};float[] shades={.87f,.80f,.91f,.72f,1,.66f};
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
    static Mesh FrontEdgeMesh()
    {
        var mesh=new Mesh{name="2D bottom / 3D front edge"};
        mesh.vertices=new[]{new Vector3(-.575f,.015f,-.575f),new Vector3(4.575f,.015f,-.575f)};
        mesh.SetIndices(new[]{0,1},MeshTopology.Lines,0);mesh.RecalculateBounds();return mesh;
    }

    static float Snap(float n)=>Mathf.Round(n/2)*2;
    void Fill(Rect r,Color c){var old=GUI.color;GUI.color=c;r=new Rect(Snap(r.x),Snap(r.y),Mathf.Max(2,Snap(r.width)),Mathf.Max(2,Snap(r.height)));GUI.DrawTexture(r,Texture2D.whiteTexture);GUI.color=old;}
    void Border(Rect r,Color c,float width=2)
    {
        width=Mathf.Max(2,Snap(width));float cut=4;
        Fill(new Rect(r.x+cut,r.y,r.width-cut*2,width),c);Fill(new Rect(r.x+cut,r.yMax-width,r.width-cut*2,width),c);
        Fill(new Rect(r.x,r.y+cut,width,r.height-cut*2),c);Fill(new Rect(r.xMax-width,r.y+cut,width,r.height-cut*2),c);
        foreach(var p in new[]{new Vector2(r.x+2,r.y+2),new Vector2(r.xMax-4,r.y+2),new Vector2(r.x+2,r.yMax-4),new Vector2(r.xMax-4,r.yMax-4)})Fill(new Rect(p.x,p.y,2,2),c);
    }
    void Text(Rect r,string value,int size=18,Color? color=null,bool bold=false,TextAnchor align=TextAnchor.MiddleLeft,Font type=null)
    {
        if(label==null)label=new GUIStyle(GUI.skin.label);
        bool pixel=type==null||type==korean||type==mono;
        if(pixel)size=size<=22?16:size<=40?32:48;
        label.font=type??korean;label.fontSize=size;label.fontStyle=FontStyle.Normal;label.normal.textColor=color??Ink;label.alignment=align;label.wordWrap=true;label.padding=new RectOffset(0,0,0,0);
        label.font.RequestCharactersInTexture(value,size,FontStyle.Normal);
        if(pixel&&label.font.material.mainTexture!=null)label.font.material.mainTexture.filterMode=FilterMode.Point;
        r.x=Snap(r.x);r.y=Snap(r.y);GUI.Label(r,value,label);
    }
    bool Button(Rect r,string value,bool enabled=true,bool primary=false)
    {
        bool over=r.Contains(Pointer());Fill(new Rect(r.x+4,r.y+4,r.width,r.height),Deep);
        Fill(r,!enabled?Hex("10304D"):primary?(over?Hex("378CB8"):Hex("246D9B")):(over?Hex("164D77"):Panel));
        Border(r,enabled?(over?Ink:primary?Sky:Line):Hex("1A4567"));
        if(enabled){Fill(new Rect(r.x+6,r.y+2,r.width-12,2),primary?Sky:Hex("245B82"));if(primary)Fill(new Rect(r.x+8,r.y+8,4,4),Pink);}
        Text(r,value,17,!enabled?Hex("4C7696"):Ink,true,TextAnchor.MiddleCenter);
        bool old=GUI.enabled;GUI.enabled=old&&enabled;bool hit=GUI.Button(r,GUIContent.none,GUIStyle.none);GUI.enabled=old;if(hit)Sound();return hit;
    }
    void LineSegment(Vector2 a,Vector2 b,Color color,float width=1)
    {
        // Rasterize diagonal lines on a shared 4-pixel grid instead of rotating smooth quads.
        int x0=Mathf.RoundToInt(a.x/4),y0=Mathf.RoundToInt(a.y/4),x1=Mathf.RoundToInt(b.x/4),y1=Mathf.RoundToInt(b.y/4);
        int dx=Math.Abs(x1-x0),sx=x0<x1?1:-1,dy=-Math.Abs(y1-y0),sy=y0<y1?1:-1,err=dx+dy;
        while(true){Fill(new Rect(x0*4,y0*4,4,4),color);if(x0==x1&&y0==y1)break;int e=2*err;if(e>=dy){err+=dy;x0+=sx;}if(e<=dx){err+=dx;y0+=sy;}}
    }
    void Backdrop()
    {
        Fill(new Rect(0,0,1440,900),Paper);
        for(int x=0;x<1440;x+=48)Fill(new Rect(x,112,2,788),Hex("0B3158"));
        for(int y=112;y<900;y+=48)Fill(new Rect(0,y,1440,2),Hex("0B3158"));
        Fill(new Rect(0,0,1440,100),Deep);Fill(new Rect(0,0,1440,4),Sky);Fill(new Rect(1260,0,88,4),Pink);
        for(int i=0;i<12;i++)Fill(new Rect(40+i*8,78,4,4),i<7?Sky:Line);
    }
    void Header(string page,string subtitle)
    {
        Text(new Rect(38,28,460,32),"PROTOCOL / AER",25,Ink,true,type:mono);
        if(Button(new Rect(1291,30,110,36),muted?"소리 꺼짐":"소리 켜짐"))muted=!muted;
        Fill(new Rect(38,110,1364,1),Line);
    }
    void OnGUI()
    {
        Resize();var old=GUI.matrix;GUI.matrix=Matrix4x4.TRS(margin,Quaternion.identity,Vector3.one*uiScale);
        if(screen==ScreenMode.Title)DrawTitle();else if(screen==ScreenMode.Boot)DrawBoot();else {Backdrop();if(screen==ScreenMode.Map)DrawMap();else if(screen==ScreenMode.End)DrawEnd();else DrawPuzzle();}
        GUI.matrix=old;
    }
    void DrawTitle()
    {
        if(sky!=null)GUI.DrawTexture(new Rect(0,0,1440,900),sky,ScaleMode.ScaleAndCrop);
        Fill(new Rect(0,0,1440,900),new Color(.1f,.22f,.38f,.12f));
        DrawRetroTitle(390,true);
        Text(new Rect(520,641,400,65),"게임 시작",32,Color.white,true,TextAnchor.MiddleCenter);
        if(Mathf.Repeat(Time.unscaledTime,1.3f)<.7f)GUI.DrawTexture(new Rect(573,663,13,18),triangle);
    }
    static uint TitleNoiseHash(uint value)
    {value^=value>>16;value*=0x7feb352du;value^=value>>15;value*=0x846ca68bu;return value^(value>>16);}
    void DrawPixelGlyph(string glyph,float left,float top,float width,Color color)
    {
        var old=GUI.matrix;pixelTitleLabel.normal.textColor=color;
        GUI.matrix=old*Matrix4x4.TRS(new Vector3(Snap(left),Snap(top),0),Quaternion.identity,new Vector3(6,6,1));
        GUI.Label(new Rect(0,0,width+1,16),glyph,pixelTitleLabel);GUI.matrix=old;
    }
    void DrawRetroTitle(float centerY,bool animated)
    {
        const string title="PROTOCOL AER";const float scale=6;
        if(pixelTitleLabel==null)pixelTitleLabel=new GUIStyle(GUI.skin.label){font=korean,fontSize=16,fontStyle=FontStyle.Normal,alignment=TextAnchor.UpperLeft,wordWrap=false,padding=new RectOffset(0,0,0,0),margin=new RectOffset(0,0,0,0)};
        korean.RequestCharactersInTexture(title,16,FontStyle.Normal);
        if(korean.material.mainTexture!=null)korean.material.mainTexture.filterMode=FilterMode.Point;
        var widths=new float[title.Length];float total=0;
        for(int i=0;i<title.Length;i++){widths[i]=Mathf.Ceil(pixelTitleLabel.CalcSize(new GUIContent(title[i].ToString())).x);total+=widths[i]*scale;}
        float left=720-total/2,start=left,top=centerY-48;
        int tick=Mathf.FloorToInt(Time.unscaledTime*8);
        for(int i=0;i<title.Length;i++)
        {
            string glyph=title[i].ToString();
            float dy=animated?Mathf.Round(Mathf.Sin(Time.unscaledTime*1.7f+i*.7f)*.8f)*2:0;
            bool glitch=animated&&tick%29==0&&i>=3&&i<=6;
            DrawPixelGlyph(glyph,left+4,top+dy+4,widths[i],new Color(.04f,.13f,.23f,.32f));
            if(glitch)DrawPixelGlyph(glyph,left-2,top+dy,widths[i],new Color(.65f,.9f,1,.22f));
            DrawPixelGlyph(glyph,left+(glitch?2:0),top+dy,widths[i],Color.white);
            left+=widths[i]*scale;
        }
        if(!animated)return;
        // Sparse point-filtered grain, kept local to the title area. Hashing
        // avoids changing Unity's random state or any puzzle behavior.
        for(uint i=0;i<18;i++)
        {
            uint seed=TitleNoiseHash((uint)tick*97u+i*7919u);
            float x=start+(seed%((uint)total/2))*2,y=top+((seed>>12)%48)*2;
            Fill(new Rect(x,y,i%5==0?4:2,2),i%3==0?new Color(.05f,.17f,.29f,.20f):new Color(1,1,1,.13f));
        }
    }
    void DrawBoot()
    {
        Fill(new Rect(0,0,1440,900),Deep);Border(new Rect(160,130,1120,640),Line,2);
        float t=Time.unscaledTime-screenSince;
        Fill(new Rect(220,650,1000,12),Panel);for(int i=0;i<Mathf.FloorToInt(Mathf.Clamp01(t/2.6f)*50);i++)Fill(new Rect(220+i*20,650,16,12),Sky);
        Text(new Rect(220,680,1000,30),"기상 조작 시스템에 연결하는 중…",17,Color.white);
    }
    int ClearCount(){int n=0;for(int i=3;i<cleared.Length;i++)if(cleared[i])n++;return n;}
    int NextMain(){for(int i=3;i<cleared.Length;i++)if(!cleared[i])return i;return cleared.Length;}
    static readonly Vector2[] BuildingPositions={new Vector2(-.65f,-.34f),new Vector2(-.25f,-.70f),new Vector2(.24f,-.68f),new Vector2(.65f,-.25f),new Vector2(.63f,.38f),new Vector2(.25f,.68f),new Vector2(-.25f,.60f),new Vector2(-.65f,.28f)};
    Vector2 RadarPoint(Vector2 p)=>new Vector2(720+p.x*610,500+p.y*255);
    void RadarRing(Vector2 center,float rx,float ry,Color color,float width=1)
    {
        Vector2 previous=center+new Vector2(rx,0);for(int i=1;i<=96;i++){float a=i*Mathf.PI*2/96;Vector2 p=center+new Vector2(Mathf.Cos(a)*rx,Mathf.Sin(a)*ry);LineSegment(previous,p,color,width);previous=p;}
    }
    void LandOutline(params Vector2[] points)
    {
        for(int i=0;i<points.Length;i++)LineSegment(RadarPoint(points[i]),RadarPoint(points[(i+1)%points.Length]),Hex("3288B1"),2);
    }
    void Building(Vector2 p,int index,Color color)
    {
        Fill(new Rect(p.x-24,p.y-12,48,36),Deep);Fill(new Rect(p.x-20,p.y-16,40,32),Panel);Border(new Rect(p.x-20,p.y-16,40,32),color,2);
        LineSegment(p+new Vector2(-20,-16),p+new Vector2(0,-29),color,2);LineSegment(p+new Vector2(0,-29),p+new Vector2(20,-16),color,2);
        if(index%2==0){Border(new Rect(p.x+20,p.y-6,13,22),color,2);}else {Border(new Rect(p.x-8,p.y-37,16,8),color,2);}
        LineSegment(p+new Vector2(-11,0),p+new Vector2(11,0),color);LineSegment(p+new Vector2(0,-12),p+new Vector2(0,12),color);
    }
    void DrawMap()
    {
        bool previousEnabled=GUI.enabled;if(confirmTutorial)GUI.enabled=false;
        Header("RECOVERY MAP","WEATHER OBSERVATORY / CONNECTION RESTORED");
        Text(new Rect(52,135,950,55),"기상 시스템 복구",34,Ink,true);
        Text(new Rect(1125,149,235,40),ClearCount()+" / 10",30,Blue,true,TextAnchor.MiddleRight,mono);
        Fill(new Rect(54,213,1332,8),Deep);for(int i=0;i<ClearCount()*10;i++)Fill(new Rect(54+i*13.32f,213,10,8),Sky);
        Fill(new Rect(54,235,1332,535),Deep);Border(new Rect(54,235,1332,535),Line);
        for(int x=72;x<1380;x+=24)for(int y=252;y<760;y+=24)Fill(new Rect(x,y,2,2),Hex("123D61"));
        Vector2 center=new Vector2(720,500);
        for(int i=1;i<=4;i++)RadarRing(center,610*i/4f,255*i/4f,Hex("17486B"));
        LineSegment(new Vector2(110,500),new Vector2(1330,500),Line);LineSegment(new Vector2(720,245),new Vector2(720,755),Line);
        LandOutline(new Vector2(-.92f,-.1f),new Vector2(-.78f,-.55f),new Vector2(-.35f,-.93f),new Vector2(.15f,-.86f),new Vector2(.43f,-.46f),new Vector2(.10f,-.08f),new Vector2(-.33f,.14f),new Vector2(-.70f,.05f));
        LandOutline(new Vector2(-.82f,.22f),new Vector2(-.40f,.27f),new Vector2(.03f,.12f),new Vector2(.48f,-.05f),new Vector2(.91f,.14f),new Vector2(.79f,.62f),new Vector2(.34f,.95f),new Vector2(-.20f,.87f),new Vector2(-.72f,.65f));
        LandOutline(new Vector2(.44f,-.85f),new Vector2(.83f,-.67f),new Vector2(.96f,-.27f),new Vector2(.61f,-.03f),new Vector2(.43f,-.30f));
        float sweep=Time.unscaledTime*.35f;
        for(int j=0;j<15;j++){float a=sweep-j*.012f;LineSegment(center,center+new Vector2(Mathf.Cos(a)*610,Mathf.Sin(a)*255),new Color(.23f,.64f,.9f,(15-j)*.012f),2);}
        int active=NextMain();
        for(int i=0;i<8;i++)
        {
            int index=AerStages.BuildingStages[i];Vector2 p=RadarPoint(BuildingPositions[i]);bool current=active==index;
            Building(p,i,cleared[index]?Ink:current?Sky:Hex("32648B"));
            if(current)
            {
                RadarRing(p,42,42,Line);
                if(Mathf.Repeat(Time.unscaledTime,1.25f)<.7f){Fill(new Rect(p.x+30,p.y-38,9,9),Pink);Border(new Rect(p.x+27,p.y-41,15,15),Pink);}
                Fill(new Rect(p.x-48,p.y+31,96,33),Panel);Border(new Rect(p.x-48,p.y+31,96,33),Sky);
                Text(new Rect(p.x-48,p.y+31,96,33),"???",20,Blue,true,TextAnchor.MiddleCenter);
            }
            else if(cleared[index])Text(new Rect(p.x-104,p.y+29,208,32),AerStages.All[index].Name,15,Ink,true,TextAnchor.MiddleCenter);
            if(current&&GUI.Button(new Rect(p.x-52,p.y-48,104,115),GUIContent.none,GUIStyle.none))EnterStage(index);
        }
        if(active<cleared.Length&&AerStages.All[active].Weather)
        {
            if(Button(new Rect(1020,790,350,48),"날씨 복원",true,true))EnterStage(active);
        }
        if(active==cleared.Length&&Button(new Rect(1020,790,350,48),"완료",true,true))SetScreen(ScreenMode.End);
        if(Button(new Rect(54,858,160,30),"타이틀로"))SetScreen(ScreenMode.Title);
        if(Button(new Rect(228,858,180,30),"조작법 다시 보기"))confirmTutorial=true;
        if(Button(new Rect(1190,858,180,30),"처음부터")){Array.Clear(cleared,0,cleared.Length);knownMinos.Clear();knownMinos.Add(4);newMino=-1;confirmTutorial=true;}
        GUI.enabled=previousEnabled;if(confirmTutorial)DrawIntro();
    }
    void Modal(Rect r,string eyebrow,string title)
    {
        Fill(new Rect(0,0,1440,900),new Color(.01f,.04f,.12f,.78f));Fill(new Rect(r.x+8,r.y+8,r.width,r.height),Deep);Fill(r,Panel);Border(r,Sky,4);
        Fill(new Rect(r.x+4,r.y+4,r.width-8,24),Hex("175B88"));
        for(int i=0;i<3;i++)Fill(new Rect(r.xMax-20-i*12,r.y+12,6,6),i==0?Pink:Sky);
        Text(new Rect(r.x+34,r.y+45,r.width-68,52),title,30,Ink,true);
    }
    void DrawIntro()
    {
        var r=new Rect(335,205,770,490);Modal(r,"FIRST CONNECTION / 00","코드 조작법을 확인하겠습니까?");
        Text(new Rect(371,360,685,100),"블록 클릭 → 배치판 클릭 → 실행\n목표와 같은 입체 모양을 만드세요.",21,Ink);
        if(Button(new Rect(371,582,333,58),"조작법 확인하기",true,true)){confirmTutorial=false;EnterStage(0);}
        if(Button(new Rect(724,582,345,58),"먼저 지도 보기"))confirmTutorial=false;
    }
    void DrawPuzzle()
    {
        bool previousEnabled=GUI.enabled;if(success||newMino>=0)GUI.enabled=false;
        Header("TUTORIAL / "+Stage.Code,"RECONSTRUCT THE CODE FROM ITS SPATIAL TRACE");
        Text(new Rect(38,133,900,50),stageIndex<3?"튜토리얼 "+(stageIndex+1):Stage.Weather?Stage.Name:"명칭 찾아내기",31,Ink,true);
        if(Button(new Rect(1240,140,160,38),"복구 지도로",!executing)){success=false;SetScreen(ScreenMode.Map);}
        Text(new Rect(38,187,510,26),"입체",18,Ink,true);
        GUI.DrawTexture(Preview,previewTexture,ScaleMode.StretchToFill,false);
        if(viewMode!=1&&!restored)GUI.DrawTexture(Preview,maskTexture,ScaleMode.StretchToFill,true);
        Border(Preview,Line);
        for(int i=0;i<3;i++)if(Button(new Rect(38+i*174,646,164,37),new[]{"목표만","현재만","겹쳐 보기"}[i],!executing,viewMode==i)){viewMode=i;Rebuild();}
        if(Button(new Rect(38,738,142,32),"시점 초기화",!executing)){yaw=32;elevation=32;zoom=11.7f;FrameCamera();}
        Text(new Rect(600,188,440,28),"배치판",18,Ink,true);
        DrawGrid();
        if(Button(new Rect(600,682,138,36),"Q/E 회전",held>=0&&!executing))Rotate(1);
        if(Button(new Rect(750,682,138,36),"F 대칭",held>=0&&!executing))Mirror();
        DrawInventory();
        if(!string.IsNullOrEmpty(message))Text(new Rect(58,811,1322,49),message,17,Ink);
        if(held>=0&&!onGrid&&!success&&!executing)
        {
            var pointer=Pointer();foreach(var c in orientation){var cell=new Rect(pointer.x+12+c.x*22,pointer.y+12+c.y*22,20,20);Fill(cell,EveningBoard.Colors[held]);Border(cell,Deep);}
        }
        GUI.enabled=previousEnabled;if(success){if(Time.unscaledTime-restoredSince>=.65f)DrawSuccess();}else if(newMino>=0)DrawMinoNotice();
    }
    void DrawGrid()
    {
        int[,] heights=board.Heights(),raw=board.BaseHeights();
        for(int y=0;y<5;y++)for(int x=0;x<5;x++)
        {
            var r=new Rect(Grid.x+x*88,Grid.y+y*88,88,88);int n=heights[x,y];
            var top=board.TopAt(new Vector2Int(x,y));Color color=top==null?Panel:EveningBoard.Colors[top.Action];
            Fill(r,top==null?Panel:Color.Lerp(color,Deep,Mathf.Min(.36f,Mathf.Max(0,n-1)*.09f)));Border(r,Line);
            if(top!=null){Fill(new Rect(r.x+6,r.y+6,76,4),color);Fill(new Rect(r.x+6,r.y+10,4,68),color);Fill(new Rect(r.x+12,r.yMax-10,70,4),Color.Lerp(color,Deep,.5f));}
            if(n<raw[x,y])Fill(new Rect(r.x+12,r.y+14,16,4),Hex("FF6475"));
            if(n>raw[x,y]){Fill(new Rect(r.xMax-24,r.y+12,4,12),Hex("FEECB1"));Fill(new Rect(r.xMax-16,r.y+12,4,12),Hex("FEECB1"));}
            if(evaluated&&n!=Stage.Target[x,y])Border(new Rect(r.x+3,r.y+3,82,82),Pink,2);
        }
        if(held>=0&&onGrid&&!executing&&!success)
        {
            var cells=Candidate();bool valid=EveningBoard.ValidCells(cells);
            foreach(var c in cells)if(EveningBoard.Inside(c)){var r=new Rect(Grid.x+c.x*88+4,Grid.y+c.y*88+4,80,80);Color color=valid?EveningBoard.Colors[held]:Pink;Fill(r,new Color(color.r,color.g,color.b,.4f));Border(r,color,4);}
        }
        if(hint>=0)
        {
            var step=Stage.Solution[hint%Stage.Supply];foreach(var c in step.Cells())Border(new Rect(Grid.x+c.x*88+4,Grid.y+c.y*88+4,80,80),Hex("D8B46C"),3);
        }
        if(hintPieceId>=0)foreach(var piece in board.Pieces)if(piece.Id==hintPieceId)foreach(var c in piece.Cells)
        {
            var r=new Rect(Grid.x+c.x*88+4,Grid.y+c.y*88+4,80,80);Fill(r,new Color(1,.24f,.3f,.22f));Border(r,Hex("FF6475"),4);
        }
        if(hint>=0||hintPieceId>=0)
        {
            var dot=new Rect(Grid.x+hintAnchor.x*88+36,Grid.y+hintAnchor.y*88+36,16,16);Fill(dot,hintPieceId>=0?Hex("FF6475"):Hex("FFE1A0"));Border(dot,Deep,2);
        }
        Border(Grid,Sky,4);Fill(new Rect(Grid.x+4,Grid.yMax-6,Grid.width-8,6),FrontEdge);
    }
    void DrawInventory()
    {
        var actions=TrayActions();int types=actions.Count;
        Text(new Rect(1080,188,320,28),"블록",18,Ink,true);
        Fill(new Rect(1080,228,320,373),Deep);Border(new Rect(1080,228,320,373),Line);
        for(int i=0;i<types;i++)
        {
            var shape=TrayBlock(i);bool selected=held==actions[i];
            DrawMino(shape,actions[i],selected);
            if(Stage.LimitedSupply&&Stage.Remaining(board,actions[i])==0)Fill(shape,new Color(Deep.r,Deep.g,Deep.b,.75f));
            if(selected)Border(new Rect(shape.x-6,shape.y-6,96,96),Ink,4);
            if(TrayHit(i).Contains(Pointer())&&!executing&&!success&&Stage.CanPlace(board,actions[i]))Border(new Rect(shape.x-4,shape.y-4,92,92),Sky);
            if(Stage.LimitedSupply)DrawStockBadge(new Rect(shape.xMax-22,shape.yMax-20,32,32),RemainingStock(actions[i]));
        }
        Text(new Rect(1080,575,320,29),"복원도 "+Stage.Similarity(board)+"% / "+Stage.ClearPercent+"%",18,Blue,true);
        if(Button(new Rect(1080,620,320,57),executing?"실행 중…":"실행",!executing&&!success&&held<0&&board.Pieces.Count>0,true))Execute();
        if(Button(new Rect(1080,691,153,38),"되돌리기",board.Pieces.Count>0&&!executing&&!success))Undo();
        if(Button(new Rect(1247,691,153,38),"초기화",!executing&&!success))ResetBoard();
        if(Button(new Rect(1080,741,320,36),"힌트",!executing&&!success))RequestHint();
    }
    void PixelDisc(Rect r,Color color)
    {
        float radius=r.width/2;
        for(float row=0;row<r.height;row+=2)
        {
            float dy=row+1-radius,half=Mathf.Floor(Mathf.Sqrt(Mathf.Max(0,radius*radius-dy*dy))/2)*2;
            if(half>0)Fill(new Rect(r.center.x-half,r.y+row,half*2,2),color);
        }
    }
    void DrawStockBadge(Rect r,int count)
    {
        r.x=Snap(r.x);r.y=Snap(r.y);
        PixelDisc(r,count==0?Muted:Ink);
        PixelDisc(new Rect(r.x+2,r.y+2,r.width-4,r.height-4),Deep);
        Text(new Rect(r.x,r.y-1,r.width,r.height),count.ToString(),16,count==0?Muted:Color.white,true,TextAnchor.MiddleCenter);
    }
    void DrawMino(Rect area,int action,bool selected=false)
    {
        var cells=EveningBoard.Shapes[action];int w=0,h=0;foreach(var c in cells){w=Math.Max(w,c.x+1);h=Math.Max(h,c.y+1);}
        float unit=Snap(Mathf.Min(area.width/w,area.height/h));Vector2 start=area.center-new Vector2(w*unit,h*unit)/2;
        Color color=EveningBoard.Colors[action];
        foreach(var c in cells){var r=new Rect(start.x+c.x*unit,start.y+c.y*unit,unit-4,unit-4);Fill(r,color);Fill(new Rect(r.x+4,r.y+4,r.width-8,2),Color.Lerp(color,Color.white,.5f));Fill(new Rect(r.x+4,r.yMax-4,r.width-4,4),Color.Lerp(color,Deep,.25f));}
    }
    void DrawMinoNotice()
    {
        int a=newMino;Modal(new Rect(380,205,680,480),"",EveningBoard.Names[a]+" 미노 추가");
        DrawMino(new Rect(440,345,140,140),a);
        string effect=a==1?"덮은 네 칸을 그중 가장 낮은 높이로 맞춘다.\n기준은 효과 적용 전 높이.":a==0?"덮은 네 칸을 그중 가장 높은 높이로 맞춘다.\nS와 T가 겹친 칸은 원래 높이를 유지한다.\n기준은 효과 적용 전 높이.":"덮는 칸마다 높이 +1";
        Text(new Rect(625,335,390,160),effect,20,Ink,true);
        if(Button(new Rect(420,565,600,65),"확인",true,true)){knownMinos.Add(a);newMino=-1;}
    }
    void DrawSuccess()
    {
        bool tutorial=stageIndex<3,final=stageIndex==cleared.Length-1;var r=new Rect(332,205,776,500);
        Modal(r,"",tutorial?"명령 복원 완료":Stage.Weather?"날씨 복원 완료":"명칭 복원 완료");
        if(!tutorial)Text(new Rect(370,380,700,88),Stage.Name,24,Ink);
        if(Button(new Rect(370,596,700,60),tutorial&&stageIndex<2?"다음":final?"완료":"지도로",true,true))
        {
            success=false;if(tutorial&&stageIndex<2)EnterStage(stageIndex+1);else SetScreen(final?ScreenMode.End:ScreenMode.Map);
        }
    }
    void DrawEnd()
    {
        if(sky!=null)GUI.DrawTexture(new Rect(0,0,1440,900),sky,ScaleMode.ScaleAndCrop);
        Fill(new Rect(0,0,1440,900),new Color(.08f,.2f,.35f,.3f));
        DrawRetroTitle(355,false);
        Text(new Rect(320,460,800,55),"복원 완료",26,Color.white,true,TextAnchor.MiddleCenter);
        if(Button(new Rect(545,640,350,50),"타이틀로"))SetScreen(ScreenMode.Title);
    }
    void CheckPreviewMaterial(bool opaque)
    {
        if(restored!=opaque)throw new Exception("Restoration state must match preview opacity");
        var props=new MaterialPropertyBlock();int count=0;
        foreach(var go in modules)if(go.name=="actual")
        {
            count++;var renderer=go.GetComponent<MeshRenderer>();renderer.GetPropertyBlock(props);
            if(renderer.sharedMaterial!=(opaque?fillMat:ghostMat)||Mathf.Abs(props.GetColor("_Color").a-(opaque?1:PreviewAlpha))>.001f)throw new Exception("Preview opacity/material is incorrect");
        }
        if(count==0)throw new Exception("Preview material check requires actual cubes");
    }
    void CheckBoardView()
    {
        if(previewCamera.orthographic)throw new Exception("Board must use normal perspective");
        Vector3 front=new Vector3(2,0,-.5f),back=new Vector3(2,0,4.5f);
        float frontWidth=Vector2.Distance(previewCamera.WorldToViewportPoint(new Vector3(-.5f,0,-.5f)),previewCamera.WorldToViewportPoint(new Vector3(4.5f,0,-.5f)));
        float backWidth=Vector2.Distance(previewCamera.WorldToViewportPoint(new Vector3(-.5f,0,4.5f)),previewCamera.WorldToViewportPoint(new Vector3(4.5f,0,4.5f)));
        bool frontIsNear=(previewCamera.transform.position-front).sqrMagnitude<(previewCamera.transform.position-back).sqrMagnitude;
        if(frontIsNear?frontWidth<=backWidth:backWidth<=frontWidth)throw new Exception("Near board edge must project larger than far edge");
        foreach(var endpoint in frontEdge.vertices)if(Mathf.Abs(endpoint.z+.575f)>.001f)throw new Exception("Front marker must remain on the 2D bottom edge");
        if(frontEdgeMat.GetInt("_ZTest")!=(int)UnityEngine.Rendering.CompareFunction.Always)throw new Exception("Front marker must remain readable after orbiting");
    }
    IEnumerator CheckFullOrbit(string directory)
    {
        EnterStage(12);newMino=-1;
        float savedYaw=yaw,savedElevation=elevation,savedZoom=zoom;
        var start=previewCamera.transform.position;var startRotation=previewCamera.transform.rotation;
        Orbit(360,0);Orbit(0,360);
        if(Vector3.Distance(start,previewCamera.transform.position)>.001f||Quaternion.Angle(startRotation,previewCamera.transform.rotation)>.02f)throw new Exception("A full orbit must return to the same view");
        Orbit(725,-725);Orbit(-725,725);
        if(Vector3.Distance(start,previewCamera.transform.position)>.001f||Quaternion.Angle(startRotation,previewCamera.transform.rotation)>.02f)throw new Exception("Repeated revolutions must preserve camera framing");
        foreach(float angle in new[]{90f,132f,-90f,-32f})
        {
            Orbit(0,Mathf.DeltaAngle(elevation,angle));
            if(Mathf.Abs(Mathf.DeltaAngle(elevation,angle))>.001f)throw new Exception("Vertical orbit is still limited");
            var expected=Quaternion.Euler(angle,yaw,0);
            if(Quaternion.Angle(expected,previewCamera.transform.rotation)>.02f)throw new Exception("Orbit pole crossing changed camera orientation");
            for(int i=0;i<16;i++){float value=previewCamera.worldToCameraMatrix[i];if(float.IsNaN(value)||float.IsInfinity(value))throw new Exception("Orbit produced invalid camera matrix");}
            if(angle<0&&previewCamera.transform.position.y>=0)throw new Exception("Full orbit must reach below the board");
            foreach(var endpoint in frontEdge.vertices)if(Mathf.Abs(endpoint.z+.575f)>.001f)throw new Exception("Orbit moved the fixed front marker");
            yield return Shot(directory,"orbit-vertical-"+angle);
        }
        yaw=savedYaw;elevation=savedElevation;zoom=savedZoom;FrameCamera();
        if(Vector3.Distance(start,previewCamera.transform.position)>.001f)throw new Exception("View reset failed after full orbit");
        EnterStage(0);
        Debug.Log("AER_ORBIT_PASSED: both axes wrap through 360 degrees, repeated revolutions, over-pole and under-board views, finite matrices and fixed front edge");
    }
    IEnumerator CaptureOutline(string directory)
    {
        Directory.CreateDirectory(directory);
        EnterStage(0);yield return Shot(directory,"simple-surface-edges");
        EnterStage(6);newMino=-1;yield return Shot(directory,"target-surface-edges");
        yaw+=90;FrameCamera();yield return Shot(directory,"target-surface-edges-rotated");
        EnterStage(11);newMino=-1;yield return Shot(directory,"late-surface-edges");
        Debug.Log("AER_OUTLINE_CAPTURED: surface crease screenshots at multiple camera angles");Application.Quit();
    }
    IEnumerator CheckPlacementInteraction(string directory)
    {
        EnterStage(0);Select(4);onGrid=true;hover=new Vector2Int(1,1);RefreshPlacementPreview();
        if(board.Pieces.Count!=0||placementPreview==null||placementPreview.Heights()[1,1]!=1||Stage.Complete(board))throw new Exception("Hover preview must not commit a placement or clear a stage");
        CheckPreviewMaterial(false);yield return Shot(directory,"live-hover-before-click");
        hover=new Vector2Int(2,1);RefreshPlacementPreview();
        if(placementPreview.Heights()[1,1]!=0||placementPreview.Heights()[2,1]!=1||board.Pieces.Count!=0)throw new Exception("Hover preview did not follow the new board cell");
        HandlePuzzleClick(new Vector2(Grid.x+4.5f*88,Grid.y+4.5f*88));
        if(held>=0||placementPreview!=null||board.Pieces.Count!=0)throw new Exception("Invalid board click must return held piece and remove preview");

        EnterStage(12);newMino=-1;Select(4);onGrid=true;hover=new Vector2Int(1,1);Place();
        Select(1);hover=new Vector2Int(2,1);RefreshPlacementPreview();
        if(placementPreview==null||placementPreview.Heights()[1,1]!=1||placementPreview.Heights()[1,2]!=0||placementPreview.Heights()[2,2]!=0||board.Heights()[1,1]!=1||board.Pieces.Count!=1)throw new Exception("S hover must lower only its footprint to the original minimum without committing");
        yield return Shot(directory,"live-S-minimum-before-click");
        hover=new Vector2Int(4,4);RefreshPlacementPreview();
        if(placementPreview!=null||held!=1)throw new Exception("Invalid hover must hide tentative result while retaining selection");
        HandlePuzzleClick(Grid.position+new Vector2(4.5f*88,4.5f*88));
        if(held>=0||board.Pieces.Count!=1)throw new Exception("Invalid S placement changed committed pieces");

        HandlePuzzleClick(Grid.position+new Vector2(1.5f*88,1.5f*88));
        if(held!=4||board.Pieces.Count!=0||placementPreview==null)throw new Exception("Picked-up piece must have a live 3D preview");
        hover=new Vector2Int(2,1);RefreshPlacementPreview();yield return Shot(directory,"live-moving-existing-piece");
        HandlePuzzleClick(Grid.position+new Vector2(4.5f*88,4.5f*88));
        if(held>=0||placementPreview!=null||board.Pieces.Count!=0)throw new Exception("Invalid move must return picked-up piece to tray");

        Select(3);onGrid=true;hover=new Vector2Int(2,1);RefreshPlacementPreview();Rotate(1);
        if(placementPreview==null||placementPreview.Heights()[0,1]!=1||board.Pieces.Count!=0)throw new Exception("Rotation must update the tentative 3D result");
        Mirror();if(placementPreview==null||placementPreview.Heights()[4,1]!=1)throw new Exception("Mirroring must update the tentative 3D result");
        ReturnHeld();Select(4);hover=new Vector2Int(1,1);Place();
        Select(0);hover=new Vector2Int(1,1);RefreshPlacementPreview();
        if(placementPreview==null||placementPreview.MaxHeight!=1||placementPreview.Heights()[3,1]!=1||board.MaxHeight!=1||board.Pieces.Count!=1)throw new Exception("T hover must raise only its footprint to the original maximum without committing");
        HandlePuzzleClick(Preview.center);RefreshPlacementPreview();
        if(held!=0||placementPreview!=null)throw new Exception("Orbiting the 3D view must preserve the held piece and clear the board hover");
        HandlePuzzleClick(new Vector2(760,700));if(held!=0)throw new Exception("Transform controls must not cancel held pieces");
        HandlePuzzleClick(new Vector2(1055,800));
        if(held>=0||placementPreview!=null||board.Pieces.Count!=1||cleared[12])throw new Exception("Clicking blank space must cancel without committing or clearing");
        EnterStage(0);
        Debug.Log("AER_PLACEMENT_PASSED: live noncommitting hover, movement, rotate/mirror, S/T effects, invalid-click returns and UI controls");
    }
    IEnumerator CheckHintInteraction(string directory)
    {
        for(int n=0;n<AerStages.All.Length;n++)
        {
            EnterStage(n);newMino=-1;
            for(int attempt=0;attempt<=Stage.Supply;attempt++)
            {
                RequestHint();
                if(Stage.Complete(board))
                {
                    if(held>=0||hint>=0||hintPieceId>=0)throw new Exception("A solved board should receive execution advice");
                    break;
                }
                if(hint<0||hintPieceId>=0||held!=Stage.Solution[hint].Action)throw new Exception("Hint failed to identify next missing mino");
                int stepIndex=hint;var expected=Stage.Solution[hint].Cells();
                RequestHint();if(hint!=stepIndex)throw new Exception("Repeated hint clicks must not skip an unplaced step");
                if(n==12&&(hint==4||hint==5))yield return Shot(directory,"final-hint-step-"+(hint+1));
                onGrid=true;hover=hintAnchor;RefreshPlacementPreview();
                if(!AerHints.SameCells(Candidate(),expected))throw new Exception("Hint orientation/anchor does not match the highlighted footprint");
                int count=board.Pieces.Count;HandlePuzzleClick(Grid.position+new Vector2((hintAnchor.x+.5f)*88,(hintAnchor.y+.5f)*88));
                if(board.Pieces.Count!=count+1||held>=0)throw new Exception("Following the hint click did not place its mino");
            }
            if(!Stage.Complete(board))throw new Exception("Following hints did not solve stage "+n);
            if(n==10&&(board.Count(1)==0||!Stage.RequiredMinoUsed(board)))throw new Exception("Wind hints did not require meaningful S use");
        }
        EnterStage(12);newMino=-1;Select(4);hover=new Vector2Int(0,3);Place();
        RequestHint();
        if(hintPieceId<0||held>=0||board.TopAt(hintAnchor).Id!=hintPieceId)throw new Exception("A mismatched plan must receive a clickable correction hint");
        yield return Shot(directory,"hint-red-removal");
        HandlePuzzleClick(Grid.position+new Vector2((hintAnchor.x+.5f)*88,(hintAnchor.y+.5f)*88));
        if(board.Pieces.Count!=0||held!=4)throw new Exception("Correction hint did not identify the piece to move");
        ReturnHeld();RequestHint();if(hint!=0)throw new Exception("Correcting extra pieces must resume the example solution");

        EnterStage(1);RequestHint();onGrid=true;hover=hintAnchor;Place();Undo();RequestHint();
        if(hint!=0||board.Pieces.Count!=0)throw new Exception("Hints must recompute after undo");
        EnterStage(10);newMino=-1;
        foreach(var step in new[]{new AerStage.Step(3,0,0,2),new AerStage.Step(4,1,1),new AerStage.Step(3,1,2,2),new AerStage.Step(3,2,2,2)})board.TryPlace(step.Action,step.Cells(),out _);
        Rebuild();Execute();if(executing||success||cleared[10]||!message.Contains("S"))throw new Exception("S-free wind attempt bypassed execution gate");
        EnterStage(0);
        Debug.Log("AER_HINTS_PASSED: all 13 stages solved through production hints/click anchors; rotation and S/T types; duplicate placements; wrong-plan correction; undo; S-free wind rejected");
    }
    IEnumerator CheckStockInteraction(string directory)
    {
        EnterStage(1);newMino=-1;
        if(RemainingStock(4)!=2)throw new Exception("Initial finite stock incorrect");
        yield return Shot(directory,"stock-initial");
        Select(4);if(RemainingStock(4)!=1)throw new Exception("Holding must reserve one stock");
        ReturnHeld();if(RemainingStock(4)!=2)throw new Exception("Returning must restore stock");
        for(int i=0;i<2;i++){Select(4);hover=new Vector2Int(1,1);if(!Place())throw new Exception("Finite stock placement failed");}
        if(RemainingStock(4)!=0||Stage.CanPlace(board,4))throw new Exception("Stock did not exhaust");
        TrySelectBlockAt(TrayBlock(0).center);if(held>=0)throw new Exception("Exhausted tray must not select");
        yield return Shot(directory,"stock-exhausted");
        // Also reject a stale held selection at the production placement gate.
        held=4;orientation=(Vector2Int[])EveningBoard.Shapes[4].Clone();anchorIndex=0;hover=new Vector2Int(1,1);
        if(Place()||board.Count(4)!=2||held>=0)throw new Exception("Finite stock placement overflow");
        PickUpAt(new Vector2Int(1,1));
        if(board.Count(4)!=1||held!=4||RemainingStock(4)!=0)throw new Exception("Picking up must transfer, not duplicate, stock");
        yield return Shot(directory,"stock-moving");
        hover=new Vector2Int(2,1);if(!Place()||board.Count(4)!=2||RemainingStock(4)!=0)throw new Exception("Moving consumed extra stock");
        PickUpAt(new Vector2Int(1,1));hover=new Vector2Int(4,4);
        if(Place()||held>=0||board.Count(4)!=1||RemainingStock(4)!=1)throw new Exception("Invalid placement must return reserved stock");
        Undo();if(board.Count(4)!=0||RemainingStock(4)!=2)throw new Exception("Undo must return stock");
        Select(4);hover=new Vector2Int(1,1);Place();ResetBoard();
        if(board.Count(4)!=0||RemainingStock(4)!=2)throw new Exception("Reset must restore the complete supply");
        EnterStage(7);newMino=-1;
        for(int i=0;i<12;i++){Select(4);hover=new Vector2Int(1,1);if(!Place())throw new Exception("Weather supply must remain unlimited");}
        if(board.Count(4)!=12||RemainingStock(4)!=-1||!Stage.CanPlace(board,4))throw new Exception("Weather stock limit introduced");
        ResetBoard();yield return Shot(directory,"stock-weather-unlimited");
        EnterStage(0);
        Debug.Log("AER_STOCK_PASSED: finite non-weather quotas, held reservations, zero selection/placement guard, pickup/move/invalid return/undo/reset, unlimited weather");
    }
    IEnumerator Capture(string directory)
    {
        Directory.CreateDirectory(directory);screen=ScreenMode.Title;yield return Shot(directory,"01-title");
        SetScreen(ScreenMode.Boot);screenSince=Time.unscaledTime-1.8f;yield return Shot(directory,"02-terminal");
        SetScreen(ScreenMode.Map);confirmTutorial=true;yield return Shot(directory,"03-intro");confirmTutorial=false;yield return Shot(directory,"04-map");
        EnterStage(0);yield return Shot(directory,"05-tutorial-empty");
        yield return CheckFullOrbit(directory);
        yield return CheckPlacementInteraction(directory);
        yield return CheckHintInteraction(directory);
        yield return CheckStockInteraction(directory);
        ResetBoard();
        Select(4);hover=new Vector2Int(0,0);if(!Place())throw new Exception("Failed-preview setup rejected");
        Execute();while(executing){UpdateGrowth();yield return null;}
        if(success||board.Pieces.Count!=1)throw new Exception("Wrong solution must remain editable");
        CheckPreviewMaterial(false);yield return Shot(directory,"05-failed-transparent");ResetBoard();
        for(int n=0;n<cleared.Length;n++)
        {
            if(n>=3&&NextMain()!=n)throw new Exception("Progression order is incorrect at "+n);
            EnterStage(n);
            CheckBoardView();
            if(newMino>=0){yield return Shot(directory,"new-mino-"+n);knownMinos.Add(newMino);newMino=-1;}
            if(n==6||n==11)
            {
                yield return Shot(directory,"target-surface-edges-"+n);
                if(n==6)
                {
                    float original=yaw;
                    foreach(int turn in new[]{90,180,270})
                    {
                        yaw=original+turn;FrameCamera();CheckBoardView();
                        if(turn!=270)yield return Shot(directory,"front-edge-rotated-"+turn);
                    }
                    yaw=original;FrameCamera();
                }
            }
            if(TrySelectBlockAt(new Vector2(1100,240)))throw new Exception("Empty inventory panel must not select a block");
            int typeCount=TrayActions().Count;
            foreach(var step in Stage.Solution)
            {
                int tray=TrayActions().IndexOf(step.Action);if(tray<0||!TrySelectBlockAt(TrayBlock(tray).center))throw new Exception("Clicking the module must select it");
                if(step.Mirrored)Mirror();for(int i=0;i<step.Turns;i++)Rotate(1);hover=step.Cells()[0];if(!Place())throw new Exception("Known tutorial solution rejected");
            }
            if(TrayActions().Count!=typeCount)throw new Exception("Placing pieces must not consume tray icons");
            CheckPreviewMaterial(false);
            if(n==6||n==11)yield return Shot(directory,"preview-transparent-"+n);
            Execute();CheckPreviewMaterial(false);while(executing){UpdateGrowth();yield return null;}
            if(!success)throw new Exception("Tutorial solution did not clear stage "+n);
            CheckPreviewMaterial(true);
            if(n==6||n==11)yield return Shot(directory,"restored-opaque-"+n);
            yield return new WaitForSeconds(.7f);
            yield return Shot(directory,"06-clear-"+n);success=false;yield return Shot(directory,"07-puzzle-"+n);
            if(n>=3){SetScreen(ScreenMode.Map);yield return Shot(directory,"map-after-"+n);}
        }
        if(NextMain()!=cleared.Length||ClearCount()!=10)throw new Exception("Must finish exactly 8 names and 2 weather stages");
        SetScreen(ScreenMode.Map);yield return Shot(directory,"08-map-complete");
        SetScreen(ScreenMode.End);yield return Shot(directory,"09-end");
        Debug.Log("AER_RUNTIME_PASSED: normal near-large perspective in four orbit quadrants; fixed bottom/front marker; 80% preview and opaque restoration; tutorial and all 10 main stages; finite non-weather stock and unlimited weather; no persistence.");Application.Quit();
    }
    IEnumerator Shot(string directory,string name)
    {
        yield return null;yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(directory,name+".png"));
        yield return null;yield return new WaitForEndOfFrame();yield return null;
    }
    void OnDestroy()
    {
        foreach(var go in modules)if(go!=null)Destroy(go);if(sceneRoot!=null)Destroy(sceneRoot.gameObject);
        if(previewCamera!=null)Destroy(previewCamera.gameObject);if(backgroundCamera!=null)Destroy(backgroundCamera.gameObject);
        if(maskCamera!=null)Destroy(maskCamera.gameObject);
        if(maskTexture!=null){maskTexture.Release();Destroy(maskTexture);}
        if(previewTexture!=null){previewTexture.Release();Destroy(previewTexture);}Destroy(fillMat);Destroy(lineMat);Destroy(ghostMat);Destroy(targetDepthMat);Destroy(targetLineMat);Destroy(frontEdgeMat);Destroy(targetEdges);Destroy(frontEdge);Destroy(cube);Destroy(edge);Destroy(triangle);Destroy(clickSound);Destroy(completeSound);
    }
}
