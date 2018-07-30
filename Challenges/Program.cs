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
            Console.ReadKey();
        }
    }

    public class RxChallengesTests
    {
        [Test]
        public void Buffer_ShouldProduceOn_CountOrTimeoutOrFlush_ByImplementingNaiveBuffer()
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
        public void Buffer_ShouldProduceOn_CountAndTimeoutAndFlush_ByUsingBuffer()
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
        public void Buffer_ShouldProduceOn_CountAndTimeoutAndFlush_ByReimplementingBufferWithWindowAndAggregate()
        {
            //Arrange
            var timeThreshold = TimeSpan.FromMilliseconds(500);
            var countThreshold = 6;

            var testScheduler = new TestScheduler();
            var testObserver = testScheduler.CreateObserver<IList<int>>();

            var source = testScheduler.CreateHotObservable( //Either Hot or Cold + Publish + RefCount, due to it being shared with the "count" observable
                new Recorded<Notification<int>>(TimeSpan.FromMilliseconds(1).Ticks, Notification.CreateOnNext(0)),
                new Recorded<Notification<int>>(TimeSpan.FromMilliseconds(1).Ticks + 1, Notification.CreateOnNext(1)),
                new Recorded<Notification<int>>(TimeSpan.FromMilliseconds(1).Ticks + 2, Notification.CreateOnNext(2)),
                new Recorded<Notification<int>>(TimeSpan.FromMilliseconds(1).Ticks + 3, Notification.CreateOnNext(3)),
                new Recorded<Notification<int>>(TimeSpan.FromMilliseconds(1).Ticks + 4, Notification.CreateOnNext(4)),
                new Recorded<Notification<int>>(TimeSpan.FromMilliseconds(1).Ticks + 5, Notification.CreateOnNext(5)),
                // Spew above due to count
                new Recorded<Notification<int>>(TimeSpan.FromMilliseconds(1).Ticks + 6, Notification.CreateOnNext(6)),
                new Recorded<Notification<int>>(TimeSpan.FromMilliseconds(1).Ticks + 7, Notification.CreateOnNext(7)),
                new Recorded<Notification<int>>(TimeSpan.FromMilliseconds(1).Ticks + 8, Notification.CreateOnNext(8)),
                new Recorded<Notification<int>>(TimeSpan.FromMilliseconds(1).Ticks + 9, Notification.CreateOnNext(9)),
                // Spew above due to timout
                new Recorded<Notification<int>>(TimeSpan.FromMilliseconds(510).Ticks, Notification.CreateOnNext(10)),
                new Recorded<Notification<int>>(TimeSpan.FromMilliseconds(510).Ticks + 1, Notification.CreateOnNext(11)),
                // Spew above due to flush
                new Recorded<Notification<int>>(TimeSpan.FromMilliseconds(515).Ticks, Notification.CreateOnNext(12)),
                new Recorded<Notification<int>>(TimeSpan.FromMilliseconds(515).Ticks + 1, Notification.CreateOnNext(13)),
                new Recorded<Notification<int>>(TimeSpan.FromMilliseconds(515).Ticks + 2, Notification.CreateOnNext(14)),
                // Spew above due to timeout
                new Recorded<Notification<int>>(TimeSpan.FromMilliseconds(1200).Ticks, Notification.CreateOnCompleted<int>())
            );

            var timer = Observable.Interval(timeThreshold, testScheduler).TakeUntil(source.TakeLast(1).Do(_ => Console.WriteLine("Blipped Last"))).Select(_ => Unit.Default).Do(_ => Console.WriteLine("Blipped from Timer"));
            var count = source.Buffer(countThreshold).Select(_ => Unit.Default).Do(_ => Console.WriteLine("Blipped from Buffer"));
            var flush = testScheduler.CreateHotObservable(
                new Recorded<Notification<Unit>>(TimeSpan.FromMilliseconds(513).Ticks, Notification.CreateOnNext(Unit.Default)),
                new Recorded<Notification<Unit>>(TimeSpan.FromMilliseconds(1200).Ticks, Notification.CreateOnCompleted<Unit>())
            ).Do(_ => Console.WriteLine("Blipped from Flush"));

            var merged = Observable.Merge(timer, count, flush);

            //Act
            BufferNonEmptyThroughWindow(source, merged).Subscribe(testObserver);

            //Assert
            testScheduler.Start();
            var results = testObserver.Messages
                .Where(m => m.Value.Kind == NotificationKind.OnNext)
                .Select(m => m.Value.Value).ToArray();

            Assert.AreEqual(4, results.Length);
            ReactiveAssert.AreElementsEqual(new[] { 0, 1, 2, 3, 4, 5 }, results[0]);
            ReactiveAssert.AreElementsEqual(new[] { 6, 7, 8, 9 }, results[1]);
            ReactiveAssert.AreElementsEqual(new[] { 10, 11 }, results[2]);
            ReactiveAssert.AreElementsEqual(new[] { 12, 13, 14 }, results[3]);
        }

        private static IObservable<List<int>> BufferNonEmptyThroughWindow(IObservable<int> source, IObservable<Unit> merged)
        {
            var buffers = source.Window(() => merged)
                .SelectMany(
                    window =>
                    {
                        return window.Aggregate(
                            new List<int>(),
                            (acc, curr) =>
                            {
                                acc.Add(curr);

                                return acc;
                            });
                    })
                .Where(b => b.Any()); //The "non-empty" part

            return buffers;
        }

        [Test]
        public void Buffer_ShouldProduceOn_CountOrTimeoutOrFlush_ByUsingAmbAndDefferedRecursion()
        {
            //Arrange
            var timeThreshold = TimeSpan.FromMilliseconds(20);
            var countThreshold = 3;

            var testScheduler = new TestScheduler();
            var testObserver = testScheduler.CreateObserver<IList<int>>();

            var source = testScheduler.CreateHotObservable(
                new Recorded<Notification<int>>(TimeSpan.FromMilliseconds(1).Ticks, Notification.CreateOnNext(0)),
                new Recorded<Notification<int>>(TimeSpan.FromMilliseconds(2).Ticks, Notification.CreateOnNext(1)),
                new Recorded<Notification<int>>(TimeSpan.FromMilliseconds(3).Ticks, Notification.CreateOnNext(2)),
                // Spew above due to count
                new Recorded<Notification<int>>(TimeSpan.FromMilliseconds(4).Ticks, Notification.CreateOnNext(3)),
                // Spew above due to timeout
                new Recorded<Notification<int>>(TimeSpan.FromMilliseconds(25).Ticks, Notification.CreateOnNext(4)),
                // Spew above due to flush
                new Recorded<Notification<int>>(TimeSpan.FromMilliseconds(33).Ticks, Notification.CreateOnNext(5)),
                //Spew above due to timeout
                new Recorded<Notification<int>>(TimeSpan.FromMilliseconds(54).Ticks, Notification.CreateOnNext(6)),
                new Recorded<Notification<int>>(TimeSpan.FromMilliseconds(64).Ticks, Notification.CreateOnNext(7)),
                //Spew above due to timeout
                new Recorded<Notification<int>>(TimeSpan.FromMilliseconds(75).Ticks, Notification.CreateOnNext(8)),
                new Recorded<Notification<int>>(TimeSpan.FromMilliseconds(76).Ticks, Notification.CreateOnNext(9)),
                new Recorded<Notification<int>>(TimeSpan.FromMilliseconds(77).Ticks, Notification.CreateOnNext(10)),
                // Spew above due to count
                new Recorded<Notification<int>>(TimeSpan.FromMilliseconds(96).Ticks, Notification.CreateOnNext(11)),
                // Spew above due to flush
                new Recorded<Notification<int>>(TimeSpan.FromMilliseconds(98).Ticks, Notification.CreateOnNext(12)),
                new Recorded<Notification<int>>(TimeSpan.FromMilliseconds(99).Ticks, Notification.CreateOnNext(13)),
                new Recorded<Notification<int>>(TimeSpan.FromMilliseconds(100).Ticks, Notification.CreateOnNext(14)),
                // Spew above due to count
                new Recorded<Notification<int>>(TimeSpan.FromMilliseconds(101).Ticks, Notification.CreateOnCompleted<int>())
            );

            var manualFlush = testScheduler.CreateHotObservable(
                new Recorded<Notification<Unit>>(TimeSpan.FromMilliseconds(27).Ticks, Notification.CreateOnNext(Unit.Default)),
                new Recorded<Notification<Unit>>(TimeSpan.FromMilliseconds(97).Ticks, Notification.CreateOnNext(Unit.Default)),
                new Recorded<Notification<Unit>>(TimeSpan.FromMilliseconds(101).Ticks, Notification.CreateOnCompleted<Unit>())
            );

            var timeOrCountOrFlush = GetTimeOrCount(source, testScheduler, timeThreshold, countThreshold, manualFlush);

            //Act
            source.Buffer(timeOrCountOrFlush).Where(b => b.Any()).Subscribe(testObserver);

            //Assert
            testScheduler.Start();
            var results = testObserver.Messages
                .Where(m => m.Value.Kind == NotificationKind.OnNext)
                .Select(m => m.Value.Value).ToArray();

            Assert.AreEqual(8, results.Length);
            ReactiveAssert.AreElementsEqual(new[] { 0, 1, 2 }, results[0]);
            ReactiveAssert.AreElementsEqual(new[] { 3 }, results[1]);
            ReactiveAssert.AreElementsEqual(new[] { 4 }, results[2]);
            ReactiveAssert.AreElementsEqual(new[] { 5 }, results[3]);
            ReactiveAssert.AreElementsEqual(new[] { 6, 7 }, results[4]);
            ReactiveAssert.AreElementsEqual(new[] { 8, 9, 10 }, results[5]);
            ReactiveAssert.AreElementsEqual(new[] { 11 }, results[6]);
            ReactiveAssert.AreElementsEqual(new[] { 12, 13, 14 }, results[7]);
        }

        private static IObservable<Unit> GetTimeOrCount(
            IObservable<int> source, TestScheduler testScheduler,
            TimeSpan timeThreshold, int countThreshold, IObservable<Unit> manualFlush)
        {
            return Observable.Amb(
                    Observable.Timer(timeThreshold, testScheduler).Select(_ => Unit.Default).Do(_ => Console.WriteLine("Blipped from Timer")),
                    source.Buffer(countThreshold).Take(1).Select(_ => Unit.Default).Do(_ => Console.WriteLine("Blipped from Buffer")),
                    manualFlush.Take(1).Do(_ => Console.WriteLine("Blipped from Flush")))
                .Concat(Observable.Defer(() => GetTimeOrCount(source, testScheduler, timeThreshold, countThreshold, manualFlush)));
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
        public void SuspendDuringFlood_ByUsingBufferAndScan()
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
            var floodPresured = source.SuspendDuringFlood(suspendOnCount, suspendCountDuration, suspendDuration, scheduler);
            floodPresured.Subscribe(observer);

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
            scheduler.AdvanceBy(1);
            source.OnCompleted();

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

        [Test]
        public void SuspendDuringFloodWithRectification_ByUsingBufferAndScan()
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
            var floodPresured = source.SuspendDuringFloodWithRectification(suspendOnCount, suspendCountDuration, suspendDuration, scheduler);
            floodPresured.Subscribe(observer);

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
            scheduler.AdvanceBy(1);
            source.OnCompleted();

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

        [Test]
        public void SuspendDuringFlood_EmphasizeTheOverlappingNatureOfSuspensions()
        {
            /******************************************************************************
             * Max = 3, Window = 5000ms, Suspend = 15000ms
             * Source -1---------2---------3-4-5-----------6-7-8---------9---------10------
             * Result -1---------2---------3-4-------------------------------------10------
             *
             * Essentially, 6-7-8 triggers a suspension (as per the one-pass analysis of the source
             * sequence) and that swallows up 9.
             ******************************************************************************/

            // arrange
            var scheduler = new TestScheduler();
            scheduler.AdvanceBy(TimeSpan.FromDays(1).Ticks);

            var observer = scheduler.CreateObserver<int>();

            var source = new Subject<int>();

            source.SuspendDuringFlood(3, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(15), scheduler)
                  .Subscribe(observer);

            // act
            /******************************************************************************
             *
             * source -1---------2---------3-4-5-----------6-7-8---------9---------10------
             *
             ******************************************************************************/

            scheduler.AdvanceBy(TimeSpan.FromSeconds(2).Ticks);  // 2
            source.OnNext(1);
            scheduler.AdvanceBy(TimeSpan.FromSeconds(10).Ticks); // 12
            source.OnNext(2);
            scheduler.AdvanceBy(TimeSpan.FromSeconds(11).Ticks); // 23
            source.OnNext(3);
            scheduler.AdvanceBy(TimeSpan.FromSeconds(2).Ticks);  // 25
            source.OnNext(4);
            scheduler.AdvanceBy(TimeSpan.FromSeconds(2).Ticks);  // 27 - Start suspension until 41
            source.OnNext(5);
            scheduler.AdvanceBy(TimeSpan.FromSeconds(12).Ticks); // 39 - Suspend
            source.OnNext(6);
            scheduler.AdvanceBy(TimeSpan.FromSeconds(2).Ticks);  // 41 - Suspend
            source.OnNext(7);
            scheduler.AdvanceBy(TimeSpan.FromSeconds(2).Ticks);  // 43 - Start new suspension until 58
            source.OnNext(8);
            scheduler.AdvanceBy(TimeSpan.FromSeconds(10).Ticks); // 53 - Suspend (within the new re-suspend period)
            source.OnNext(9);
            scheduler.AdvanceBy(TimeSpan.FromSeconds(10).Ticks); // 63
            source.OnNext(10);
            scheduler.AdvanceBy(1);
            source.OnCompleted();

            // verify
            var results = observer.
                Messages
                .Where(m => m.Value.Kind == NotificationKind.OnNext)
                .Select(m => m.Value.Value).ToArray();

            Assert.AreEqual(1, results[0]);
            Assert.AreEqual(2, results[1]);
            Assert.AreEqual(3, results[2]);
            Assert.AreEqual(4, results[3]);
            Assert.AreEqual(10, results[4]);
        }

        [Test]
        public void SuspendDuringFlood_EmphasizeTheNeedForResetOfPerspectiveAfterSuspensionIsOver()
        {
            /******************************************************************************
             * Max = 3, Window = 5000ms, Suspend = 15000ms
             * Source -1---------2---------3-4-5-----------6-7-8---------9---------10------
             * Result -1---------2---------3-4-----------------8---------8---------10------
             *
             * As opposed to the solution above, 6 & 7 are now considered as having been swallowed/ignored
             * due to the 3-4-5 triggered suspension. 8 is now the first element after a suspension end.
             * Since the Suspend interval is higher than the Window interval, 8 cannot be in a buffer with
             * some 2 (i.e, Max - 1) preceeding values. It acts like the Max - 1 elements of the source
             * sequence's beginning. So do all of the Max - 1 values following a suspension end.
             ******************************************************************************/

            // arrange
            var scheduler = new TestScheduler();
            scheduler.AdvanceBy(TimeSpan.FromDays(1).Ticks);

            var observer = scheduler.CreateObserver<int>();

            var source = new Subject<int>();

            source.SuspendDuringFloodWithRectification(3, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(15), scheduler)
                  .Subscribe(observer);

            // act
            /******************************************************************************
             *
             * source -1---------2---------3-4-5-----------6-7-8---------9---------10------
             *
             ******************************************************************************/

            scheduler.AdvanceBy(TimeSpan.FromSeconds(2).Ticks);  // 2
            source.OnNext(1);
            scheduler.AdvanceBy(TimeSpan.FromSeconds(10).Ticks); // 12
            source.OnNext(2);
            scheduler.AdvanceBy(TimeSpan.FromSeconds(11).Ticks); // 23
            source.OnNext(3);
            scheduler.AdvanceBy(TimeSpan.FromSeconds(2).Ticks);  // 25
            source.OnNext(4);
            scheduler.AdvanceBy(TimeSpan.FromSeconds(2).Ticks);  // 27 - Start suspension until 41
            source.OnNext(5);
            scheduler.AdvanceBy(TimeSpan.FromSeconds(12).Ticks); // 39 - Suspend
            source.OnNext(6);
            scheduler.AdvanceBy(TimeSpan.FromSeconds(2).Ticks);  // 41 - Suspend
            source.OnNext(7);
            scheduler.AdvanceBy(TimeSpan.FromSeconds(2).Ticks);  // 43 - Disconsider this as being part of the 6-7-8 buffer since 6 & 7 are ignored
            source.OnNext(8);
            scheduler.AdvanceBy(TimeSpan.FromSeconds(10).Ticks); // 53
            source.OnNext(9);
            scheduler.AdvanceBy(TimeSpan.FromSeconds(10).Ticks); // 63
            source.OnNext(10);
            scheduler.AdvanceBy(1);
            source.OnCompleted();

            // verify
            var results = observer.
                Messages
                .Where(m => m.Value.Kind == NotificationKind.OnNext)
                .Select(m => m.Value.Value).ToArray();

            Assert.AreEqual(1, results[0]);
            Assert.AreEqual(2, results[1]);
            Assert.AreEqual(3, results[2]);
            Assert.AreEqual(4, results[3]);
            Assert.AreEqual(8, results[4]);
            Assert.AreEqual(9, results[5]);
            Assert.AreEqual(10, results[6]);
        }
    }
}
