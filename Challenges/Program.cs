using Microsoft.Reactive.Testing;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;

namespace Challenges
{
    public class Program
    {
        static void Main()
        {
            //Playground.FilterMerge();
            Playground.SuspendResumeTrial();

            Console.ReadKey();
        }
    }

    public class RxChallengesTests
    {
        [Test]
        public void Buffer_ShouldBulkOn_CountTimeoutOrFlush_NaiveSolution()
        {
            // produce a buffer that should produce its bulk
            // as result of one of the following trigger
            // timeout, count threshold or manual flush request

            // arrange
            var testScheduler = new TestScheduler();
            var testObserver = testScheduler.CreateObserver<IList<int>>();

            var manualTrigger = new Subject<Unit>();

            var ctfBuffer = new CTFBuffer(testScheduler, manualTrigger);
            ctfBuffer.Subscribe(testObserver);

            // act
            for (int i = 0; i < 10; i++)
            {
                ctfBuffer.OnNext(i); // should bulk after 6 items
            }

            testScheduler.AdvanceBy(TimeSpan.FromMilliseconds(510).Ticks);

            for (int i = 10; i < 12; i++)
            {
                ctfBuffer.OnNext(i);
            }
            manualTrigger.OnNext(Unit.Default);
            for (int i = 12; i < 15; i++)
            {
                ctfBuffer.OnNext(i);
            }
            ctfBuffer.OnCompleted(); // trigger the last balk

            // verify
            IList<int>[] results = testObserver.
                Messages
                .Where(m => m.Value.Kind == NotificationKind.OnNext)
                .Select(m => m.Value.Value).ToArray();

            Assert.AreEqual(4, results.Length);
            ReactiveAssert.AreElementsEqual(new[] { 0, 1, 2, 3, 4, 5 }, results[0]);
            ReactiveAssert.AreElementsEqual(new[] { 6, 7, 8, 9 }, results[1]);
            ReactiveAssert.AreElementsEqual(new[] { 10, 11 }, results[2]);
            ReactiveAssert.AreElementsEqual(new[] { 12, 13, 14 }, results[3]);
        }

        [Test]
        public void Buffer_ShouldBulkOn_CountTimeoutOrFlush_ProvidedSolution()
        {
            // produce a buffer that should produce its bulk
            // as result of one of the following trigger
            // timeout, count threshold or manual flush request

            // arrange
            ISubject<int> source = new Subject<int>();
            TimeSpan timeout = TimeSpan.FromSeconds(0.5);
            int threshold = 6;

            var testScheduler = new TestScheduler();
            var testObserver = testScheduler.CreateObserver<IList<int>>();

            ISubject<int> trigger = new Subject<int>();

            //Solution: Use the Buffer overload that takes an observable as a sequence closing selector
            IObservable<IList<int>> bulks = source.Buffer(() => Observable.Merge(
                trigger,
                source.Skip(threshold - 1).Take(1), // subscribe on opening
                source.Sample(timeout).Take(1)));
            bulks.Subscribe(testObserver);

            // act
            for (int i = 0; i < 10; i++)
            {
                source.OnNext(i); // should bulk after 6 items
            }
            TimeSpan sleep = timeout.Add(TimeSpan.FromMilliseconds(10));
            Thread.Sleep(sleep);
            for (int i = 10; i < 12; i++)
            {
                source.OnNext(i); // should bulk after 6 items
            }
            trigger.OnNext(-1);
            for (int i = 12; i < 15; i++)
            {
                source.OnNext(i); // should bulk after 6 items
            }
            source.OnCompleted(); // trigger the last balk

            // verify
            IList<int>[] results = testObserver.
                Messages
                .Where(m => m.Value.Kind == NotificationKind.OnNext)
                .Select(m => m.Value.Value).ToArray();

            Assert.AreEqual(4, results.Length);
            ReactiveAssert.AreElementsEqual(new[] { 0, 1, 2, 3, 4, 5 }, results[0]);
            ReactiveAssert.AreElementsEqual(new[] { 6, 7, 8, 9 }, results[1]);
            ReactiveAssert.AreElementsEqual(new[] { 10, 11 }, results[2]);
            ReactiveAssert.AreElementsEqual(new[] { 12, 13, 14 }, results[3]);
        }

        [Test]
        public void AvoidOverlappingStreams_ByUsingSwitchOperator()
        {
            /******************************************************
            *
            * you start with Stream that create sub stream of string,
            * each sub stream is built of single char representation.
            *
            * --#----------------@----------------*------------------
            *
            *    |
            *
            *     #--##--###--####--#####--######--#######--########
            *
            *                    |
            *
            *                    @--@@--@@@--@@@@--@@@@--@@@@@--@@@@@@
            *
            *                                     |
            *
            *                                     *---**---***---****
            *
            * you should find a way to get output stream where
            * listen to the latest sub stream.
            *
            * the output stream should be look like:
            *
            * --#--##--###--####-@--@@--@@@--@@@@-*---**---***---****
            *
            * in other words you have to avoid overlapping char.
            *
            ******************************************************/

            var scheduler = new TestScheduler();
            var observer = scheduler.CreateObserver<string>();

            var source = new Subject<char>();
            var overlapped = from c in source
                // create stream per char
                select Observable.Interval(TimeSpan.FromMilliseconds(1), scheduler).
                    Select(l => new string(c, (int)l + 1)); // multiply the char

            //Solution: use switch instead of Merge
            var flatten = overlapped.Switch();
            flatten.Subscribe(observer);

            source.OnNext('#');
            scheduler.AdvanceBy(TimeSpan.FromMilliseconds(3).Ticks);
            source.OnNext('@');
            scheduler.AdvanceBy(TimeSpan.FromMilliseconds(3).Ticks);
            source.OnNext('*');
            scheduler.AdvanceBy(TimeSpan.FromMilliseconds(3).Ticks);

            var results = observer.
                Messages
                .Where(m => m.Value.Kind == NotificationKind.OnNext)
                .Select(m => m.Value.Value).ToArray();

            ReactiveAssert.AreElementsEqual(
                new[] { "#", "##", "###", "@", "@@", "@@@", "*", "**", "***" },
                results);

        }

        [Test]
        public void SuspendResume_2_Test()
        {
            /**********************************************************************
                *
                * source -1---------2---------3-4-5-6-7--------8---------9------
                * Result -1---------2---------3-4------------------------9------
                *
                **********************************************************************/

            int suspendOnCount = 3;
            TimeSpan suspendCountDuration = TimeSpan.FromSeconds(5);
            TimeSpan suspendDuration = TimeSpan.FromSeconds(15);

            // arrange
            var scheduler = new TestScheduler();
            var observer = scheduler.CreateObserver<int>();

            var source = new Subject<int>();

            // act
            var windowIdx = 0;
            var result = source.Window(source, i => Observable.Timer(suspendCountDuration)).Subscribe(
                w =>
                {
                    var thisWindowIdx = windowIdx++;
                    Console.WriteLine("--Starting new window");
                    var windowName = "Window" + thisWindowIdx;
                    w.Subscribe(i => Console.WriteLine($"{i} is in window {windowName}"));
                });


            /**********************************************************************
                *
                * source --1---------2----------3-4-5-6-7--------8---------9------
                *
                **********************************************************************/

            scheduler.AdvanceBy(TimeSpan.FromSeconds(2).Ticks);
            source.OnNext(1);
            scheduler.AdvanceBy(TimeSpan.FromSeconds(10).Ticks);
            source.OnNext(2);
            scheduler.AdvanceBy(TimeSpan.FromSeconds(11).Ticks);
            source.OnNext(3);
            scheduler.AdvanceBy(TimeSpan.FromSeconds(2).Ticks);
            source.OnNext(4);
            scheduler.AdvanceBy(TimeSpan.FromSeconds(2).Ticks);
            source.OnNext(5);
            scheduler.AdvanceBy(TimeSpan.FromSeconds(2).Ticks);
            source.OnNext(6);
            scheduler.AdvanceBy(TimeSpan.FromSeconds(2).Ticks);
            source.OnNext(7);
            scheduler.AdvanceBy(TimeSpan.FromSeconds(9).Ticks);
            source.OnNext(8);
            scheduler.AdvanceBy(TimeSpan.FromSeconds(10).Ticks);
            source.OnNext(9);

            // verify
            var results = observer.
                Messages
                .Where(m => m.Value.Kind == NotificationKind.OnNext)
                .Select(m => m.Value.Value).ToArray();

            Assert.AreEqual(1, results[0]);
            Assert.AreEqual(2, results[1]);
            Assert.AreEqual(3, results[2]);
            Assert.AreEqual(4, results[3]);
            Assert.AreEqual(9, results[4]);
        }
    }
}
