using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed partial class EveningPrototype
{
    int stageIndex=-2,hintIndex=-1;
    bool stageEvaluated,stageAttemptPassed,showTarget=true;
    readonly bool[] clearedStages=new bool[EveningStages.All.Length];
    readonly List<GameObject> targetVisuals=new List<GameObject>();
    EveningStage Stage=>stageIndex>=0&&stageIndex<EveningStages.All.Length?EveningStages.All[stageIndex]:null;
    bool AllStagesCleared{get{foreach(bool cleared in clearedStages)if(!cleared)return false;return true;}}
    int ResultHeight=>Math.Max(board.MaxHeight,Stage==null?0:Stage.MaxHeight);
    bool CanSelect(int action)=>!result&&action>=0&&action<EveningBoard.Shapes.Length;
    void SwitchStage(int index)
    {
        if(animating||index==stageIndex)return;
        if(index<0||index>=EveningStages.All.Length)throw new ArgumentOutOfRangeException(nameof(index));
        if(result)BackToPlan();ReturnHeld();board.Clear();stageIndex=index;
        hintIndex=-1;stageEvaluated=false;stageAttemptPassed=false;showTarget=true;showPrediction=true;scroll=Vector2.zero;
        ResetBoardCamera();RebuildPlanning();
        message=Stage.Lesson+" 목표의 위치와 층수를 맞춰주세요.";
    }
    void InvalidateAttempt(){stageEvaluated=false;stageAttemptPassed=false;if(!result)RebuildPlanning();}
    void EvaluateStage()
    {
        if(Stage==null)return;
        stageEvaluated=true;stageAttemptPassed=Stage.Complete(board);
        if(stageAttemptPassed)
        {
            clearedStages[stageIndex]=true;
            message=AllStagesCleared?"성공! 일곱 스테이지를 모두 완성했어요.":"성공! 목표 모양을 완성했어요. 결과를 확인하고 다음 스테이지로 가세요.";
        }
        else message="아직 목표와 높이가 다른 부분이 있어요. 배치로 돌아가 회색 목표와 연보라 예상 모양을 비교해보세요. 빨간 테두리는 초과한 큐브예요.";
    }
    void RestartStage()
    {
        if(animating)return;if(result)BackToPlan();ClearBoard();hintIndex=-1;showTarget=true;
        RebuildPlanning();if(Stage!=null)message=Stage.Lesson;
    }
    void NextHint()
    {
        if(Stage==null||result)return;
        hintIndex=(hintIndex+1)%Stage.HintSteps.Length;
        RebuildPlanning();
        message="블록을 놓을 위치를 배치판에 파란 선으로 표시했어요.";
    }
    void ToggleTarget()
    {showTarget=!showTarget;foreach(var go in targetVisuals)if(go!=null)go.SetActive(showTarget);}
    void AddTargetOutlines()
    {
        BuildTargetGhost();
    }
}
