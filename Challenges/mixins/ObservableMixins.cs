using Microsoft.Reactive.Testing;
using System;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;

namespace Challenges
{
    public static class ObservableMixins
    {
        /// <summary>
        /// Continously monitors the underlying observable and emits suspensions of duration <param name="suspendDuration" />
        /// whenever the number of <param name="maxElementsPerWindow" /> is exceeded within a window of duration <param name="windowDuration" />
        /// It stiches together overlapping suspensions
        /// </summary>
        public static IObservable<T> SuspendDuringFlood<T>(
            this IObservable<T> source,
            int maxElementsPerWindow,
            TimeSpan windowDuration,
            TimeSpan suspendDuration,
            IScheduler scheduler)
        {
            scheduler = scheduler ?? new TestScheduler();

            var internalSource = source.Timestamp(scheduler).Select(i => new MetaValue<T>(i.Value, i.Timestamp, Emit.Value));

            // The first "max-1" elements will always be emitted (no need to enrich them)
            var head = source.Take(maxElementsPerWindow - 1);

            // Enrich the rest of the elements with information about the absolute time they were emitted (see ... below)
            var tail = internalSource
                // Apply a buffer in order to determine if the last element in the buffer (the element of importance) is supposed to introduce a suspension
                .Buffer(maxElementsPerWindow, 1)
                // The last "max-1" buffers are incomplete and can be safely ignored, because the last buffer of size "max" contains that last element in the observable
                .Where(b => b.Count == maxElementsPerWindow)
                .Select(b =>
                {
                    var lastInBuffer = b.Last();
                    var firstInBuffer = b.First();

                    var diff = lastInBuffer.Timestamp.Subtract(firstInBuffer.Timestamp);
                //... also enrich the value with information about whether or not it is supposed to introduce a suspension
                // (based on the time difference to the first element in the buffer)
                    return new MetaValue<T>(lastInBuffer.Value, lastInBuffer.Timestamp, diff <= windowDuration ? Emit.Suspend : Emit.Value);
                });

            // Run through the meta values produced and ignore the ones emitted during the suspension intervals (which are considered to be stiched together when overlapping)
            var suspendedTail = tail.Scan(
                    ScannedValue<T>.Empty,
                    (accumulator, metaValue) =>
                    {
                        if (accumulator.StartOfSuspendWindow != DateTimeOffset.MinValue)
                        {
                            var endOfSuspend = accumulator.StartOfSuspendWindow.Add(suspendDuration);
                            if (metaValue.Timestamp < endOfSuspend) // Ignore (return null) values emitted during an ongoing suspend window
                                return new ScannedValue<T>(null, accumulator.StartOfSuspendWindow);
                        }

                        return metaValue.Kind == Emit.Suspend
                            ? new ScannedValue<T>(null, metaValue.Timestamp) // Begin a suspend window without emitting the value that triggered it
                            : new ScannedValue<T>(metaValue, DateTimeOffset.MinValue); // Emit a value, effectively resetting a suspend window as well
                    })
                .Where(sv => sv.MetaValue != null) // This is the part that effectively actually ignores the values under a suspend window
                .Select(sv => sv.MetaValue.Value);

            return head.Merge(suspendedTail);
        }

        public static IObservable<T> SuspendDuringFloodWithRectification<T>(
            this IObservable<T> source,
            int maxElementsPerWindow,
            TimeSpan windowDuration,
            TimeSpan suspendDuration,
            IScheduler scheduler)
        {
            scheduler = scheduler ?? new TestScheduler();

            var internalSource = source.Timestamp(scheduler).Select(i => new MetaValue<T>(i.Value, i.Timestamp, Emit.Value));

            // The first max-1 elements will always be emitted (no need to enrich them)
            var head = source.Take(maxElementsPerWindow - 1);

            // Enrich the rest of the elements with information about the absolute time they were emitted (see ... below)
            var tail = internalSource
                // Apply a buffer in order to determine if the last element in the buffer (the element of importance) is supposed to introduce a suspension
                .Buffer(maxElementsPerWindow, 1)
                // The last max-1 buffers are incomplete and can be safely ignored, because the last buffer of size max contains that last element in the observable
                .Where(b => b.Count == maxElementsPerWindow)
                .Select(b =>
                {
                    var lastInBuffer = b.Last();
                    var firstInBuffer = b.First();

                    var diff = lastInBuffer.Timestamp.Subtract(firstInBuffer.Timestamp);
                    //... also enrich the value with information about whether or not it is supposed to introduce a suspension
                    // (based on the time difference to the first element in the buffer)
                    return new MetaValue<T>(lastInBuffer.Value, lastInBuffer.Timestamp, diff <= windowDuration ? Emit.Suspend : Emit.Value);
                });

            // Run through the meta values produced and ignore the ones emitted during a suspend,
            // while rectifing (emitting) the ones wrongly identified as suspended (caused by them being in buffers with values that should have been ignored)
            var rectifiedTail = tail.Scan(
                    ScannedValueWithRectification<T>.Empty,
                    (accumulator, metaValue) =>
                    {
                        if (accumulator.StartOfSuspendWindow != DateTimeOffset.MinValue)
                        {
                            var endOfSuspend = accumulator.StartOfSuspendWindow.Add(suspendDuration);
                            if (metaValue.Timestamp < endOfSuspend) // Ignore (return null) values emitted during an ongoing suspend window
                                return new ScannedValueWithRectification<T>(
                                    null,
                                    accumulator.StartOfSuspendWindow,
                                    accumulator.RemainingNumberOfValuesToRectify);
                        }

                        if (accumulator.RemainingNumberOfValuesToRectify > 0)
                        {
                            // Rectify the first max-1 values after a resume to Emit.Value. There is a philosofical reason for this: if one value
                            // (for which there is always a buffer in which it is the last value) is deemed as introducing a suspension due
                            // to values from the original source that ended up being ignored because they were emitted during an active suspension,
                            // I don't find it logical for this value to still be considered as introducing a suspension. It should be considered as
                            // the 1st value emitted after a suspend, and because suspendDuration >> windowDuration it should always emit a value, same goes
                            // for all max-1 values that follow an end of a suspension interval.
                            var rectifiedCurrent = new MetaValue<T>(metaValue.Value, metaValue.Timestamp, Emit.Value);
                            // Preserve the information of the suspend window until all necessary values have been rectified
                            return new ScannedValueWithRectification<T>(
                                rectifiedCurrent,
                                accumulator.StartOfSuspendWindow,
                                accumulator.RemainingNumberOfValuesToRectify - 1);
                        }

                        return metaValue.Kind == Emit.Suspend
                            ? new ScannedValueWithRectification<T>(null, metaValue.Timestamp, maxElementsPerWindow - 1) // Begin a suspend window without emitting the value that triggered it
                            : new ScannedValueWithRectification<T>(metaValue, DateTimeOffset.MinValue, 0); // Emit a value
                    })
                .Where(sv => sv.MetaValue != null) // This is the part that effectively actually ignores the values under a suspend window
                .Select(ev => ev.MetaValue.Value);

            return head.Merge(rectifiedTail);
        }

    }
}
