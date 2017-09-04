using System;

namespace Challenges
{
    public class ScannedValue<T>
    {
        public static ScannedValue<T> Empty { get; } = new ScannedValue<T>(MetaValue<T>.Empty, DateTimeOffset.MinValue);

        public MetaValue<T> MetaValue { get; }

        public DateTimeOffset StartOfSuspendWindow { get; }

        public ScannedValue(MetaValue<T> metaValue, DateTimeOffset startOfSuspendWindow)
        {
            MetaValue = metaValue;
            StartOfSuspendWindow = startOfSuspendWindow;
        }

        public override string ToString()
        {
            return StartOfSuspendWindow != DateTimeOffset.MinValue
                ? $"Currently in suspend, swallowing what should be null: {MetaValue}"
                : $"Emitting {MetaValue}";
        }
    }
}