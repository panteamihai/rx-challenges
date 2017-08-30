using System;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace Challenges
{
    public class StockPriceVariation
    {
        internal struct StockInfo
        {
            public string Symbol { get; }

            public decimal Price { get; }

            public StockInfo(string symbol, decimal price)
            {
                Symbol = symbol;
                Price = price;
            }

            public override string ToString()
            {
                return $"{Symbol} @ ${Price}";
            }
        }

        public void AnalyzeStockPriceVariation()
        {
            Subject<StockInfo> ticks = new Subject<StockInfo>();

            ticks.Subscribe(si => Console.WriteLine($"{DateTime.Now.ToString("HH:mm:ss:fff")}| Blipped {si}"));

            ticks.GroupBy(si => si.Symbol)
                .Select(g => g.Buffer(2, 1)
                    .Where(si => 1.1m * si.First().Price <= si.Last().Price))
                .Merge()
                .Subscribe(buff => Console.WriteLine($"Got {buff.First().Symbol} with {buff.First().Price} - {buff.Last().Price}"));

            ticks.OnNext(new StockInfo("MSFT", 22.1m));
            ticks.OnNext(new StockInfo("MSFT", 21.9m));
            ticks.OnNext(new StockInfo("MSFT", 23.5m));
            ticks.OnNext(new StockInfo("MSFT", 27.9m));
            ticks.OnNext(new StockInfo("MSFT", 21.3m));
            ticks.OnNext(new StockInfo("INTL", 42.1m));
            ticks.OnNext(new StockInfo("MSFT", 29.1m));
            ticks.OnNext(new StockInfo("INTL", 42.1m));
            ticks.OnNext(new StockInfo("MSFT", 22.2m));
            ticks.OnNext(new StockInfo("INTL", 48.1m));
            ticks.OnNext(new StockInfo("INTL", 49.2m));
            ticks.OnNext(new StockInfo("MSFT", 22.1m));
            ticks.OnNext(new StockInfo("INTL", 44.5m));
            ticks.OnNext(new StockInfo("INTL", 43.3m));
            ticks.OnNext(new StockInfo("INTL", 41.2m));
            ticks.OnNext(new StockInfo("MSFT", 26.1m));
            ticks.OnNext(new StockInfo("MSFT", 24.3m));
            ticks.OnNext(new StockInfo("INTL", 49.1m));
            ticks.OnNext(new StockInfo("MSFT", 21.1m));

            Console.ReadKey();
        }
    }
}
