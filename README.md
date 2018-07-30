# Solutions to Bnaya Eshet's RX Challenges

### RX Challenge #1
[Definition](http://blogs.microsoft.co.il/bnaya/2015/01/27/rx-challenge/): Create a buffer that should produce its bulk as a result of one of the following triggers: timeout, threshold reached or a manual flush request.

[Naive Solution](https://github.com/panteamihai/rx-challenges/blob/master/Challenges/CTFBuffer.cs): The `CTFBuffer` (**C**ount, **T**imeout, **F**lush)

[Solution #1](https://github.com/panteamihai/rx-challenges/blob/master/Challenges/Program.cs#L72): Use the `Buffer` overload that takes an observable as a sequence closing selector.

[Solution #2](https://github.com/panteamihai/rx-challenges/blob/master/Challenges/Program.cs#L127): Reimplement the above `Buffer` overload using `Window` and `Aggregate`. Also filter out empty buffers.

[Solution #3](https://github.com/panteamihai/rx-challenges/blob/master/Challenges/Program.cs#L205): One issue with the previous implementations is that we will always produce a buffer (possibly an empty one) after the time threshold is reached (because the timeout observable is a cyclical emmiter with its origin at 0 timewise). Moreover, every X (a.k.a, count threshold) items will produce a buffer, regardless of whether or not the buffer being produced actually holds X items, spooky I know. The simple explanation is that we don't reset **a hypothetical internal flag** that would indicate that you need X items to produce a buffer after a timeout has produced one already. But instead we produce buffers timewise or countwise without considering that other triggers might have just produced a buffer independently of the current trigger. That is why the naming of the previous tests links the triggers with and *And* instead of an *Or*. The third solution tries to fix this by suggesting that each buffer is produced by the winner of applying `Amb` to the trigger observables, then recursively applying `Amb` again. This effectively resets the timeout and count (inner buffer) after each buffer emit.

### RX Challenge #2
[Definition](blogs.microsoft.co.il/bnaya/2015/02/05/rx-challenge-2/): Avoid overlaps of multiple emitting streams.

[Solution](https://github.com/panteamihai/rx-challenges/blob/master/Challenges/Program.cs#L281): Use the `Switch` operator

### RX Challenge #3
[Definition](http://blogs.microsoft.co.il/bnaya/2015/03/06/rx-challenge-3/): Add flood protection to an observable. A flood is defined as an event which starts when more than X elements are emitted by the source observable during an Y interval. Should this arise, the source's values should be ignored for a Z interval (suspend), including the value that triggered the suspension.

[Solution #1](https://github.com/panteamihai/rx-challenges/blob/master/Challenges/mixins/ObservableMixins.cs#L16): Use `Buffer` to determine which value emitting events should introduce a suspend, then use `Scan` to run through the sequence with a possible suspend window in mind. **Suspensions introduced by the values of the source observable should be cumulated.** [More details](https://github.com/panteamihai/rx-challenges/blob/master/Challenges/Program.cs#L345)

[Solution #2](https://github.com/panteamihai/rx-challenges/blob/master/Challenges/mixins/ObservableMixins.cs#L69): Use `Buffer` to determine which value emitting events should introduce a suspend, then use `Scan` to run through the sequence with a possible suspend window in mind. **Elements that were ignored during a suspension should not be considered as contributing to introducing a subsequent suspension.** [More details](https://github.com/panteamihai/rx-challenges/blob/master/Challenges/Program.cs#L409)