using System.Collections.Generic;
using UnityEngine;

public sealed partial class EveningPrototype
{
    static Rect PlanningCellRect(Vector2Int cell)
    {
        float size=placementBoard.width/EveningBoard.Size;
        return new Rect(placementBoard.x+cell.x*size,placementBoard.y+cell.y*size,size,size);
    }
    public static Color PlanningDensityColor(int layers)
    {
        // Equivalent to compositing the same translucent lavender once per layer.
        return Color.Lerp(Color.white,new Color(.50f,.39f,.68f),1-Mathf.Pow(.73f,Mathf.Max(0,layers)));
    }
    void DrawFlatPlacement()
    {
        var heights=board.Heights();
        for(int x=0;x<5;x++)for(int y=0;y<5;y++)
        {
            var cell=new Vector2Int(x,y);var rect=PlanningCellRect(cell);
            Fill(rect,PlanningDensityColor(heights[x,y]));
            Border(rect,new Color(.75f,.74f,.78f),1);
        }
        if(!result&&held>=0&&hasCell)
        {
            var cells=Candidate();bool valid=EveningBoard.ValidCells(cells);
            foreach(var cell in cells)if(EveningBoard.Inside(cell))
                Fill(PlanningCellRect(cell),valid?new Color(.55f,.39f,.76f,.28f):new Color(.86f,.25f,.23f,.24f));
            DrawFlatFootprint(cells,valid?new Color(.46f,.29f,.66f):new Color(.80f,.20f,.18f),3);
        }
        if(!result&&Stage!=null&&hintIndex>=0)
            DrawFlatFootprint(Stage.HintSteps[hintIndex].Cells(),new Color(.17f,.48f,.74f),3);
        Border(placementBoard,new Color(.53f,.51f,.59f),2);
    }
    void DrawFlatFootprint(Vector2Int[] cells,Color color,float width)
    {
        var set=new HashSet<Vector2Int>(cells);
        foreach(var cell in cells)
        {
            if(!EveningBoard.Inside(cell))continue;var r=PlanningCellRect(cell);
            if(!set.Contains(cell+Vector2Int.left))Fill(new Rect(r.x,r.y,width,r.height),color);
            if(!set.Contains(cell+Vector2Int.right))Fill(new Rect(r.xMax-width,r.y,width,r.height),color);
            if(!set.Contains(cell+Vector2Int.down))Fill(new Rect(r.x,r.y,r.width,width),color);
            if(!set.Contains(cell+Vector2Int.up))Fill(new Rect(r.x,r.yMax-width,r.width,width),color);
        }
    }
    void DrawStageNavigation()
    {
        Label(new Rect(36,794,1364,28),"스테이지 선택",small);
        for(int i=0;i<EveningStages.All.Length;i++)
        {
            string name=(i+1)+". "+EveningStages.All[i].Name+(clearedStages[i]?"  ✓":"");
            if(Button(new Rect(36+i*(1364f/EveningStages.All.Length),832,1364f/EveningStages.All.Length-8,44),name,!animating,stageIndex==i))SwitchStage(i);
        }
    }
    void DrawSplitInterface()
    {
        // Leave only the left preview camera's rectangle uncovered.
        Fill(new Rect(0,0,1440,170),Paper);Fill(new Rect(0,650,1440,250),Paper);
        Fill(new Rect(0,170,36,480),Paper);Fill(new Rect(596,170,844,480),Paper);
        Label(new Rect(36,25,320,60),"evening",title);
        Label(new Rect(650,35,750,36),Stage==null?"목표 준비 중":Stage.Name,heading);
        Label(new Rect(650,78,750,30),result?(animating?"모양을 확인하는 중…":Stage==null?"완성된 모양":stageAttemptPassed?"성공!":"다시 도전!"):"겹쳐 놓고, 모양을 완성하기",body);

        Label(new Rect(36,110,560,30),"입체 미리보기",heading);
        Label(new Rect(36,143,560,22),"회색은 목표 · 연보라는 현재 배치와 들고 있는 블록",small);
        Border(viewport,new Color(.75f,.74f,.78f));
        Label(new Rect(650,130,480,30),"배치판",heading);
        Label(new Rect(650,156,480,22),"위에서 본 모습 · 겹칠수록 진해져요",small);
        DrawFlatPlacement();

        Label(new Rect(1170,130,230,30),"블록",heading);
        for(int action=0;action<5;action++)
        {
            var rect=new Rect(1170,170+action*92,230,82);
            if(Button(rect,"",CanSelect(action),held==action))Select(action);
            Icon(new Rect(rect.x+10,rect.y+8,210,66),action);
        }

        if(Button(new Rect(36,666,174,34),"시점 초기화",!animating))ResetBoardCamera();
        if(Button(new Rect(222,666,174,34),showTarget?"목표 표시 중":"목표 숨김",Stage!=null))ToggleTarget();
        if(Button(new Rect(408,666,188,34),showPrediction?"예상 표시 중":"예상 숨김",!result))TogglePrediction();

        if(Button(new Rect(650,684,105,36),"왼쪽 회전",held>=0&&!result))Turn(-1);
        if(Button(new Rect(765,684,105,36),"오른쪽 회전",held>=0&&!result))Turn(1);
        if(Button(new Rect(880,684,105,36),"대칭",held>=0&&!result))orientation=EveningBoard.Mirror(orientation);
        if(Button(new Rect(995,684,135,36),"다음 힌트",Stage!=null&&!result))NextHint();
        Label(new Rect(650,728,480,30),result?"배치로 돌아가면 수정할 수 있어요.":"클릭: 배치 / 회수 · Q/E: 회전 · F: 대칭 · Esc: 반환",small);

        if(Button(new Rect(1170,650,230,48),result?"배치로 돌아가기":"행동 시작!",!animating&&(result||board.Pieces.Count>0&&held<0),true))
        {if(result)BackToPlan();else StartResult();}
        if(Button(new Rect(1170,710,110,36),"배치 취소",!result&&board.Pieces.Count>0))Undo();
        if(Button(new Rect(1290,710,110,36),"다시 하기",!animating))RestartStage();

        DrawStageNavigation();
    }
}
