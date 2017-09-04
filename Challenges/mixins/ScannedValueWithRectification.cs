using System;

namespace Challenges
{
    public class ScannedValueWithRectification<T> : ScannedValue<T>
    {
        public new static ScannedValueWithRectification<T> Empty { get; } = new ScannedValueWithRectification<T>(MetaValue<T>.Empty, DateTimeOffset.MinValue, 0);

        public int RemainingNumberOfValuesToRectify { get; }

        public ScannedValueWithRectification(MetaValue<T> metaValue, DateTimeOffset startOfSuspendWindow, int remainingNumberOfValuesToRectify)
            : base(metaValue, startOfSuspendWindow)
        {
            RemainingNumberOfValuesToRectify = remainingNumberOfValuesToRectify;
        }

        public override string ToString()
        {
            return StartOfSuspendWindow != DateTimeOffset.MinValue && RemainingNumberOfValuesToRectify > 0
                ? $"Currently in suspend, swallowing what should be null: {MetaValue}"
                : base.ToString();
        }
    }
}