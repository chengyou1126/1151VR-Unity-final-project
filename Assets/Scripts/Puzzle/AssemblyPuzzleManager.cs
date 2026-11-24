using System;
using System.Collections.Generic;
using UnityEngine;

public enum AnswerResult { Correct, Wrong, GameOver }

/// <summary>第三關組語解謎邏輯：依序作答、答對進下一步、答錯扣命。進度存在 GameManager.Data.quizStep。</summary>
public class AssemblyPuzzleManager : MonoBehaviour
{
    public static AssemblyPuzzleManager Instance { get; private set; }

    public event Action<AssemblyStep> OnStepCompleted;
    public event Action<AssemblyStep> OnWrongAnswer;

    void Awake() { Instance = this; }
    void OnDestroy() { if (Instance == this) Instance = null; }

    public IReadOnlyList<AssemblyStep> Steps => AssemblyQuestions.Steps;

    public int CurrentIndex
    {
        get => GameManager.Instance.Data.quizStep;
        private set => GameManager.Instance.Data.quizStep = value;
    }

    public bool AllDone => CurrentIndex >= Steps.Count;
    public AssemblyStep Current => AllDone ? null : Steps[CurrentIndex];

    /// <summary>某個模組是否已修復（該模組最後一個 completesModule 步驟已通過）。</summary>
    public bool IsModuleRestored(int module)
    {
        for (int i = 0; i < CurrentIndex && i < Steps.Count; i++)
            if (Steps[i].module == module && Steps[i].completesModule) return true;
        return false;
    }

    public int RestoredCount
    {
        get
        {
            int n = 0;
            for (int m = 0; m < AssemblyQuestions.ModuleNames.Length; m++) if (IsModuleRestored(m)) n++;
            return n;
        }
    }

    public AnswerResult Submit(int option)
    {
        var step = Current;
        if (step == null) return AnswerResult.Correct;

        if (option == step.correct)
        {
            CurrentIndex++;
            OnStepCompleted?.Invoke(step);
            return AnswerResult.Correct;
        }

        OnWrongAnswer?.Invoke(step);
        bool alive = GameManager.Instance.LoseLife(LifeLossReason.WrongAnswer);
        return alive ? AnswerResult.Wrong : AnswerResult.GameOver;
    }
}
