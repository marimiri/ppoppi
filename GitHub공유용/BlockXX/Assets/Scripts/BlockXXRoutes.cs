using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed partial class BlockXXPrototype
{
    public enum PlayMode { Basic, Plant, Music, Reading, Engineering }
    enum RouteOutcome { None, Success, Failed, Bad }
    static readonly string[] ModeNames={"기본","식물루트","음악루트","독서루트","공학루트"};
    PlayMode mode;
    RouteOutcome routeOutcome;
    readonly float[] routeScores=new float[4];
    float routeScore {get=>routeScores[MainAction];set=>routeScores[MainAction]=value;}
    float routeVoid;
    bool weekCleaningDone;
    int completedTurns,engineeringSupply=InitialEngineeringSupply,attempt=1;
    string routeReason="";
    Dictionary<Vector2Int,HashSet<int>> scheduledKinds;
    readonly HashSet<int> purePieces=new HashSet<int>();
    readonly int[] scheduledCounts=new int[9];
    bool RouteEnabled=>mode!=PlayMode.Basic;
    bool RouteIsOver=>RouteEnabled&&routeOutcome!=RouteOutcome.None;
    bool NeedsCleaning=>RouteEnabled&&(mode==PlayMode.Reading||week==2);
    int MainAction=>mode==PlayMode.Plant?0:mode==PlayMode.Music?2:mode==PlayMode.Reading?1:3;
    float LoadFactor=>mode==PlayMode.Plant?.5f:mode==PlayMode.Reading?1.2f:mode==PlayMode.Music?0:1;
    float VoidFactor=>mode==PlayMode.Plant?.5f:1;
    int PlacedCount(int action){int count=0;foreach(var p in pieces)if(p.action==action)count++;return count;}
    int EngineeringAvailable=>started&&!resolving?engineeringSupply:Math.Max(0,engineeringSupply-PlacedCount(3)-(held==3?1:0));

    void ChangeMode(PlayMode next)
    {
        mode=next;routeOutcome=RouteOutcome.None;routeReason="";
        Array.Clear(routeScores,0,routeScores.Length);routeVoid=0;weekCleaningDone=false;completedTurns=0;engineeringSupply=InitialEngineeringSupply;attempt=1;week=1;nextId=1;
        scheduledKinds=null;purePieces.Clear();Array.Clear(scheduledCounts,0,scheduledCounts.Length);
        Reset(false);message=ModeNames[(int)mode]+" 모드 · "+(RouteEnabled?"4주 안에 "+TargetDescription+"을 달성하세요.":"자유롭게 일정을 배치하세요.");
    }
    void RetryRoute()
    {
        if(routeOutcome!=RouteOutcome.Failed)return;
        routeScore=mode==PlayMode.Plant?0:Round2(routeScore*.5f);
        completedTurns=0;engineeringSupply=InitialEngineeringSupply;week=1;attempt++;
        routeOutcome=RouteOutcome.None;routeReason="";Reset(false);
        message="재도전 · 지수 "+routeScore.ToString("0.##")+" / 공허함 "+routeVoid.ToString("0.##")+"에서 시작해요.";
    }
    static float Round2(float value)=>(float)Math.Round(value,2,MidpointRounding.AwayFromZero);

    void PrepareRouteWeek()
    {
        weekCleaningDone=false;
        scheduledKinds=null;purePieces.Clear();Array.Clear(scheduledCounts,0,scheduledCounts.Length);
        if(!suppressRandomDreams)GenerateRandomDreams();
        if(mode==PlayMode.Music)GenerateMusicEvents();
    }
    void GenerateMusicEvents()
    {
        var reserved=new HashSet<Vector2Int>();foreach(var p in pieces)foreach(var c in p.cells)reserved.Add(new Vector2Int(c.x,c.z));
        var candidates=new List<Vector2Int>();
        for(int d=0;d<DayCount-1;d++)for(int s=0;s<SlotsPerDay;s++)candidates.Add(new Vector2Int(d,s));
        var random=new System.Random(unchecked(attempt*911+week*7919+Environment.TickCount));
        for(int i=candidates.Count-1;i>0;i--){int j=random.Next(i+1);var swap=candidates[i];candidates[i]=candidates[j];candidates[j]=swap;}
        string[] events={"토끼 조우?","악기 청소","시 쓰기"};int count=0;
        foreach(var start in candidates)
        {
            var end=start+Vector2Int.right;
            if(reserved.Contains(start)||reserved.Contains(end))continue;
            reserved.Add(start);reserved.Add(end);
            pieces.Add(new Piece{action=8,id=nextId++,fixedPlacement=true,label=events[random.Next(events.Length)],cells=new[]{new Vector3Int(start.x,0,start.y),new Vector3Int(end.x,0,end.y)}});
            if(++count==4)break;
        }
        if(count!=4)throw new InvalidOperationException("Could not place four music events without overlap.");
    }
    bool CanChooseAction(int action)
    {
        if(action==5&&!medicationEnabled){message="약 사용 설정이 꺼져 있어요.";return false;}
        if(action==4&&PlacedCount(4)>=RestLimit&&held!=4){message="아무것도 안 하기는 주 3개까지예요.";return false;}
        if(!RouteEnabled)return true;
        if(action==3&&mode==PlayMode.Engineering&&EngineeringAvailable==0&&held!=3){message="이번 주 교신 블록을 모두 사용했어요.";return false;}
        if(action==6&&(!NeedsCleaning||PlacedCount(6)>0)){message=NeedsCleaning?"이번 주 청소 블록은 한 개예요.":"이번 주에는 청소 일정이 없어요.";return false;}
        return action<7;
    }
    bool ActionEnabled(int action)
    {
        if(started||badEnding||RouteIsOver)return false;
        if(action==5&&!medicationEnabled)return false;
        if(action==4&&PlacedCount(4)>=RestLimit&&held!=4)return false;
        if(!RouteEnabled)return true;
        if(action==3&&mode==PlayMode.Engineering)return EngineeringAvailable>0||held==3;
        if(action==6)return NeedsCleaning&&PlacedCount(6)==0;
        return true;
    }

    public static Dictionary<Vector2Int,HashSet<int>> ActivityKinds(IEnumerable<Piece> source)
    {
        var kinds=new Dictionary<Vector2Int,HashSet<int>>();
        foreach(var p in source)
        {
            if(p.action>=7)continue;
            foreach(var c in p.cells){var key=new Vector2Int(c.x,c.z);if(!kinds.TryGetValue(key,out var actions)){actions=new HashSet<int>();kinds[key]=actions;}actions.Add(p.action);}
        }
        return kinds;
    }
    static bool IsPure(Piece piece,Dictionary<Vector2Int,HashSet<int>> kinds)
    {
        foreach(var c in piece.cells)if(!kinds.TryGetValue(new Vector2Int(c.x,c.z),out var actions)||actions.Count!=1)return false;
        return true;
    }
    void CaptureRouteSchedule()
    {
        frozenAwareness=Classify(pieces,mode!=PlayMode.Music);
        scheduledKinds=ActivityKinds(CountedPieces(frozenAwareness));purePieces.Clear();Array.Clear(scheduledCounts,0,scheduledCounts.Length);
        foreach(var p in pieces)if(frozenAwareness[p]!=Awareness.Invalid){scheduledCounts[p.action]++;if(p.action<7&&FocusEligible(p,frozenAwareness,scheduledKinds))purePieces.Add(p.id);}
    }
    float RouteGain(int action)
    {
        var states=CurrentAwareness();var kinds=started&&scheduledKinds!=null?scheduledKinds:ActivityKinds(CountedPieces(states));float result=0;
        foreach(var p in pieces)if(p.action==action&&states.TryGetValue(p,out var state)&&state!=Awareness.Invalid)foreach(var c in p.cells)
            if(state==Awareness.Unconscious)result+=.5f;
            else if(kinds.TryGetValue(new Vector2Int(c.x,c.z),out var actions))result+=1f/actions.Count;
        return Round2(result);
    }
    float DisplayScore(int action)=>RouteEnabled?Round2(routeScores[action]+(started?0:Gain(action))):Gain(action);
    int ScoreTarget(int action)=>!RouteEnabled?100:action!=MainAction?50:mode==PlayMode.Music?300:mode==PlayMode.Engineering?160:200;
    string ScoreText(int action)=>DisplayScore(action).ToString("0.##")+(RouteEnabled?" / "+ScoreTarget(action):"");
    bool RouteGoalsMet()
    {for(int action=0;action<4;action++)if(routeScores[action]<ScoreTarget(action))return false;return true;}
    bool MayAdvanceWeek=>started&&!resolving&&!RouteIsOver&&(mode!=PlayMode.Reading||weekCleaningDone);

    void FinishRouteWeek()
    {
        if(!RouteEnabled)return;
        weekCleaningDone=scheduledCounts[6]>0&&PlacedCount(6)>0;
        if(mode==PlayMode.Reading&&!weekCleaningDone)
        {started=false;scheduledKinds=null;message="이번 주 청소를 수행하지 않았어요. 청소 1회를 배치한 뒤 행동을 시작해주세요.";return;}
        completedTurns++;
        for(int action=0;action<4;action++)routeScores[action]=Round2(routeScores[action]+Gain(action));
        routeVoid=Round2(routeVoid+executedVoid);
        if(routeVoid>=20){SetRouteOutcome(RouteOutcome.Bad,"공허함이 20에 도달했어요.");return;}
        if(mode==PlayMode.Plant)
        {
            for(int d=0;d<7;d++)
            {
                bool validDay=false;
                foreach(var p in pieces)if(p.action==0&&p.cells.Length>0&&p.cells[0].x==d&&purePieces.Contains(p.id)){validDay=true;break;}
                if(!validDay){SetRouteOutcome(RouteOutcome.Failed,"매일 멀티태스킹 없는 식물 키우기가 한 번씩 필요해요.");return;}
            }
        }
        if(mode==PlayMode.Music&&(scheduledCounts[2]<25||scheduledCounts[0]+scheduledCounts[1]+scheduledCounts[3]<10))
        {SetRouteOutcome(RouteOutcome.Failed,"매주 음악 25개 이상과 다른 기본 활동 총 10개 이상이 필요해요.");return;}
        if(mode==PlayMode.Engineering)
        {
            int other=0,cells=0;
            foreach(var p in pieces){if(p.action<3)other++;if(p.action==3&&purePieces.Contains(p.id))cells+=p.cells.Length;}
            if(other<=5){SetRouteOutcome(RouteOutcome.Bad,"예상치 못한 결말에 도달했어요.");return;}
            engineeringSupply+=cells/4;
        }
        if(RouteGoalsMet()){SetRouteOutcome(RouteOutcome.Success,TargetDescription+"을 달성했어요.");return;}
        if(completedTurns>=4){SetRouteOutcome(RouteOutcome.Failed,"4주 안에 "+TargetDescription+"을 채우지 못했어요.");return;}
        message+="  /  "+ModeNames[(int)mode]+" "+completedTurns+"주 완료 · 지수 "+routeScore.ToString("0.##");
    }
    void SetRouteOutcome(RouteOutcome outcome,string reason)
    {
        routeOutcome=outcome;routeReason=reason;boardDrag.Cancel();ReturnHeld();
        message=outcome==RouteOutcome.Success?"성공!":outcome==RouteOutcome.Bad?"배드엔딩!":"실패!";
    }
    void DrawRouteOutcome()
    {
        if(!RouteIsOver)return;
        Rect panel=new Rect(430,340,700,230);Fill(panel,Color.white);Border(panel,Ink);
        string result=routeOutcome==RouteOutcome.Success?"성공!":routeOutcome==RouteOutcome.Bad?"배드엔딩!":"실패!";
        Label(new Rect(465,360,630,52),result,title);
        Label(new Rect(465,414,630,48),routeReason,new GUIStyle(text){wordWrap=true});
        Label(new Rect(465,464,630,26),"지수 "+routeScore.ToString("0.##")+" / 공허함 "+routeVoid.ToString("0.##"),small);
        if(routeOutcome==RouteOutcome.Failed)
        {
            if(Button(new Rect(465,510,240,38),"다시하기",true))RetryRoute();
            Label(new Rect(725,506,365,46),mode==PlayMode.Plant?"식물 지수는 0, 공허함은 유지돼요.":"지수는 절반, 공허함은 유지돼요.",new GUIStyle(small){wordWrap=true});
        }
        else Label(new Rect(465,510,630,30),"왼쪽 모드 버튼으로 다른 루트를 체험할 수 있어요.",small);
    }
    string RouteHelp=>mode==PlayMode.Plant?"매일 순수 식물 활동 1회 · 부하/공허함 ½":mode==PlayMode.Music?"매주 음악 25개 + 다른 기본 활동 10개 · 부하 0":mode==PlayMode.Reading?"매주 청소 · 책 중첩 30칸까지 부하 면제 · 부하 ×1.2":"순수 교신으로 다음 주 지급량 증가";
}
