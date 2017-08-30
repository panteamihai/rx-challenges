using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;

namespace Challenges
{
    class EnumerableToObservable
    {
        private static void GetValue()
        {
            var input = GetConsoleLines().ToObservable().TakeWhile(line => !string.IsNullOrEmpty(line));
            input.GroupBy(word => word)
                .Select(grouping => grouping.Zip(Observable.Range(1, 3), (s, i) => new Tuple<string, int>(s, i))).Merge()
                .Subscribe(r => Console.WriteLine($"you typed {r.Item1} {r.Item2} times"));
        }

        public static IEnumerable<string> GetConsoleLines()
        {
            while (true)
                yield return Console.ReadLine();
        }
    }
}
