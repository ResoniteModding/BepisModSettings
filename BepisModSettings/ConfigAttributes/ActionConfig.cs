using System;

namespace BepisModSettings.ConfigAttributes;

public class ActionConfig(Delegate action)
{
    public Delegate Action { get; } = action ?? throw new ArgumentNullException(nameof(action));

    public object Invoke(params object[] args) => Action.DynamicInvoke(args);
}
/*
TODO: implement these inside BepisPluginPage.cs, then uncomment

public class ActionConfig<T>(Action<T> action) : ActionConfig(action)
{
    public void Invoke(T arg) => ((Action<T>)Action)(arg);
}

public class ActionConfig<T1, T2>(Action<T1, T2> action) : ActionConfig(action)
{
    public void Invoke(T1 arg1, T2 arg2) => ((Action<T1, T2>)Action)(arg1, arg2);
}

public class FuncConfig<TResult>(Func<TResult> func) : ActionConfig(func)
{
    public TResult Invoke() => ((Func<TResult>)Action)();
}

public class FuncConfig<T, TResult>(Func<T, TResult> func) : ActionConfig(func)
{
    public TResult Invoke(T arg) => ((Func<T, TResult>)Action)(arg);
}
*/