// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// https://github.com/dotnet/runtime/blob/a0964f9e3793cb36cc01d66c14a61e89ada5e7da/src/libraries/Microsoft.Extensions.DependencyInjection/src/ServiceLookup/StackGuard.cs

using System.Runtime.CompilerServices;
using System.Threading;

namespace Jint.Runtime.CallStack;

internal sealed class StackGuard
{
    private const int MaxExecutionStackCount = 1024;

    private int _executionStackCount;

    public bool TryEnterOnCurrentStack()
    {
#if NETFRAMEWORK || NETSTANDARD2_0
        try
        {
            RuntimeHelpers.EnsureSufficientExecutionStack();
            return true;
        }
        catch (InsufficientExecutionStackException)
        {
        }
#else
        if (RuntimeHelpers.TryEnsureSufficientExecutionStack())
        {
            return true;
        }
#endif

        if (_executionStackCount < MaxExecutionStackCount)
        {
            return false;
        }

        throw new InsufficientExecutionStackException();
    }

    public TR RunOnEmptyStack<T1, TR>(Func<T1, TR> action, T1 arg1)
    {
#if NETFRAMEWORK || NETSTANDARD2_0
        return RunOnEmptyStackCore(static s =>
        {
            var t = (Tuple<Func<T1, TR>, T1>) s;
            return t.Item1(t.Item2);
        }, Tuple.Create(action, arg1));
#else
        // Prefer ValueTuple when available to reduce dependencies on Tuple
        return RunOnEmptyStackCore(static s =>
        {
            var t = ((Func<T1, TR>, T1)) s;
            return t.Item1(t.Item2);
        }, (action, arg1));
#endif
    }

    private R RunOnEmptyStackCore<R>(Func<object, R> action, object state)
    {
        _executionStackCount++;

        try
        {
            // Using default scheduler rather than picking up the current scheduler.
            Task<R> task = Task.Factory.StartNew((Func<object?, R>) action, state, CancellationToken.None, TaskCreationOptions.DenyChildAttach, TaskScheduler.Default);

            // Avoid AsyncWaitHandle lazy allocation of ManualResetEvent in the rare case we finish quickly.
            if (!task.IsCompleted)
            {
                // Task.Wait has the potential of inlining the task's execution on the current thread; avoid this.
                ((IAsyncResult) task).AsyncWaitHandle.WaitOne();
            }

            // Using awaiter here to propagate original exception
            return task.GetAwaiter().GetResult();
        }
        finally
        {
            _executionStackCount--;
        }
    }
}
