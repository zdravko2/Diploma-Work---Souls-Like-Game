using System;
using System.Collections.Generic;

public abstract class StateMachine
{
    readonly List<StateTransition> globalTransitions = new List<StateTransition>();

    public State CurrentState { get; private set; }

    public void Tick()
    {
        if (CurrentState == null)
        {
            return;
        }

        CurrentState.Tick();

        State nextState = GetGlobalTransition();
        if (nextState == null)
        {
            nextState = CurrentState.GetTransition();
        }

        if (nextState != null && nextState != CurrentState)
        {
            ChangeState(nextState);
        }
    }

    protected void SetInitialState(State state)
    {
        if (state == null)
        {
            return;
        }

        CurrentState = state;
        CurrentState.Enter();
    }

    protected void AddGlobalTransition(State toState, Func<bool> condition, int priority = 0)
    {
        if (toState == null || condition == null)
        {
            return;
        }

        globalTransitions.Add(new StateTransition(toState, condition, priority));
        globalTransitions.Sort(StateTransition.CompareByPriority);
    }

    protected void ChangeState(State nextState)
    {
        if (nextState == null || nextState == CurrentState)
        {
            return;
        }

        CurrentState.Exit();
        CurrentState = nextState;
        CurrentState.Enter();
    }

    State GetGlobalTransition()
    {
        for (int i = 0; i < globalTransitions.Count; i++)
        {
            StateTransition transition = globalTransitions[i];
            if (transition.Condition())
            {
                return transition.ToState;
            }
        }

        return null;
    }
}

public abstract class State
{
    readonly List<StateTransition> transitions = new List<StateTransition>();

    protected StateMachine StateMachine { get; private set; }

    protected State(StateMachine stateMachine)
    {
        StateMachine = stateMachine;
    }

    public virtual void Enter()
    {
    }

    public virtual void Tick()
    {
    }

    public virtual void Exit()
    {
    }

    public void AddTransition(State toState, Func<bool> condition, int priority = 0)
    {
        if (toState == null || condition == null)
        {
            return;
        }

        transitions.Add(new StateTransition(toState, condition, priority));
        transitions.Sort(StateTransition.CompareByPriority);
    }

    internal State GetTransition()
    {
        for (int i = 0; i < transitions.Count; i++)
        {
            StateTransition transition = transitions[i];
            if (transition.Condition())
            {
                return transition.ToState;
            }
        }

        return null;
    }
}

public sealed class StateTransition
{
    public State ToState { get; private set; }
    public Func<bool> Condition { get; private set; }
    public int Priority { get; private set; }

    public StateTransition(State toState, Func<bool> condition, int priority)
    {
        ToState = toState;
        Condition = condition;
        Priority = priority;
    }

    public static int CompareByPriority(StateTransition a, StateTransition b)
    {
        return b.Priority.CompareTo(a.Priority);
    }
}
