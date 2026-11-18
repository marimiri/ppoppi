using System;
using System.Collections.Generic;
using UnityEngine;

// Daily minos live in the time/depth plane. X=day, Y=depth, Z=time.
public sealed partial class BlockXXPrototype : MonoBehaviour
{
    public const int DayCount=7;
    public const int SlotsPerDay=10;
    public const int BoardCellCount=DayCount*SlotsPerDay;
    const float DefaultZoom=5.9f;
    public static readonly string[] Names = { "식물 키우기", "소설책 읽기", "하모니카 불기", "교신 시도", "아무것도 안 하기", "약 먹기", "청소", "꿈꾸기", "이벤트" };
    public static readonly Vector3Int[][] Shapes = {
        new[]{ V(0,0), V(1,0), V(2,0), V(1,1) },
        new[]{ V(1,0), V(2,0), V(0,1), V(1,1) },
        new[]{ V(0,0), V(1,0), V(2,0), V(3,0) },
        new[]{ V(0,0), V(0,1), V(0,2), V(1,2) },
        new[]{ V(0,0), V(1,0), V(0,1), V(1,1) },
        new[]{ V(0,0) },
        new[]{ V(0,0), V(1,0), V(2,0) },
        new[]{ V(0,0), V(0,1), V(1,1) },
        new[]{ V(0,0), V(1,0) }
    };
    static Vector3Int V(int time, int depth) { return new Vector3Int(0,depth,time); }
    public sealed class Piece { public int action; public Vector3Int[] cells; public int id; public bool fixedPlacement,sharedVolume; public string label; }
    readonly List<Piece> pieces = new List<Piece>();
    readonly List<GameObject> solids = new List<GameObject>(), ghosts = new List<GameObject>();
    public static readonly Color[] BlockColors = {
        new Color(.66f,.85f,.37f), new Color(.79f,.69f,.92f), new Color(.97f,.64f,.78f),
        new Color(.53f,.79f,.95f), new Color(.055f,.055f,.065f), Color.white, new Color(.98f,.85f,.32f), Color.white, new Color(.50f,.51f,.54f)
    };
    Camera boardCamera; Material solidMaterial, ghostMaterial, lineMaterial; Mesh cubeMesh, edgeMesh;
    Texture2D portrait; Font korean; GUIStyle text, small, heading, title, button, number;
    Transform visuals;
    int held = -1, nextId = 1, week = 1, depth, day, slot, anchorIndex;
    Vector3Int[] orientation;
    bool hasTarget, valid, shiftWasHeld, started, badEnding, medicationWarning;
    float endingStarted;
    MeshRenderer anchorRenderer,anchorEdge;
    MaterialPropertyBlock pulseProperties;
    bool captureMode;
    float shiftTravel, previousMouseY, zoom = DefaultZoom;
    float cameraYaw=30f,cameraElevation=40f;
    readonly BoardDragGesture boardDrag=new BoardDragGesture();
    // GUI coordinates are resolution independent, so click tolerance is consistent.
    public sealed class BoardDragGesture
    {
        public bool Active {get;private set;}
        public bool Dragging {get;private set;}
        Vector2 origin,previous;
        public void Begin(Vector2 point){Active=true;Dragging=false;origin=previous=point;}
        public Vector2 Move(Vector2 point)
        {
            if(!Active)return Vector2.zero;
            if(!Dragging&&(point-origin).sqrMagnitude<36)return Vector2.zero;
            Dragging=true;Vector2 delta=point-previous;previous=point;return delta;
        }
        public bool End(){bool click=Active&&!Dragging;Cancel();return click;}
        public void Cancel(){Active=false;Dragging=false;}
    }
    string message = "위쪽 목록에서 블록을 골라 이브의 일주일을 채워주세요.";
    string invalidReason = "";
    Vector3Int[] candidate;
    string ghostKey = "";
    Rect boardRect = new Rect(325,250,900,510);
    float scale; Vector2 offset;
    readonly string[] days = { "월", "화", "수", "목", "금", "토", "일" };
    static readonly Color Ink = new Color(.13f,.14f,.15f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap() { if (FindFirstObjectByType<BlockXXPrototype>() == null) new GameObject("Block XX").AddComponent<BlockXXPrototype>(); }

    void Awake()
    {
        pulseProperties=new MaterialPropertyBlock();
        Application.runInBackground=true;
        korean = Resources.Load<Font>("Fonts/Malgun");
        if (korean == null) korean = Font.CreateDynamicFontFromOSFont("Malgun Gothic",18);
        portrait = Resources.Load<Texture2D>("Art/EvePortrait");
        var old = Camera.main; if (old != null) old.enabled = false;
        boardCamera = new GameObject("Schedule Camera").AddComponent<Camera>();
        boardCamera.orthographic = false;boardCamera.fieldOfView=35f;boardCamera.clearFlags = CameraClearFlags.SolidColor;
        boardCamera.backgroundColor = new Color(.965f,.965f,.965f);
        boardCamera.nearClipPlane = .1f; boardCamera.farClipPlane = 160;
        boardCamera.cullingMask = 1 << 30;
        visuals = new GameObject("Schedule Blocks").transform;
        var shader = Shader.Find("BlockXX/Ink");
        solidMaterial = new Material(shader); ghostMaterial = new Material(shader);
        ghostMaterial.SetInt("_ZWrite",0); ghostMaterial.SetInt("_ZTest",8); ghostMaterial.renderQueue = 3000;
        lineMaterial = new Material(shader);
        cubeMesh = BuildCube(); edgeMesh = BuildEdges();
        Resize(); Reset(false);
        string[] args=Environment.GetCommandLineArgs();
        for(int i=0;i+1<args.Length;i++)if(args[i]=="-blockxx-capture"){captureMode=true;StartCoroutine(CaptureFrame(args[i+1]));}
    }
    System.Collections.IEnumerator CaptureFrame(string path)
    {
        Debug.Log("BLOCKXX_CAPTURE_STARTED");
        suppressRandomDreams=true;Reset(false);
        CaptureChecks();
        yield return CheckWeekExecution();
        yield return CheckRouteModes();
        CheckRandomDreams();
        for(int i=0;i<6;i++)yield return new WaitForEndOfFrame();
        ScreenCapture.CaptureScreenshot(path);
        for(int i=0;i<6;i++)yield return new WaitForEndOfFrame();
        Seed(0,0,1);Seed(1,2,0);Seed(2,4,1);Seed(4,6,3);Seed(5,1,4);
        Rebuild();Choose(3);day=5;slot=1;depth=0;hasTarget=true;UpdateCandidate();
        message="블록을 골라 이브의 일주일을 만들어주세요.";
        for(int i=0;i<3;i++)yield return new WaitForEndOfFrame();
        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(path),System.IO.Path.GetFileNameWithoutExtension(path)+"-placement.png"));
        for(int i=0;i<6;i++)yield return new WaitForEndOfFrame();
        Debug.Log("BLOCKXX_CAPTURE_COMPLETED");
        Application.Quit();
    }
    void Seed(int action,int d,int s)
    {
        var cells=(Vector3Int[])Shapes[action].Clone();for(int i=0;i<cells.Length;i++)cells[i]+=new Vector3Int(d,0,s);
        pieces.Add(new Piece{action=action,cells=cells,id=nextId++});
    }
    void CaptureChecks()
    {
        if(!Validate(Shapes[0],out _))throw new Exception("Initial T placement rejected.");
        Seed(0,0,0);
        if(Validate(Shapes[0],out _))throw new Exception("Overlapping voxels must be rejected.");
        var crossDay=Turn(Shapes[2],true,1);for(int i=0;i<crossDay.Length;i++)crossDay[i]+=new Vector3Int(0,0,6);
        if(!Validate(crossDay,out _))throw new Exception("Cross-day temporary placement must be allowed before final awareness validation.");
        pieces.Clear();Seed(5,0,0);Seed(5,0,1);Seed(5,0,2);
        BeginWeek();if(badEnding||medicationWarning||started)throw new Exception("An incomplete board must not begin any activity or ending flow.");
        pieces.Clear();for(int d=0;d<DayCount;d++)for(int s=0;s<SlotsPerDay;s++)Seed(5,d,s);
        BeginWeek();if(!medicationWarning||badEnding)throw new Exception("Medication must ask for a choice before ending.");
        int count=pieces.Count;DismissMedicationWarning();
        if(medicationWarning||badEnding||started||pieces.Count!=count)throw new Exception("Rearranging must preserve all placed blocks.");
        BeginWeek();ProceedMedicationEnding();
        if(!badEnding||medicationWarning||started)throw new Exception("Ignoring the warning must enter the ending.");
        Reset(false);
        Choose(1);day=1;slot=2;depth=2;hasTarget=true;UpdateCandidate();
        Vector3Int target=new Vector3Int(day,depth,slot);
        if(candidate[anchorIndex]!=target)throw new Exception("S mino must anchor a real voxel at the cursor, not an empty bounding-box corner.");
        Rotate(false,1);Mirror();
        if(candidate[anchorIndex]!=target)throw new Exception("Rotating and mirroring must preserve the highlighted cursor voxel.");
        Reset(false);
        Seed(0,2,1);Choose(5);
        if(!TrySnapToSurface(pieces,new Ray(new Vector3(2,-.5f,-3),Vector3.forward),out Vector3Int contact)||contact!=new Vector3Int(2,0,0))
            throw new Exception("A cursor on a placed mino's side must target the adjacent free time cell.");
        day=contact.x;depth=contact.y;slot=contact.z;hasTarget=true;UpdateCandidate();
        if(!valid)throw new Exception("Side-contact preview of a medicine must be placeable.");
        ClickBoard();
        if(pieces.Count!=2||held>=0||pieces[1].cells[0]!=contact)throw new Exception("Committed placement must match the surface-contact preview exactly.");
        Reset(false);
        Vector3 position=boardCamera.transform.position,forward=boardCamera.transform.forward,right=boardCamera.transform.right;
        Vector3 close=position+forward*8,far=position+forward*16;
        float closeWidth=boardCamera.WorldToViewportPoint(close+right*.5f).x-boardCamera.WorldToViewportPoint(close-right*.5f).x;
        float farWidth=boardCamera.WorldToViewportPoint(far+right*.5f).x-boardCamera.WorldToViewportPoint(far-right*.5f).x;
        if(boardCamera.orthographic||closeWidth<=farWidth)throw new Exception("Nearer equal-sized board cells must project larger than farther cells.");
        foreach(float d in new[]{-.5f,DayCount-.5f})foreach(float s in new[]{-.5f,SlotsPerDay-.5f})
        {
            Vector3 point=boardCamera.WorldToViewportPoint(new Vector3(d,0,s));
            if(point.z<=0||point.x<0||point.x>1||point.y<0||point.y>1)throw new Exception("The full ten-slot board must fit the default camera view.");
        }
        Debug.Log("BLOCKXX_RUNTIME_CHECKS_PASSED: placement, medication choices, upper anchor, surface contact, correct perspective scale");
    }
    void OnDestroy()
    {
        if(boardCamera != null) Destroy(boardCamera.gameObject);
        if(visuals != null) Destroy(visuals.gameObject);
        Destroy(solidMaterial); Destroy(ghostMaterial); Destroy(lineMaterial); Destroy(cubeMesh); Destroy(edgeMesh);
    }
    void Resize()
    {
        scale = Mathf.Min(Screen.width/1440f, Screen.height/900f);
        offset = new Vector2((Screen.width-1440*scale)/2,(Screen.height-900*scale)/2);
        boardCamera.pixelRect = new Rect(offset.x+boardRect.x*scale, Screen.height-offset.y-boardRect.yMax*scale,boardRect.width*scale,boardRect.height*scale);
        Vector3 focus=new Vector3((DayCount-1)*.5f,-.7f,(SlotsPerDay-1)*.5f);
        Quaternion view=BoardViewRotation(cameraElevation,cameraYaw);
        // Keep the familiar framing at the board center, but move the camera to zoom.
        // A positive perspective lens makes nearby cells genuinely appear larger.
        float distance=zoom/Mathf.Tan(boardCamera.fieldOfView*.5f*Mathf.Deg2Rad);
        boardCamera.transform.SetPositionAndRotation(focus+view*Vector3.back*distance,view);
    }
    Vector2 MouseGUI() { return (new Vector2(Input.mousePosition.x,Screen.height-Input.mousePosition.y)-offset)/scale; }
    void Update()
    {
        TickRemovalEffects();
        if(captureMode){Resize();PulseAnchor();return;}
        if(badEnding){if(Time.unscaledTime-endingStarted>=4f)ExitGame();return;}
        if(medicationWarning)return;
        if(boardDrag.Active&&!Input.GetMouseButton(0)&&!Input.GetMouseButtonUp(0))boardDrag.Cancel();
        float step=Time.unscaledDeltaTime*45f;
        if(Input.GetKey(KeyCode.A))cameraYaw-=step;
        if(Input.GetKey(KeyCode.D))cameraYaw+=step;
        if(Input.GetKey(KeyCode.W))cameraElevation+=step;
        if(Input.GetKey(KeyCode.S))cameraElevation-=step;
        Resize();
        Vector2 m = MouseGUI();
        if(boardRect.Contains(m)) zoom = Mathf.Clamp(zoom-Input.mouseScrollDelta.y*.35f,2.8f,18f);
        bool shift = Input.GetKey(KeyCode.LeftShift)||Input.GetKey(KeyCode.RightShift);
        if(held>=0 && shift)
        {
            if(!shiftWasHeld) { shiftTravel=0; previousMouseY=Input.mousePosition.y; }
            shiftTravel += (previousMouseY-Input.mousePosition.y)/scale;
            int steps = (int)(shiftTravel/22f);
            if(steps!=0) { depth=Mathf.Max(0,depth+steps); shiftTravel-=steps*22f; }
        }
        else if(held>=0)UpdateAnchor(m);
        previousMouseY=Input.mousePosition.y; shiftWasHeld=shift;
        if(Input.GetKeyDown(KeyCode.Escape)||Input.GetMouseButtonDown(1)){boardDrag.Cancel();ReturnHeld();}
        if(held>=0 && !started && !badEnding && !RouteIsOver)
        {
            if(Input.GetKeyDown(KeyCode.Q)) Rotate(true,-1);
            if(Input.GetKeyDown(KeyCode.E)) Rotate(true,1);
            if(Input.GetKeyDown(KeyCode.R)) Rotate(false,1);
            if(Input.GetKeyDown(KeyCode.F)) Mirror();
            UpdateCandidate();
        }
        PulseAnchor();
    }
    void OnApplicationFocus(bool focused){if(!focused)boardDrag.Cancel();}
    public static Quaternion BoardViewRotation(float elevation,float yaw)
    {
        // Preserve the camera's own up axis through the poles, including inverted views.
        return Quaternion.Euler(Mathf.Repeat(elevation,360),Mathf.Repeat(yaw,360),0);
    }
    void OrbitByDrag(Vector2 delta)
    {
        // Horizontal direction follows the player's preferred orbit gesture.
        cameraYaw+=delta.x*.28f;
        cameraElevation+=delta.y*.28f;
        Resize();
    }
    Ray PointerRay() { return boardCamera.ScreenPointToRay(Input.mousePosition); }
    void UpdateAnchor(Vector2 m)
    {
        hasTarget=false;
        if(!boardRect.Contains(m)) return;
        if(TrySnapToSurface(pieces,PointerRay(),out Vector3Int target))
        {
            if(mode==PlayMode.Reading)
            {
                Piece touched=PieceAtPointer(out Vector3Int touchedCell);
                if(touched!=null&&CanShareWith(touched,held,touchedCell))target=touchedCell;
            }
            day=target.x;depth=target.y;slot=target.z;hasTarget=true;
        }
    }
    // Ray-box intersection returns the actual visible face, even when the board is upside down.
    // Normals are returned in schedule coordinates, where positive Y means deeper.
    public static bool TryHitVoxel(Ray ray,Vector3Int cell,out float distance,out Vector3Int gridNormal)
    {
        return TryHitBounds(ray,new Bounds(World(cell),Vector3.one*.96f),out distance,out gridNormal);
    }
    static bool TryHitBounds(Ray ray,Bounds bounds,out float distance,out Vector3Int gridNormal)
    {
        float enter=float.NegativeInfinity,exit=float.PositiveInfinity;
        Vector3Int enterNormal=Vector3Int.zero,exitNormal=Vector3Int.zero;
        distance=0;gridNormal=Vector3Int.zero;
        for(int axis=0;axis<3;axis++)
        {
            float direction=ray.direction[axis],origin=ray.origin[axis],lo=bounds.min[axis],hi=bounds.max[axis];
            if(Mathf.Abs(direction)<.000001f){if(origin<lo||origin>hi)return false;continue;}
            float near=(lo-origin)/direction,far=(hi-origin)/direction;
            Vector3Int nearNormal=Vector3Int.zero,farNormal=Vector3Int.zero;
            nearNormal[axis]=-1;farNormal[axis]=1;
            if(near>far){float swap=near;near=far;far=swap;var ns=nearNormal;nearNormal=farNormal;farNormal=ns;}
            if(near>enter){enter=near;enterNormal=nearNormal;}
            if(far<exit){exit=far;exitNormal=farNormal;}
            if(enter>exit)return false;
        }
        if(exit<0)return false;
        distance=enter>=0?enter:exit;
        Vector3Int worldNormal=enter>=0?enterNormal:exitNormal;
        gridNormal=new Vector3Int(worldNormal.x,-worldNormal.y,worldNormal.z);
        return true;
    }
    public static bool TrySnapToSurface(IEnumerable<Piece> source,Ray ray,out Vector3Int target)
    {
        target=default;float best=float.MaxValue;bool hit=false;
        var occupiedColumns=new HashSet<Vector2Int>();
        foreach(var p in source)foreach(var c in p.cells)
        {
            occupiedColumns.Add(new Vector2Int(c.x,c.z));
            if(TryHitBounds(ray,PieceBounds(p,c,source),out float distance,out Vector3Int normal)&&distance<best)
            {best=distance;target=c+normal;hit=true;}
        }
        // Only exposed, empty board tiles can be targeted. Occupied columns have no
        // invisible plane in front of their blocks that could steal the ray hit.
        if(new Plane(Vector3.up,Vector3.zero).Raycast(ray,out float boardDistance)&&boardDistance<best)
        {
            Vector3 point=ray.GetPoint(boardDistance);int d=Mathf.FloorToInt(point.x+.5f),s=Mathf.FloorToInt(point.z+.5f);
            if(d>=0&&d<DayCount&&s>=0&&s<SlotsPerDay&&!occupiedColumns.Contains(new Vector2Int(d,s)))
            {target=new Vector3Int(d,0,s);hit=true;}
        }
        return hit;
    }
    public static Vector3 World(Vector3Int c) { return new Vector3(c.x,-c.y-.5f,c.z); }
    public static Vector3Int[] Turn(Vector3Int[] source,bool yaw,int direction)
    {
        var result=new Vector3Int[source.Length];
        for(int i=0;i<source.Length;i++)
        {
            var c=source[i];
            result[i]=yaw?new Vector3Int(direction*c.z,c.y,-direction*c.x):new Vector3Int(c.x,direction*c.z,-direction*c.y);
        }
        Normalize(result);return result;
    }
    static void Normalize(Vector3Int[] cells)
    {
        var min=cells[0]; foreach(var c in cells) min=Vector3Int.Min(min,c);
        for(int i=0;i<cells.Length;i++) cells[i]-=min;
    }
    void Rotate(bool yaw,int direction)
    {
        if(held<0)return;
        if(yaw)orientation=Turn(orientation,true,direction);
        else
        {
            // Snap the camera's horizontal rotation axis to a voxel-grid axis.
            // This keeps every quarter-turn on exact integer cells as the view orbits.
            Vector3 right=boardCamera.transform.right;
            if(Mathf.Abs(right.x)>=Mathf.Abs(right.z))orientation=Turn(orientation,false,direction*(right.x>=0?1:-1));
            else
            {
                int sign=direction*(right.z>=0?1:-1);
                for(int i=0;i<orientation.Length;i++){var c=orientation[i];orientation[i]=new Vector3Int(sign*c.y,-sign*c.x,c.z);}
                Normalize(orientation);
            }
        }
        anchorIndex=BoardNearestAnchor(orientation);UpdateCandidate();
    }
    void Mirror() { if(held<0)return;for(int i=0;i<orientation.Length;i++) orientation[i].z=-orientation[i].z;Normalize(orientation);anchorIndex=BoardNearestAnchor(orientation);UpdateCandidate(); }
    public static int BoardNearestAnchor(Vector3Int[] cells)
    {
        int nearest=0;
        for(int i=1;i<cells.Length;i++)if(cells[i].y<cells[nearest].y)nearest=i;
        return nearest;
    }
    void Choose(int action)
    {
        if(started||badEnding||RouteIsOver||!CanChooseAction(action))return;
        boardDrag.Cancel();
        held=action; orientation=(Vector3Int[])Shapes[action].Clone();anchorIndex=BoardNearestAnchor(orientation);
        depth=0;ghostKey="";
        message=Names[action]+" 선택 · 클릭 배치 / Shift+마우스 위아래: 깊이 / 보드 밖 클릭: 반환";
    }
    void ReturnHeld()
    {
        if(held<0)return;
        held=-1;candidate=null;ghostKey="";Clear(ghosts);message="블록을 목록으로 돌려보냈어요.";
    }
    void UpdateCandidate()
    {
        if(held<0)return;
        candidate=new Vector3Int[orientation.Length];
        // Keep a real voxel, rather than an empty corner of the bounding box, under the cursor.
        for(int i=0;i<candidate.Length;i++)candidate[i]=orientation[i]-orientation[anchorIndex]+new Vector3Int(day,depth,slot);
        valid=hasTarget&&Validate(candidate, out invalidReason);
        if(!hasTarget) invalidReason="보드 위로 커서를 옮겨주세요.";
        string key=held+":"+valid+":"+hasTarget+":"+string.Join(";",Array.ConvertAll(candidate,c=>c.ToString()));
        if(key==ghostKey)return;
        ghostKey=key;Clear(ghosts);
        if(hasTarget)for(int i=0;i<candidate.Length;i++)AddCube(candidate[i],held,true,ghosts,valid,i==anchorIndex);
    }
    void PulseAnchor()
    {
        if(anchorRenderer==null||held<0||!hasTarget)return;
        float pulse=.5f+.5f*Mathf.Sin(Time.unscaledTime*Mathf.PI*2/3f);
        Color tint=Color.Lerp(BlockColors[held],Color.white,.08f+.10f*pulse);
        tint.a=(valid?.55f:.20f)+pulse*.16f;
        pulseProperties.SetColor("_Color",tint);anchorRenderer.SetPropertyBlock(pulseProperties);
        if(anchorEdge!=null)
        {
            Color edge=held==4?Color.white:Ink;edge.a=.65f+.25f*pulse;
            pulseProperties.SetColor("_Color",edge);anchorEdge.SetPropertyBlock(pulseProperties);
        }
    }
    bool Validate(Vector3Int[] cells,out string reason)
    {
        reason="";
        if(!ValidateRoutePlacement(cells,out reason))return false;
        foreach(var c in cells)
        {
            if(c.x<0||c.x>=DayCount||c.z<0||c.z>=SlotsPerDay||c.y<0) {reason="블록이 하루 "+SlotsPerDay+"개 시간대 안에 들어와야 해요.";return false;}
            foreach(var p in pieces)foreach(var occupied in p.cells)if(c==occupied&&!CanShareWith(p,held,c)) {reason="이미 블록이 있는 칸이에요. Shift로 깊이를 바꿔보세요.";return false;}
        }
        // Awareness is a property of the final schedule. Allow provisional, cross-day
        // and hidden placements while editing; reject unresolved pieces at BeginWeek.
        return true;
    }
    static void Top(Dictionary<Vector2Int,int> tops,Vector3Int c)
    {
        var k=new Vector2Int(c.x,c.z);if(!tops.ContainsKey(k)||c.y<tops[k])tops[k]=c.y;
    }
    static bool Exposed(Vector3Int[] cells,Dictionary<Vector2Int,int> tops)
    {foreach(var c in cells)if(tops[new Vector2Int(c.x,c.z)]==c.y)return true;return false;}
    void ClickBoard()
    {
        if(started||badEnding||RouteIsOver)return;
        if(held>=0)
        {
            UpdateCandidate();if(!hasTarget){ReturnHeld();return;}
            if(!valid){message=invalidReason;return;}
            pieces.Add(new Piece{action=held,cells=(Vector3Int[])candidate.Clone(),id=nextId++,sharedVolume=mode==PlayMode.Reading&&held==1});
            string name=Names[held];held=-1;candidate=null;Clear(ghosts);Rebuild();message=name+" 배치 완료 · 배치한 블록을 클릭하면 다시 옮길 수 있어요.";
            return;
        }
        Piece picked=PieceAtPointer(out _);
        if(picked==null)return;
        if(picked.fixedPlacement){message="고정 일정과 이벤트는 옮길 수 없어요.";return;}
        pieces.Remove(picked);held=picked.action;orientation=(Vector3Int[])picked.cells.Clone();Normalize(orientation);
        anchorIndex=BoardNearestAnchor(orientation);
        Vector3Int upper=picked.cells[anchorIndex];day=upper.x;slot=upper.z;depth=upper.y;
        Rebuild();ghostKey="";UpdateCandidate();message=Names[held]+" 회수 · 위치를 골라 다시 놓거나, 보드 밖을 클릭해 반환하세요.";
    }
    Piece PieceAtPointer(out Vector3Int hitCell)
    {
        Ray ray=PointerRay();float best=float.MaxValue;Piece picked=null;hitCell=default;
        foreach(var p in pieces)foreach(var c in p.cells)
            if(TryHitBounds(ray,PieceBounds(p,c,pieces),out float distance,out _)&&distance<best){best=distance;picked=p;hitCell=c;}
        return picked;
    }
    public static int CountLoad(IEnumerable<Piece> source)
    {
        var counts=new Dictionary<Vector2Int,int>();
        var merged=new HashSet<Vector3Int>();
        foreach(var p in source)
        {
            // Rest contributes neither its own voxels nor extra stack height to load.
            // Its reduced recovery effect is applied separately through EvaluateRest.
            if(p.action==4||p.action>=7)continue;
            foreach(var c in p.cells)
            {
                if(SharedAt(c,source)&&!merged.Add(c))continue;
                var k=new Vector2Int(c.x,c.z);counts[k]=counts.ContainsKey(k)?counts[k]+1:1;
            }
        }
        int result=0;foreach(int n in counts.Values)result+=Math.Max(0,n-1);return result;
    }
    float Gain(int action)
    {
        return RouteGain(action);
    }
    int Coverage()
    {var k=new HashSet<Vector2Int>();foreach(var p in pieces)foreach(var c in p.cells)k.Add(new Vector2Int(c.x,c.z));return k.Count;}
    public enum WeekStartDecision { HoldingBlock, IncompleteBoard, MedicationWarning, Ready }
    public static WeekStartDecision EvaluateWeekStart(IEnumerable<Piece> source,bool holding)
    {
        if(holding)return WeekStartDecision.HoldingBlock;
        var covered=new HashSet<Vector2Int>();var medicine=new int[7];
        foreach(var p in source)
        {
            foreach(var c in p.cells)if(c.x>=0&&c.x<DayCount&&c.z>=0&&c.z<SlotsPerDay&&c.y>=0)covered.Add(new Vector2Int(c.x,c.z));
            if(p.action==5&&p.cells.Length>0&&p.cells[0].x>=0&&p.cells[0].x<7)medicine[p.cells[0].x]++;
        }
        if(covered.Count!=BoardCellCount)return WeekStartDecision.IncompleteBoard;
        foreach(int count in medicine)if(count>=3)return WeekStartDecision.MedicationWarning;
        return WeekStartDecision.Ready;
    }
    void BeginWeek()
    {
        if(medicationWarning||badEnding||started||resolving||RouteIsOver)return;
        var decision=EvaluateWeekStart(pieces,held>=0);
        if(decision==WeekStartDecision.HoldingBlock){message="들고 있는 블록을 배치하거나 목록으로 돌려보내주세요.";return;}
        if(!StartRulesReady(out string ruleReason)){message=ruleReason;return;}
        if(decision==WeekStartDecision.IncompleteBoard){message="아직 비어 있는 시간대가 있어요. "+BoardCellCount+"칸을 모두 채워주세요.";return;}
        if(RouteEnabled&&NeedsCleaning&&PlacedCount(6)<1){message="이번 주 필수 청소 블록을 배치해주세요.";return;}
        if(decision==WeekStartDecision.MedicationWarning){boardDrag.Cancel();medicationWarning=true;return;}
        CaptureRouteSchedule();started=true;Clear(ghosts);StartWeekResolution();
    }
    void DismissMedicationWarning(){medicationWarning=false;message="약 블록을 회수해 하루 2회 이하로 바꿔주세요.";}
    void ProceedMedicationEnding()
    {if(!medicationWarning)return;medicationWarning=false;if(RouteEnabled){SetRouteOutcome(RouteOutcome.Bad,"하루에 약을 3개 이상 배치했어요.");return;}badEnding=true;started=false;endingStarted=Time.unscaledTime;boardDrag.Cancel();Clear(ghosts);}
    void ExitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying=false;
#else
        Application.Quit();
#endif
    }
    void Reset(bool advance)
    {
        if(advance&&mode==PlayMode.Reading&&!weekCleaningDone){message="이번 주 청소를 한 번 수행해야 다음 주차로 갈 수 있어요.";return;}
        CancelWeekResolution();boardDrag.Cancel();pieces.Clear();ReturnHeld();started=false;frozenAwareness=null;badEnding=false;medicationWarning=false;if(advance)week++;depth=0;PrepareRouteWeek();Rebuild();message="새 일정을 자유롭게 배치해보세요.";
    }
    void Rebuild()
    {
        Clear(solids);
        cellVisuals.Clear();
        var occupied=new HashSet<Vector2Int>();var states=CurrentAwareness();
        foreach(var p in pieces)foreach(var c in p.cells){occupied.Add(new Vector2Int(c.x,c.z));AddCube(c,p.action,false,solids,states[p]!=Awareness.Invalid);}
        for(int d=0;d<DayCount;d++)for(int s=0;s<SlotsPerDay;s++)if(!occupied.Contains(new Vector2Int(d,s)))
        {
            var o=MakeMesh(cubeMesh,solidMaterial,new Vector3(d,-.045f,s),new Vector3(.96f,.06f,.96f),new Color(.91f,.90f,.88f));solids.Add(o);
            var edge=MakeMesh(edgeMesh,lineMaterial,new Vector3(d,-.045f,s),new Vector3(.96f,.06f,.96f),new Color(.68f,.68f,.66f));solids.Add(edge);
        }
    }
    void AddCube(Vector3Int c,int action,bool ghost,List<GameObject> list,bool legal,bool highlight=false)
    {
        Color tint=BlockColors[action];tint.a=ghost?(legal?.52f:.16f):1;
        bool split=SharedAt(c,pieces)||(ghost&&mode==PlayMode.Reading&&CanPreviewShared(c,action));
        Vector3 position=World(c)+(split?Vector3.right*(action==1?-.25f:.25f):Vector3.zero);
        Vector3 size=new Vector3(split?.46f:.96f,.96f,.96f);
        var o=MakeMesh(cubeMesh,ghost?ghostMaterial:solidMaterial,position,size,tint);list.Add(o);
        Color outline=action==4?new Color(.6f,.6f,.62f):Ink;outline.a=ghost?.6f:1;
        if(!ghost&&!legal)outline=new Color(.85f,.16f,.13f);
        var edge=MakeMesh(edgeMesh,ghost?ghostMaterial:lineMaterial,position,size+Vector3.one*.005f,outline);list.Add(edge);
        if(!ghost)
        {
            if(cellVisuals.TryGetValue(c,out var previous)){var joined=new List<GameObject>(previous){o,edge};cellVisuals[c]=joined.ToArray();}
            else cellVisuals[c]=new[]{o,edge};
        }
        if(highlight){anchorRenderer=o.GetComponent<MeshRenderer>();anchorEdge=edge.GetComponent<MeshRenderer>();}
    }
    GameObject MakeMesh(Mesh mesh,Material mat,Vector3 position,Vector3 size,Color tint)
    {
        var go=new GameObject("Ink Block");go.layer=30;go.transform.SetParent(visuals);go.transform.position=position;go.transform.localScale=size;
        go.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=mat;
        var props=new MaterialPropertyBlock();props.SetColor("_Color",tint);renderer.SetPropertyBlock(props);return go;
    }
    static void Clear(List<GameObject> list){foreach(var o in list){o.SetActive(false);Destroy(o);}list.Clear();}
    static Mesh BuildCube()
    {
        var v=new List<Vector3>();var colors=new List<Color>();var triangles=new List<int>();
        Vector3[] corners={new Vector3(-.5f,-.5f,-.5f),new Vector3(.5f,-.5f,-.5f),new Vector3(.5f,.5f,-.5f),new Vector3(-.5f,.5f,-.5f),new Vector3(-.5f,-.5f,.5f),new Vector3(.5f,-.5f,.5f),new Vector3(.5f,.5f,.5f),new Vector3(-.5f,.5f,.5f)};
        int[][] faces={new[]{0,3,2,1},new[]{5,6,7,4},new[]{4,7,3,0},new[]{1,2,6,5},new[]{3,7,6,2},new[]{4,0,1,5}};
        float[] shades={.84f,.74f,.79f,.66f,1,.58f};
        for(int f=0;f<6;f++){int n=v.Count;foreach(int c in faces[f]){v.Add(corners[c]);colors.Add(new Color(shades[f],shades[f],shades[f]));}triangles.AddRange(new[]{n,n+1,n+2,n,n+2,n+3});}
        var m=new Mesh();m.SetVertices(v);m.SetColors(colors);m.SetTriangles(triangles,0);m.RecalculateNormals();m.RecalculateBounds();return m;
    }
    static Mesh BuildEdges()
    {
        var vertices=new List<Vector3>();var indices=new List<int>();
        for(int axis=0;axis<3;axis++)for(int a=-1;a<=1;a+=2)for(int b=-1;b<=1;b+=2)
        {Vector3 p=axis==0?new Vector3(-.5f,a*.5f,b*.5f):axis==1?new Vector3(a*.5f,-.5f,b*.5f):new Vector3(a*.5f,b*.5f,-.5f);Vector3 q=p;q[axis]=.5f;indices.Add(vertices.Count);vertices.Add(p);indices.Add(vertices.Count);vertices.Add(q);}
        var m=new Mesh();m.SetVertices(vertices);m.SetIndices(indices.ToArray(),MeshTopology.Lines,0);return m;
    }

    void Styles()
    {
        if(text!=null)return;
        text=new GUIStyle(GUI.skin.label){font=korean,fontSize=17,normal={textColor=Ink},alignment=TextAnchor.MiddleLeft};
        small=new GUIStyle(text){fontSize=13};heading=new GUIStyle(text){fontSize=20,fontStyle=FontStyle.Bold};
        title=new GUIStyle(text){fontSize=32,fontStyle=FontStyle.Bold};number=new GUIStyle(title){alignment=TextAnchor.MiddleRight};
        button=new GUIStyle(text){alignment=TextAnchor.MiddleCenter,fontSize=16};
    }
    void Fill(Rect r,Color c){Color prev=GUI.color;GUI.color=c;GUI.DrawTexture(r,Texture2D.whiteTexture);GUI.color=prev;}
    void Border(Rect r,Color c){Fill(new Rect(r.x,r.y,r.width,1),c);Fill(new Rect(r.x,r.yMax-1,r.width,1),c);Fill(new Rect(r.x,r.y,1,r.height),c);Fill(new Rect(r.xMax-1,r.y,1,r.height),c);}
    void Label(Rect r,string s,GUIStyle style=null){GUI.Label(r,s,style??text);}
    bool Button(Rect r,string s,bool dark=false,bool enabled=true)
    {
        bool hover=r.Contains(Event.current.mousePosition);Fill(r,dark?Ink:hover?new Color(.88f,.88f,.86f):Color.white);Border(r,enabled?Ink:new Color(.75f,.75f,.73f));
        Color old=button.normal.textColor;button.normal.textColor=dark?Color.white:enabled?Ink:Color.gray;
        bool before=GUI.enabled;GUI.enabled=before&&enabled;bool hit=GUI.Button(r,s,button);GUI.enabled=before;button.normal.textColor=old;return hit;
    }
    void Icon(Rect r,int action,bool selected)
    {
        var shape=Shapes[action];int w=0,h=0;foreach(var c in shape){w=Math.Max(w,c.z+1);h=Math.Max(h,c.y+1);}float size=Mathf.Min(18,r.width/w);
        float ox=r.center.x-w*size/2,oy=r.center.y-h*size/2;
        foreach(var c in shape)
        {
            var cell=new Rect(ox+c.z*size,oy+c.y*size,size-2,size-2);Color fill=BlockColors[action],outline=selected?Color.white:Ink;
            if(action==3&&mode==PlayMode.Engineering&&EngineeringAvailable==0){fill.a=.25f;outline.a=.25f;}
            Fill(cell,fill);Border(cell,outline);
        }
    }
    void Stat(float y,string name,string value,float fraction)
    {Label(new Rect(52,y,190,23),name,small);Label(new Rect(172,y,90,23),value,new GUIStyle(small){alignment=TextAnchor.MiddleRight});Fill(new Rect(52,y+29,210,4),new Color(.85f,.85f,.83f));Fill(new Rect(52,y+29,210*Mathf.Clamp01(fraction),4),Ink);}
    void OnGUI()
    {
        if(boardCamera==null)return;Styles();
        GUI.matrix=Matrix4x4.TRS(new Vector3(offset.x,offset.y,0),Quaternion.identity,Vector3.one*scale);
        bool previousEnabled=GUI.enabled;GUI.enabled=previousEnabled&&!medicationWarning&&!badEnding&&!RouteIsOver;
        // Only draw the surrounding frame so the central live 3D camera remains visible.
        Color paper=new Color(.965f,.965f,.965f);
        Fill(new Rect(0,0,1440,250),paper);Fill(new Rect(0,250,325,650),paper);Fill(new Rect(1225,250,215,650),paper);Fill(new Rect(325,760,900,140),paper);
        Label(new Rect(40,24,380,48),"BLOCK XX",title);Label(new Rect(41,72,300,24),"이브의 일주일을 만들어주세요",small);
        Label(new Rect(1180,35,220,30),"WEEK  "+week.ToString("00"),heading);
        Fill(new Rect(40,112,1360,1),new Color(.76f,.76f,.73f));
        // Eve and the preview readouts.
        Fill(new Rect(40,142,235,306),Color.white);Border(new Rect(40,142,235,306),new Color(.76f,.76f,.73f));
        if(portrait!=null)
        {
            Rect frame=new Rect(49,151,217,257);
            GUI.DrawTexture(frame,portrait,ScaleMode.ScaleToFit);
        }
        Label(new Rect(57,409,190,29),"E V E   /   이브",heading);
        GUI.enabled=previousEnabled&&!resolving;
        if(Button(new Rect(40,454,235,38),"모드: "+ModeNames[(int)mode]+"  >",true,!resolving))ChangeMode((PlayMode)(((int)mode+1)%5));
        GUI.enabled=previousEnabled&&!medicationWarning&&!badEnding&&!RouteIsOver;
        Label(new Rect(52,496,225,24),RouteEnabled?week+" / 4주 · "+attempt+"번째 도전":"기본 모드 · 자유 배치",small);
        var restSummary=CurrentRest();
        float load=restSummary.rawLoad,rest=restSummary.recoveryCapacity,net=RemovalBudget(restSummary.remainingLoad,0),empty=restSummary.emptiness;
        Label(new Rect(51,523,200,24),started?(resolving?"부하 처리 중":"이번 주 결과"):"이번 배치 예상",small);
        float displayedLoad=started?(resolving?Mathf.Max(0,executedNet-removedCells):0):net;
        Stat(551,"부하",displayedLoad.ToString("0.##"),displayedLoad/20);
        for(int action=0;action<4;action++)Stat(ScoreRowY(action),ScoreNames[action],ScoreText(action),DisplayScore(action)/ScoreTarget(action));
        DrawRouteProgress();
        float displayedVoid=RouteEnabled?routeVoid+(started&&!resolving?0:started?executedVoid:empty):started?executedVoid:empty;
        Stat(773,"공허함",displayedVoid.ToString("0.##"),displayedVoid/20);
        Label(new Rect(52,817,215,32),started?"발생 "+executedLoad.ToString("0.##")+" / 상쇄량 "+executedRest.ToString("0.##")+"\n제거된 블록 칸 "+removedCells:"쌓인 부하 "+load.ToString("0.##")+" / 상쇄량 "+rest.ToString("0.##"),small);
        // Basic action tray, separate everyday tray.
        Label(new Rect(325,132,400,24),"일정 블록",small);
        Label(new Rect(465,132,650,24),AwarenessSummary(),small);
        for(int a=0;a<5;a++)
        {
            Rect r=new Rect(325+a*164,166,152,80);bool sel=held==a;
            if(Button(r,"",sel,ActionEnabled(a)))Choose(a);
            Color originalColor=GUI.color;
            if(a==3&&mode==PlayMode.Engineering&&EngineeringAvailable==0)GUI.color=new Color(1,1,1,.25f);
            Icon(new Rect(r.x+8,r.y+4,136,42),a,sel);
            GUI.color=originalColor;
            var labelStyle=new GUIStyle(small){alignment=TextAnchor.MiddleCenter,normal={textColor=sel?Color.white:Ink}};
            Label(new Rect(r.x,r.y+47,r.width,24),Names[a],labelStyle);
            if(a==3&&mode==PlayMode.Engineering)
            {
                Fill(new Rect(r.xMax-34,r.y+4,29,24),Color.white);Border(new Rect(r.xMax-34,r.y+4,29,24),Ink);
                Label(new Rect(r.xMax-34,r.y+4,29,24),EngineeringAvailable.ToString(),new GUIStyle(small){alignment=TextAnchor.MiddleCenter});
            }
        }
        Label(new Rect(1175,132,220,24),"생활 블록",small);
        if(Button(new Rect(1270,127,125,30),medicationEnabled?"약 사용: 켜짐":"약 사용: 꺼짐",false,!started&&!resolving&&!RouteIsOver))ToggleMedication();
        for(int a=5;a<7;a++)
        {
            Rect r=new Rect(1175+(a-5)*112,166,104,80);bool sel=held==a;
            if(Button(r,"",sel,ActionEnabled(a)))Choose(a);
            Icon(new Rect(r.x+7,r.y+4,90,42),a,sel);
            Label(new Rect(r.x,r.y+47,r.width,24),Names[a],new GUIStyle(small){alignment=TextAnchor.MiddleCenter,normal={textColor=sel?Color.white:Ink}});
        }
        Label(new Rect(340,265,870,25),RouteEnabled?ModeNames[(int)mode]+" · "+TargetDescription+"  /  "+RouteHelp:"WEEKLY SCHEDULE   /   "+DayCount+" DAYS × "+SlotsPerDay+" SLOTS",small);
        // Projected labels follow the zoomed board.
        for(int d=0;d<DayCount;d++)WorldLabel(new Vector3(d,.08f,SlotsPerDay+.15f),days[d]);
        for(int s=0;s<SlotsPerDay;s++)WorldLabel(new Vector3(-.9f,.08f,s),(SlotsPerDay-s).ToString("00"));
        Label(new Rect(1245,294,165,26),"블록 회전",heading);
        Label(new Rect(1245,330,160,23),"좌우  ·  Q / E",small);
        if(Button(new Rect(1245,360,70,48),"↶",false,held>=0))Rotate(true,-1);
        if(Button(new Rect(1325,360,70,48),"↷",false,held>=0))Rotate(true,1);
        Label(new Rect(1245,432,160,23),"앞뒤  ·  R",small);
        if(Button(new Rect(1245,462,70,48),"앞",false,held>=0))Rotate(false,1);
        if(Button(new Rect(1325,462,70,48),"뒤",false,held>=0))Rotate(false,-1);
        if(Button(new Rect(1245,534,150,42),"대칭  ·  F",false,held>=0))Mirror();
        Label(new Rect(1245,612,170,24),"깊이  "+depth+"층",heading);
        Label(new Rect(1245,646,175,62),"Shift + 마우스 위아래\n휠로 확대 / 축소\nWASD로 시점 회전",new GUIStyle(small){wordWrap=true});
        if(Button(new Rect(1245,716,150,38),"시점 초기화")){zoom=DefaultZoom;cameraYaw=30;cameraElevation=40;}
        Label(new Rect(340,770,865,27),started?(resolving?"일정 실행 중 · 겹친 블록의 부하를 처리하고 있어요":"이번 주 완료 · 제거된 칸 "+removedCells+"개"):held>=0?Names[held]+"  /  "+(hasTarget?TargetLocation():"배치할 위치를 선택해주세요"):"드래그로 보드 회전 · 블록을 클릭하면 집어 들어요.",heading);
        Label(new Rect(340,807,865,24),held>=0&&!valid?invalidReason:message,small);
        Label(new Rect(340,843,670,24),"Shift + 이동: 깊이 조절     휠: 확대/축소     보드 밖 클릭 · Esc: 목록으로 반환",small);
        Label(new Rect(1245,775,150,23),Coverage()+" / "+BoardCellCount+" 시간",small);
        bool canStart=!RouteIsOver&&!resolving&&(started?MayAdvanceWeek:(held<0&&Coverage()==BoardCellCount&&StartRulesReady(out _)&&(!NeedsCleaning||ValidCount(6)>0)));
        if(Button(new Rect(1245,810,150,48),resolving?"실행 중…":started?"다음 주차":"행동 시작!",true,canStart)) {if(started)Reset(true);else BeginWeek();}
        if(!started&&InvalidCount()>0)Label(new Rect(1245,859,170,22),"미확정 블록을 수정해주세요",small);
        else if(!started&&Coverage()<BoardCellCount)Label(new Rect(1245,859,170,22),"빈 시간대를 모두 채워주세요",small);
        else if(!started&&NeedsCleaning&&PlacedCount(6)==0)Label(new Rect(1245,859,170,22),"필수 청소를 배치해주세요",small);
        else if(started&&mode==PlayMode.Reading&&!weekCleaningDone)Label(new Rect(1245,859,170,22),"이번 주 청소가 필요해요",small);
        if(Button(new Rect(40,850,235,30),"현재 주차 비우기",false,!started&&!resolving))Reset(false);
        GUI.enabled=previousEnabled;
        DrawRouteOutcome();
        if(!medicationWarning&&!badEnding&&!RouteIsOver){MedicationTooltip();DrawRouteProgressTooltip();}
        if(medicationWarning)
        {
            Fill(new Rect(0,0,1440,900),new Color(0,0,0,.65f));Fill(new Rect(440,305,560,270),paper);Border(new Rect(440,305,560,270),Ink);
            Label(new Rect(480,335,480,40),"이대로 진행할까요?",heading);
            Label(new Rect(480,390,480,65),"하루에 약을 3개 이상 배치했어요.\n무시하고 진행하면 엔딩으로 이어집니다.",text);
            if(Button(new Rect(480,490,230,46),"무시하고 진행",true))ProceedMedicationEnding();
            if(Button(new Rect(730,490,230,46),"다시 배치"))DismissMedicationWarning();
        }
        if(badEnding)
        {
            Fill(new Rect(0,0,1440,900),new Color(.04f,.04f,.04f));
            Label(new Rect(170,360,1100,140),"그게 너의 선택이라면, 어쩔 수 없으려나.",new GUIStyle(title){fontSize=30,alignment=TextAnchor.MiddleCenter,normal={textColor=Color.white}});
            Label(new Rect(420,540,600,32),"잠시 후 게임이 종료됩니다.",new GUIStyle(small){alignment=TextAnchor.MiddleCenter,normal={textColor=Color.gray}});
            if(Button(new Rect(620,600,200,44),"종료"))ExitGame();
        }
        Event e=Event.current;
        if(boardDrag.Active&&e.button==0&&!badEnding&&!medicationWarning&&!RouteIsOver)
        {
            if(e.type==EventType.MouseDrag)
            {
                if(held<0)OrbitByDrag(boardDrag.Move(e.mousePosition));else boardDrag.Cancel();
                e.Use();
            }
            else if(e.type==EventType.MouseUp)
            {
                if(held<0)OrbitByDrag(boardDrag.Move(e.mousePosition));
                bool click=boardDrag.End();
                if(click&&held<0&&boardRect.Contains(e.mousePosition))ClickBoard();
                e.Use();
            }
        }
        if(e.type==EventType.MouseDown&&e.button==0&&!badEnding&&!medicationWarning&&!RouteIsOver)
        {
            if(boardRect.Contains(e.mousePosition))
            {
                if(held>=0)ClickBoard();else boardDrag.Begin(e.mousePosition);
                e.Use();
            }
            else if(held>=0){ReturnHeld();e.Use();}
        }
    }
    string TargetLocation()
    {
        if(day<0||day>=DayCount||slot<0||slot>=SlotsPerDay||depth<0)return "보드 범위를 벗어난 위치";
        return days[day]+"요일 · "+(SlotsPerDay-slot)+"번 시간 · 깊이 "+depth;
    }
    void MedicationTooltip()
    {
        Vector2 mouse=Event.current.mousePosition;
        bool hovered=new Rect(1175,166,104,80).Contains(mouse);
        if(boardRect.Contains(mouse))
        {
            Piece p=PieceAtPointer(out _);
            hovered|=(p!=null&&p.action==5)||(held==5&&hasTarget);
        }
        if(!hovered)return;
        Rect bubble=new Rect(Mathf.Clamp(mouse.x+18,12,1098),Mathf.Clamp(mouse.y+22,12,800),330,88);
        Fill(new Rect(bubble.x+4,bubble.y+4,bubble.width,bubble.height),new Color(0,0,0,.12f));
        Fill(bubble,Color.white);Border(bubble,Ink);
        Label(new Rect(bubble.x+16,bubble.y+12,bubble.width-32,bubble.height-24),"이브를 도와주는 약.\n하루에 두 개까진 괜찮아.",text);
    }
    void WorldLabel(Vector3 world,string value,Color? color=null)
    {
        Vector3 p=boardCamera.WorldToScreenPoint(world);Vector2 gui=(new Vector2(p.x,Screen.height-p.y)-offset)/scale;
        if(!boardRect.Contains(gui))return;
        Label(new Rect(gui.x-28,gui.y-12,56,24),value,new GUIStyle(small){alignment=TextAnchor.MiddleCenter,fontStyle=FontStyle.Bold,normal={textColor=color??Ink}});
    }
}
