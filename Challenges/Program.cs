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
    class Program { static void Main() { } }

    class RxChallengesTests
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
    }
}
