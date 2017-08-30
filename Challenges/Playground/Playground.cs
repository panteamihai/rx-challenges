using Microsoft.Reactive.Testing;
using System;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace Challenges
{
    public static class TimePrinterExtensions
    {
        public static string ToSeconds(this DateTimeOffset offset)
        {
            return offset.DateTime.ToString("ss.fff") + " sec(s)";
        }
    }

    class Playground
    {
        internal class ValueDescriptor<T>
        {
            public T Value { get; }
            public Emit Emit { get; }
            public NotificationKind Kind { get; }
            public DateTimeOffset Timestamp { get; }
            public string Window { get; }

            public ValueDescriptor()
            {
                Value = default(T);
                Emit = Emit.None;
                Kind = NotificationKind.OnNext;
                Timestamp = DateTimeOffset.MinValue;
                Window = null;
            }

            public ValueDescriptor(T value, Emit emit, NotificationKind kind, DateTimeOffset timestamp, string window)
            {
                Value = value;
                Emit = emit;
                Kind = kind;
                Timestamp = timestamp;
                Window = window;
            }

            public override string ToString()
            {
                return Kind == NotificationKind.OnNext
                    ? $"{Value} appeared after {Timestamp.ToSeconds()} as {Emit} through {Kind} in {Window}"
                    : $"{Kind} in {Window} after {Timestamp.ToSeconds()}";
            }

            public string ToShortString()
            {
                var s = "after " + Timestamp.DateTime.ToString("ss.fff") + " sec(s)";
                return Kind == NotificationKind.OnNext
                    ? $"{Value} emitted @ {s} as {Emit} by an {Kind} in {Window}"
                    : $"{Kind} in {Window} @ {s}";
            }
        }

        public static void SuspendResumeTrial()
        {
            var scheduler = new TestScheduler();

            int suspendOnCount = 3;
            TimeSpan suspendCountDuration = TimeSpan.FromSeconds(5);
            TimeSpan suspendDuration = TimeSpan.FromSeconds(15);

            var subject = new Subject<int>();
            var source = subject.ObserveOn(scheduler);

            var heartbeatCount = 0;
            var closer = Observable.Interval(suspendCountDuration, scheduler);
            closer.Subscribe(_ => { ++heartbeatCount; Console.WriteLine($"[CLOSER] Tick #{heartbeatCount}");});

            var windowIndex = 0;
            var refinedOverlappingWindows = source
                                            .Window(source, _ => closer)
                                            .Select(window =>
                                                    {
                                                        var localWindowIndex = windowIndex++;
                                                        var windowName = $"[Window{localWindowIndex}]";
                                                        var refinedWindowName = $"[RefinedWindow{localWindowIndex}]";

                                                        var windowOpening = true;
                                                        window.Subscribe(i =>
                                                            {
                                                                if (windowOpening)
                                                                {
                                                                    Console.WriteLine($"{windowName} Opened @ {scheduler.Now.ToSeconds()}");
                                                                    windowOpening = false;
                                                                }

                                                                Console.WriteLine($"{windowName} Got {i} @ {scheduler.Now.ToSeconds()}");
                                                            }, () => Console.WriteLine($"{windowName} Closed @ {scheduler.Now.ToSeconds()}"));

                                                        // We decorate the element/value with a flag which characterizes what its existence represents for this particular
                                                        // 5 second window. If it is the 3rd to be blipped, then it will actually end the window since, as the problem states,
                                                        // it should now suspend the original observable's emitting.
                                                        var valueEmittingObservable = window.Take(2).Select(i => new Tuple<int, Emit>(i, Emit.Value));
                                                        var delayEmittingObservable = window.Skip(2).Take(1).Select(i => new Tuple<int, Emit>(i, Emit.Delay));

                                                        // We'll refine the windows in the sense that we'll turn them into either windows of 5 seconds or 3 elements
                                                        // The (Take + Merge) combo ensures that if the initial 5 second window has more than 3 elements, it will be closed
                                                        var refinedWindows = valueEmittingObservable.Merge(delayEmittingObservable);

                                                        var refinedWindowOpening = true;
                                                        refinedWindows.Subscribe(
                                                            i =>
                                                            {
                                                                if (refinedWindowOpening)
                                                                {
                                                                    Console.WriteLine($"{refinedWindowName} Opened @ {scheduler.Now.ToSeconds()}");
                                                                    refinedWindowOpening = false;
                                                                }
                                                                Console.WriteLine($"{refinedWindowName} Got {i} @ {scheduler.Now.ToSeconds()}");
                                                            }, () => Console.WriteLine($"{refinedWindowName} Closed @ {scheduler.Now.ToSeconds()}"));

                                                        // We materialize the window because we'll next flatten the resulting windows, and their overlaps will contain
                                                        // what the value/element represents in all the windows it participates in. A value/element is emitted as a fixed point
                                                        // on the axis, so we'll need that information too, that's why we do a Timestamp transformation.
                                                        // We'll only be concerned with OnNext events, not the OnCompleted events of windows, but since they do occur, we'll have
                                                        // to add some dummy values to them (null pattern-ish)
                                                        return refinedWindows
                                                                .Materialize()
                                                                .Where(i => i.Kind == NotificationKind.OnNext)
                                                                .Dematerialize()
                                                                .Timestamp(scheduler);
                                                    })
                                            .Merge();

            var refinedWindowGroups = refinedOverlappingWindows.GroupBy(i => i.Timestamp);

            var groupIndex = 0;
            var flattenedRefinement = refinedWindowGroups.SelectMany(group =>
                                            {
                                                var localGroupIndex = groupIndex++;
                                                var groupName = $"[Group{localGroupIndex}]";
                                                var timeboundGroupName = $"[TimeboundGroup{localGroupIndex}]";

                                                var groupOpening = true;
                                                group.Subscribe(
                                                    i =>
                                                    {
                                                        if (groupOpening)
                                                        {
                                                            Console.WriteLine($"{groupName} Opened @ {scheduler.Now.ToSeconds()}");
                                                            groupOpening = false;
                                                        }
                                                        Console.WriteLine($"{groupName} Got {i.Value} @ {scheduler.Now.ToSeconds()}");
                                                    }, () => Console.WriteLine($"{groupName} Closed @ {scheduler.Now.ToSeconds()}"));

                                                var timeboundGroup = group.TakeUntil(Observable.Timer(TimeSpan.FromTicks(1), scheduler));
                                                // All the values in the group should "materialize" instantly since the group is over a point in time (timestamp).
                                                // The group needs to be closed manually because it wont complete on its own.
                                                // Why? Because the point in time (timestamp) might only emit one value, or be just an OnCompleted event
                                                var timeboundGroupOpening = true;
                                                timeboundGroup.Subscribe(
                                                    i =>
                                                    {
                                                        if (timeboundGroupOpening)
                                                        {
                                                            Console.WriteLine($"{timeboundGroupName} Opened @ {scheduler.Now.ToSeconds()}");
                                                            timeboundGroupOpening = false;
                                                        }
                                                        Console.WriteLine($"{timeboundGroupName} Got {i.Value} @ {scheduler.Now.ToSeconds()}");
                                                    }, exception => Console.WriteLine($"{timeboundGroupName} errored"), () => Console.WriteLine($"{timeboundGroupName} Closed @ {scheduler.Now.ToSeconds()}"));

                                                var aggregatePerPointInTime = timeboundGroup.Aggregate(new Tuple<int, Emit>(int.MinValue, Emit.None), (acc, current) => current.Value.Item2 == Emit.Delay ? current.Value : (acc != null && acc.Item2 == Emit.Delay ? acc : current.Value));
                                                aggregatePerPointInTime.Subscribe(i => Console.WriteLine($"\t[Aggregate @ Key{{ {group.Key.ToSeconds()} }}] is {i} @ {scheduler.Now.ToSeconds()}"));

                                                return aggregatePerPointInTime;
                                            });

            flattenedRefinement.Subscribe(r => Console.WriteLine("\t\t[PROCESSED] " + r));

            /**********************************************************************
                *
                * source --1---------2----------3-4-5-6-7--------8---------9------
                *
                **********************************************************************/

            scheduler.AdvanceBy(TimeSpan.FromMilliseconds(2000).Ticks);
            subject.OnNext(1);
            scheduler.AdvanceBy(TimeSpan.FromMilliseconds(10000).Ticks);
            subject.OnNext(2);
            scheduler.AdvanceBy(TimeSpan.FromMilliseconds(11000).Ticks);
            subject.OnNext(3);
            scheduler.AdvanceBy(TimeSpan.FromMilliseconds(2000).Ticks);
            subject.OnNext(4);
            scheduler.AdvanceBy(TimeSpan.FromMilliseconds(1500).Ticks);
            subject.OnNext(5);
            scheduler.AdvanceBy(TimeSpan.FromMilliseconds(1250).Ticks);
            subject.OnNext(6);
            scheduler.AdvanceBy(TimeSpan.FromMilliseconds(2250).Ticks);
            subject.OnNext(7);
            scheduler.AdvanceBy(TimeSpan.FromMilliseconds(9000).Ticks);
            subject.OnNext(8);
            scheduler.AdvanceBy(TimeSpan.FromMilliseconds(10000).Ticks);
            subject.OnNext(9);
            scheduler.AdvanceBy(TimeSpan.FromMilliseconds(1).Ticks);
            subject.OnCompleted();
        }

        public static void MergeVsGroupVsIntersect()
        {
            Subject<char> stream1 = new Subject<char>();
            Subject<char> stream2 = new Subject<char>();

            stream1.AsObservable().Subscribe(x => Console.WriteLine("stream1: " + x));
            stream2.AsObservable().Subscribe(x => Console.WriteLine("stream2: " + x));

            var distinctOne = stream1.AsObservable().Distinct();
            distinctOne.Subscribe(x => Console.WriteLine("\tdistinctOne: " + x));

            var distinctTwo = stream2.AsObservable().Distinct();
            distinctTwo.Subscribe(x => Console.WriteLine("\tdistinctTwo: " + x));

            var merged = distinctOne.Merge(distinctTwo);
            merged.Subscribe(x => Console.WriteLine("\t\tmerged: " + x));

            var grouped = merged.GroupBy(c => c);
            merged.Subscribe(x => Console.WriteLine("\t\t\tgrouped: " + x));

            var intersect = grouped.SelectMany(g => g.Skip(1).Take(1));
            intersect.Subscribe(x => Console.WriteLine("\t\t\t\tintersect: " + x));

            stream2.OnNext('b');
            stream1.OnNext('a');
            stream2.OnNext('a');
            stream1.OnNext('b');
            stream2.OnNext('e');
            stream1.OnNext('c');
            stream1.OnNext('d');
            stream1.OnNext('e');
            stream1.OnNext('a');
            stream2.OnNext('a');
            stream1.OnNext('b');
            stream2.OnNext('b');
            stream1.OnNext('c');
            stream2.OnNext('c');
            stream1.OnCompleted();
            stream2.OnCompleted();
        }

        public static void FilterMerge()
        {
            var scheduler = new TestScheduler();

            Subject<int> stream = new Subject<int>();

            stream.Where(i => i % 2 == 0).Take(1).Merge(stream.Where(i => i % 2 == 1).Take(1))
                .Subscribe(i => Console.WriteLine($"Got: {i}"));

            scheduler.AdvanceBy(TimeSpan.FromSeconds(2).Ticks);
            stream.OnNext(1);
            scheduler.AdvanceBy(TimeSpan.FromSeconds(2).Ticks);
            stream.OnNext(7);
            scheduler.AdvanceBy(TimeSpan.FromSeconds(2).Ticks);
            stream.OnNext(3);
            scheduler.AdvanceBy(TimeSpan.FromSeconds(2).Ticks);
            stream.OnNext(9);
            scheduler.AdvanceBy(TimeSpan.FromSeconds(2).Ticks);
            stream.OnNext(2);
            stream.OnCompleted();
        }
    }
}
