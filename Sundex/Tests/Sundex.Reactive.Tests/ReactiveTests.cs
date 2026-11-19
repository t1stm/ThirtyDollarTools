using Sundex.Reactive;
using static Sundex.Reactive.Rx;

[assembly: Xunit.v3.Parallelization(Mode = Xunit.Sdk.ParallelMode.None)]

namespace Sundex.Reactive.Tests;

/// <summary>The reactive core is static and single-threaded, so these run one at a time.</summary>
public class ReactiveTests
{
    [Fact]
    public void AnEffectRunsAtOnceThenOncePerFlushHoweverManyWrites()
    {
        var count = State(0);
        var runs = 0;
        var seen = -1;
        using var scope = Scope.Root();
        scope.Run(() => Effect(() =>
        {
            runs++;
            seen = count.Value;
        }));

        Assert.Equal(1, runs);
        count.Value = 1;
        count.Value = 2;
        count.Value = 3;
        Assert.Equal(1, runs);
        Flush();
        Assert.Equal(2, runs);
        Assert.Equal(3, seen);
    }

    [Fact]
    public void WritingAnEqualValueWakesNobody()
    {
        var name = State("a");
        var runs = 0;
        using var scope = Scope.Root();
        scope.Run(() => Effect(() =>
        {
            _ = name.Value;
            runs++;
        }));

        name.Value = "a";
        Flush();
        Assert.Equal(1, runs);
    }

    [Fact]
    public void AComputedThatRecomputesToTheSameAnswerChangesNothingDownstream()
    {
        var count = State(1);
        var computes = 0;
        var runs = 0;
        using var scope = Scope.Root();
        scope.Run(() =>
        {
            var positive = Computed(() =>
            {
                computes++;
                return count.Value > 0;
            });
            Effect(() =>
            {
                _ = positive.Value;
                runs++;
            });
        });

        count.Value = 2;
        Flush();
        Assert.Equal(2, computes);
        Assert.Equal(1, runs);

        count.Value = -1;
        Flush();
        Assert.Equal(2, runs);
    }

    [Fact]
    public void AComputedIsLazy()
    {
        var count = State(1);
        var computes = 0;
        var doubled = new Computed<int>(() =>
        {
            computes++;
            return count.Value * 2;
        });

        count.Value = 5;
        count.Value = 6;
        Assert.Equal(0, computes);
        Assert.Equal(12, doubled.Value);
        Assert.Equal(1, computes);
        Assert.Equal(12, doubled.Value);
        Assert.Equal(1, computes);
    }

    [Fact]
    public void EndingAScopeDisposesItsEffectsAndReleasesItsComputeds()
    {
        var count = State(0);
        var runs = 0;
        var scope = Scope.Root();
        Computed<int>? doubled = null;
        scope.Run(() =>
        {
            doubled = Computed(() => count.Value * 2);
            Effect(() =>
            {
                _ = doubled.Value;
                runs++;
            });
        });

        scope.Dispose();
        count.Value = 1;
        Flush();

        Assert.Equal(1, runs);
        // Released: still answers, but holds no subscription.
        Assert.Equal(2, doubled!.Value);
        Assert.Empty(count.Observers);
    }

    [Fact]
    public void WhatAnEffectRunMadeEndsBeforeTheNextRun()
    {
        var outer = State(0);
        var inner = State(0);
        var innerRuns = 0;
        using var scope = Scope.Root();
        scope.Run(() => Effect(() =>
        {
            _ = outer.Value;
            Effect(() =>
            {
                _ = inner.Value;
                innerRuns++;
            });
        }));

        for (var run = 1; run <= 3; run++)
        {
            outer.Value = run;
            Flush();
        }

        // Four inner effects were made, three of them already ended: one write reaches one.
        innerRuns = 0;
        inner.Value = 1;
        Flush();
        Assert.Equal(1, innerRuns);
        Assert.Single(inner.Observers);
    }

    [Fact]
    public void CleanupsRunBeforeTheNextRunAndAtTheEnd()
    {
        var count = State(0);
        var log = new List<string>();
        var scope = Scope.Root();
        scope.Run(() => Effect(() =>
        {
            var seen = count.Value;
            log.Add($"run {seen}");
            OnCleanup(() => log.Add($"clean {seen}"));
        }));

        count.Value = 1;
        Flush();
        scope.Dispose();

        Assert.Equal(["run 0", "clean 0", "run 1", "clean 1"], log);
    }

    [Fact]
    public void UntrackReadsWithoutSubscribing()
    {
        var count = State(0);
        var runs = 0;
        using var scope = Scope.Root();
        scope.Run(() => Effect(() =>
        {
            _ = Untrack(() => count.Value);
            _ = count.Peek;
            runs++;
        }));

        count.Value = 1;
        Flush();
        Assert.Equal(1, runs);
    }

    [Fact]
    public void UpdateTellsReadersOfAListChangedInPlace()
    {
        var items = State(new List<int> { 1 });
        var total = 0;
        using var scope = Scope.Root();
        scope.Run(() => Effect(() => total = items.Value.Sum()));

        items.Update(list => list.Add(2));
        Flush();
        Assert.Equal(3, total);
    }

    [Fact]
    public void AScopeEndsWithItsParentAndLeavesNoTraceInIt()
    {
        var parent = Scope.Root();
        Scope? early = null, late = null;
        parent.Run(() => early = new Scope());
        early!.Dispose();
        parent.Run(() => late = new Scope());
        var endedEarly = 0;
        early.OnDispose(() => endedEarly++);

        parent.Dispose();

        Assert.True(late!.Disposed);
        Assert.True(late.Token.IsCancellationRequested);
        // The early one was already over: ending the parent didn't end it a second time.
        Assert.Equal(1, endedEarly);
    }

    [Fact]
    public void PumpRunsPostedActionsTimersAndTheFlush()
    {
        var count = State(0);
        var seen = -1;
        var fired = 0;
        using var scope = Scope.Root();
        scope.Run(() =>
        {
            Effect(() => seen = count.Value);
            Frame.After(0, () => fired++);
        });

        Frame.Post(() => count.Value = 7);
        Frame.Pump();

        Assert.Equal(7, seen);
        Assert.Equal(1, fired);
        Frame.Pump();
        Assert.Equal(1, fired);
    }

    [Fact]
    public void TickersAndTimersEndWithTheirScope()
    {
        var ticks = 0;
        var fired = 0;
        var scope = Scope.Root();
        scope.Run(() =>
        {
            Frame.Tick(() =>
            {
                ticks++;
                return true;
            });
            Frame.Every(0, () => fired++);
        });

        Frame.Pump();
        scope.Dispose();
        Frame.Pump();
        Frame.Pump();

        Assert.Equal(1, ticks);
        Assert.Equal(1, fired);
    }

    [Fact]
    public async Task ALoadReloadsWhenWhatItReadChangesAndDropsTheOneItReplaced()
    {
        var query = State("a");
        var pending = new Dictionary<string, TaskCompletionSource<string>>();
        var cancelled = new List<string>();
        using var scope = Scope.Root();
        var result = scope.Run(() => Load(token =>
        {
            var typed = query.Value;
            var source = new TaskCompletionSource<string>();
            token.Register(() => cancelled.Add(typed));
            pending[typed] = source;
            return source.Task;
        }, ""));

        Assert.True(result.Loading.Peek);
        query.Value = "b";
        Flush();
        Assert.Equal(["a"], cancelled);

        pending["a"].SetResult("stale");
        pending["b"].SetResult("fresh");
        await Task.Yield();
        await Task.Delay(10, TestContext.Current.CancellationToken);

        Assert.Equal("fresh", result.Peek);
        Assert.False(result.Loading.Peek);
        Assert.Null(result.Error.Peek);
    }

    [Fact]
    public async Task ALoadThatFailsReportsTheError()
    {
        using var scope = Scope.Root();
        var result = scope.Run(() =>
            Load<int>(_ => Task.FromException<int>(new InvalidOperationException("no")), 0));
        await Task.Delay(10, TestContext.Current.CancellationToken);

        Assert.IsType<InvalidOperationException>(result.Error.Peek);
        Assert.False(result.Loading.Peek);
    }

#if DEBUG
    [Fact]
    public void WritingStateOffTheUiThreadThrowsInDebug()
    {
        var count = State(0);
        Frame.Install();
        try
        {
            // A thread of its own: waiting on a Task.Run can run it inline, on this thread.
            Exception? error = null;
            var worker = new Thread(() => error = Record.Exception(() => count.Value = 1));
            worker.Start();
            worker.Join();
            Assert.IsType<InvalidOperationException>(error);
            count.Value = 2;
        }
        finally
        {
            Frame.Uninstall();
        }
    }
#endif
}
