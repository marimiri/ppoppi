using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed partial class BlockXXPrototype
{
    Piece CheckPiece(int action,params Vector3Int[] cells)
    {var p=new Piece{action=action,id=nextId++,cells=cells};pieces.Add(p);return p;}
    void CheckQuotaFixture(int music,int others)
    {
        pieces.Clear();
        for(int i=0;i<music+others;i++)CheckPiece(i<music?2:0,new Vector3Int(i/SlotsPerDay,0,i%SlotsPerDay));
    }
    IEnumerator CheckRouteModes()
    {
        // Minimal geometry below isolates classification and arithmetic. Full-shaped,
        // complete-board fixtures later exercise the actual start/settlement path.
        foreach(PlayMode candidateMode in new[]{PlayMode.Plant,PlayMode.Music,PlayMode.Reading,PlayMode.Engineering})
        {
            ChangeMode(candidateMode);
            int expected=candidateMode==PlayMode.Music?300:candidateMode==PlayMode.Engineering?160:200;
            Verify(ScoreTarget(MainAction)==expected,"each route must use its own main target");
            for(int a=0;a<4;a++){Verify(ScoreTarget(a)==(a==MainAction?expected:50),"secondary targets must all be fifty");routeScores[a]=ScoreTarget(a);}
            Verify(RouteGoalsMet(),"meeting all four targets must succeed");
            for(int a=0;a<4;a++){routeScores[a]-=.01f;Verify(!RouteGoalsMet(),"every target is required");routeScores[a]+=.01f;}
        }
        ChangeMode(PlayMode.Basic);
        var cross=Turn(Shapes[2],true,1);for(int i=0;i<cross.Length;i++)cross[i]+=new Vector3Int(0,1,0);
        var hidden=CheckPiece(2,cross);
        Verify(Classify(pieces)[hidden]==Awareness.Invalid&&Gain(2)==0,"visible cross-day pieces are provisional and excluded from score");
        for(int d=0;d<4;d++)CheckPiece(0,new Vector3Int(d,0,0));
        Verify(Classify(pieces)[hidden]==Awareness.Unconscious&&Gain(2)==2,"fully hidden cross-day I must earn half volume without task attenuation");
        Verify(WeightedLoad(pieces,Classify(pieces))==2,"four hidden cells must add half load each");
        Verify(Gain(0)==2,"visible caps must still receive their normal two-kind penalty");
        cameraYaw+=130;cameraElevation+=170;Resize();
        Verify(Classify(pieces)[hidden]==Awareness.Unconscious,"orbiting must not change awareness");
        cameraYaw=30;cameraElevation=40;Resize();
        CaptureRouteSchedule();started=true;pieces.RemoveAt(pieces.Count-1);
        Verify(Gain(2)==2,"settlement must retain awareness captured at action start");
        Reset(false);
        Seed(0,0,0);var floating=pieces[0];for(int i=0;i<floating.cells.Length;i++)floating.cells[i]+=new Vector3Int(0,5,0);
        Verify(Classify(pieces)[floating]==Awareness.Preconscious&&WeightedLoad(pieces,Classify(pieces))==1,"floating T must exempt its nearest three caps and leave one load");
        foreach(int action in new[]{4,5,6})
        {
            Reset(false);var life=CheckPiece(action,new Vector3Int(0,1,0));CheckPiece(0,new Vector3Int(0,0,0));
            Verify(Classify(pieces)[life]==Awareness.Invalid&&!StartRulesReady(out _),"hidden life/rest blocks must block action start");
            Verify(CurrentRest().recoveryCapacity==0,"invalid rest must not supply recovery");
        }
        ChangeMode(PlayMode.Music);pieces.Clear();
        var buried=CheckPiece(0,new Vector3Int(0,1,0));CheckPiece(2,new Vector3Int(0,0,0));
        Verify(Classify(pieces,false)[buried]==Awareness.Invalid&&Gain(0)==0&&!StartRulesReady(out _),"music must prohibit unconscious activity");

        ChangeMode(PlayMode.Plant);
        Seed(0,0,1);Seed(0,0,3);var second=pieces[1];for(int i=0;i<second.cells.Length;i++)second.cells[i]+=new Vector3Int(0,1,0);
        Verify(Gain(0)==8&&PlantDailyCounts()[0]==2,"same-kind overlap must retain full score and focus");
        Seed(3,0,0);Verify(PlantDailyCounts()[0]==1,"one pure plant must suffice even if another is mixed");
        Reset(false);var unconsciousPlant=CheckPiece(0,new Vector3Int(0,1,0));CheckPiece(2,new Vector3Int(0,0,0));
        Verify(Gain(0)==.5f&&PlantFocusedDays()==0,"unconscious plant must not satisfy the daily requirement");
        ChangeMode(PlayMode.Plant);for(int d=0;d<7;d++)Seed(0,d,0);
        routeScores[0]=190;for(int a=1;a<4;a++)routeScores[a]=50;
        FixtureSettle();Verify(routeOutcome==RouteOutcome.Success,"a valid daily plant schedule and all targets must succeed");
        ChangeMode(PlayMode.Plant);routeVoid=3;routeScore=100;FixtureSettle();RetryRoute();
        Verify(routeScore==0&&routeVoid==3&&week==1,"plant retry resets only its main score and keeps void");

        ChangeMode(PlayMode.Engineering);
        Verify(EngineeringAvailable==5&&ExpectedEngineeringSupply()==5,"engineering must start with five blocks");
        Choose(3);Verify(EngineeringAvailable==4,"holding must reserve one communication block");ReturnHeld();
        for(int d=0;d<5;d++)Seed(3,d,0);
        Verify(ExpectedEngineeringSupply()==10&&EngineeringAvailable==0&&!ActionEnabled(3),"five pure Ls must exhaust supply and preview ten next week");
        Choose(3);Verify(held==-1,"exhausted inventory cannot be selected");
        for(int d=0;d<6;d++)Seed(0,d,5);
        FixtureSettle();Verify(engineeringSupply==10&&routeOutcome==RouteOutcome.None,"pure completed L volume must increase next supply");
        Verify(ExpectedEngineeringSupply()==10,"settled stock must not double count rewards");
        ChangeMode(PlayMode.Engineering);Seed(3,0,0);var comm=pieces[0];
        foreach(var c in comm.cells)CheckPiece(0,new Vector3Int(c.x,0,c.z));
        // Shift the complete L below the caps; even a same-kind hidden L has no focus bonus.
        for(int i=0;i<comm.cells.Length;i++)comm.cells[i]+=new Vector3Int(0,1,0);
        Verify(Classify(pieces)[comm]==Awareness.Unconscious&&ExpectedEngineeringSupply()==5,"unconscious communication must not earn focus stock rewards");
        ChangeMode(PlayMode.Engineering);for(int d=0;d<5;d++)Seed(0,d,0);FixtureSettle();
        Verify(routeOutcome==RouteOutcome.Bad&&!routeReason.Contains("5")&&!RouteHelp.Contains("6회"),"hidden engineering ending must stay hidden in UI");
        ChangeMode(PlayMode.Engineering);routeScore=60;completedTurns=3;for(int d=0;d<6;d++)Seed(0,d,0);FixtureSettle();RetryRoute();
        Verify(engineeringSupply==5&&routeScore==30,"engineering retry must halve main score and restore five initial blocks");

        foreach(var quota in new[]{new[]{24,10},new[]{25,9},new[]{25,10}})
        {
            ChangeMode(PlayMode.Music);CheckQuotaFixture(quota[0],quota[1]);
            for(int a=0;a<4;a++)routeScores[a]=ScoreTarget(a);
            FixtureSettle();Verify(routeOutcome==(quota[0]>=25&&quota[1]>=10?RouteOutcome.Success:RouteOutcome.Failed),"weekly music quota boundaries must be 25/10");
        }
        ChangeMode(PlayMode.Basic);
        for(int d=0;d<3;d++)Seed(4,d,0);
        Verify(!ActionEnabled(4),"three rests must disable the tray");Choose(4);Verify(held==-1,"fourth rest cannot be selected");
        pieces.RemoveAt(0);Verify(ActionEnabled(4),"returning rest must restore one available slot");
        medicationEnabled=true;Choose(5);ToggleMedication();Verify(!medicationEnabled&&held==-1&&!ActionEnabled(5),"disabling pills returns held pill and disables card");
        Seed(5,6,9);Verify(!StartRulesReady(out _),"placed pills must block start while medication is disabled");ToggleMedication();Verify(ActionEnabled(5),"pills must be available after enabling");

        ChangeMode(PlayMode.Reading);pieces.Clear();
        for(int i=0;i<30;i++)
        {var c=new Vector3Int(i/SlotsPerDay,0,i%SlotsPerDay);CheckPiece(2,c);var book=CheckPiece(1,c);book.sharedVolume=true;}
        Choose(0);Verify(Validate(new[]{new Vector3Int(4,0,0)},out _),"thirty shared cells must be allowed");ReturnHeld();
        Seed(2,3,0);Choose(1);Verify(!Validate(new[]{new Vector3Int(3,0,0)},out _),"thirty-first shared cell must be rejected");ReturnHeld();
        Verify(SharedBookCells(pieces)==30&&Gain(1)==15,"shared visible books must retain task attenuation and count volume once");
        ChangeMode(PlayMode.Reading);
        for(int d=0;d<DayCount;d++){Seed(2,d,0);Seed(2,d,4);Seed(5,d,8);Seed(5,d,9);}Rebuild();
        BeginWeek();Verify(!started,"reading cannot start without cleaning");Reset(true);Verify(week==1,"reading cannot advance without cleaning");
        pieces.Clear();Seed(0,0,0);Seed(6,0,3);Seed(2,0,6);
        for(int d=1;d<DayCount;d++){Seed(2,d,0);Seed(2,d,4);Seed(5,d,8);Seed(5,d,9);}Rebuild();
        BeginWeek();while(resolving)yield return null;
        Verify(weekCleaningDone&&MayAdvanceWeek&&completedTurns==1,"actual cleaning must unlock the next reading week");Reset(true);
        Verify(week==2&&!weekCleaningDone&&!MayAdvanceWeek,"cleaning credit must not carry into the next week");

        ChangeMode(PlayMode.Basic);
        for(int d=0;d<DayCount;d++){Seed(6,d,0);Seed(6,d,3);Seed(6,d,6);Seed(5,d,9);}
        var under=Turn(Shapes[2],true,1);for(int i=0;i<under.Length;i++)under[i]+=new Vector3Int(0,1,9);
        var underPiece=CheckPiece(2,under);Rebuild();
        Verify(Coverage()==BoardCellCount&&InvalidCount()==0&&Gain(2)==2&&CurrentRest().rawLoad==2,"complete real-mino schedule must recognize hidden cross-day activity");
        BeginWeek();while(resolving)yield return null;
        Verify(started&&removedCells==2&&underPiece.cells.Length==2&&Gain(2)==1&&executedVoid==2,"actual settlement must freeze awareness and remove exactly floor residual load cells");
        Verify(RemovalBudget(.5f,0)==0&&RemovalBudget(2.9f,0)==2&&RemovalBudget(3.5f,2)==1,"fractional remaining load must be discarded only after recovery");
        ChangeMode(PlayMode.Basic);
        for(int d=0;d<DayCount;d++){Seed(6,d,0);Seed(6,d,3);Seed(6,d,6);Seed(5,d,9);}
        var unresolved=Turn(Shapes[2],true,1);for(int i=0;i<unresolved.Length;i++)unresolved[i]+=new Vector3Int(0,1,9);
        CheckPiece(2,unresolved);pieces.RemoveAll(p=>p.action==5&&p.cells[0].x==3);
        // Removing the cap on its last day exposes part of the cross-day I.
        BeginWeek();Verify(!started&&InvalidCount()==1,"one unresolved cross-day piece must block an otherwise complete week");
        ChangeMode(PlayMode.Basic);
        Debug.Log("BLOCKXX_AWARENESS_CHECKS_PASSED: provisional/frozen classification, cross-day hidden minos, half gain/load, music/life bans, focus/stock, floor residual load, medication setting and rest cap");
        Debug.Log("BLOCKXX_ROUTE_CHECKS_PASSED: route goals 200/200/300/160 and side 50, music 25/10, engineering five, reading shared-30/cleaning, retry and hidden ending");
    }
}
