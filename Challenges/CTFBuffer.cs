using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace Challenges
{
    public class CTFBuffer : IObserver<int>, IObservable<IList<int>>
    {
        private const int Threshold = 6;
        private const int TimeoutMs = 500;

        private readonly List<int> _buffer = new List<int>(Threshold);
        private readonly List<IObserver<IList<int>>> _observers = new List<IObserver<IList<int>>>();
        private readonly IObservable<IList<int>> _internalObservable = new Subject<IList<int>>().AsObservable();

        private readonly IDisposable _timeoutSubscription;
        private readonly Logger _logger = LogManager.GetCurrentClassLogger();

        public CTFBuffer(IScheduler scheduler, IObservable<Unit> externalTrigger)
        {
            _timeoutSubscription = Observable.Interval(TimeSpan.FromMilliseconds(TimeoutMs), scheduler).Subscribe(
                _ =>
                {
                    _logger.Warn("Timing out");
                    BlipBuffer();
                });

            externalTrigger.Subscribe(
                _ =>
                {
                    _logger.Warn("Manually triggered blipping");
                    BlipBuffer();
                });
        }

        public void OnNext(int value)
        {
            _buffer.Add(value);

            _logger.Info($"Adding value: {value} to internal buffer, current internal counter is {_buffer.Count}, while the buffer is: {string.Join(", ", _buffer)}");
            if (_buffer.Count < Threshold) return;

            BlipBuffer();
        }

        private void BlipBuffer()
        {
            if (_buffer.Count <= 0)
            {
                _logger.Info("Empty buffer trying to be blipped");
                return;
            }

            var snapshot = _buffer.ToList();
            _logger.Info($"Blipping current buffer: {string.Join(", ", snapshot)}");
            foreach (var observer in _observers)
            {
                observer.OnNext(snapshot);
            }
            _buffer.Clear();
        }

        public void OnError(Exception error)
        {
            _logger.Error("Erroring");

            _timeoutSubscription.Dispose();

            foreach (var observer in _observers)
                observer.OnError(error);

            _buffer.Clear();
            _observers.Clear();
        }

        public void OnCompleted()
        {
            _logger.Info("Completing");

            _timeoutSubscription.Dispose();

            BlipBuffer();

            foreach (var observer in _observers)
                observer.OnCompleted();

            _observers.Clear();
        }

        public IDisposable Subscribe(IObserver<IList<int>> observer)
        {
            _observers.Add(observer);

            return _internalObservable.Subscribe(observer);
        }
    }
}
