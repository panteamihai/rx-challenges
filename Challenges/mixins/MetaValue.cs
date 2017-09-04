using System;

namespace Challenges
{
    public class MetaValue<T>
    {
        public static MetaValue<T> Empty { get; } = new MetaValue<T>(default(T), DateTimeOffset.MinValue, Emit.None);

        public T Value { get; }

        public DateTimeOffset Timestamp { get; }

        public Emit Kind { get; }

        public MetaValue(T value, DateTimeOffset timestamp, Emit kind)
        {
            Value = value;
            Timestamp = timestamp;
            Kind = kind;
        }

        public override string ToString()
        {
            return $"({Value}, {Kind}) @ {Timestamp.DateTime.TimeOfDay.TotalSeconds}s";
        }
    }
}