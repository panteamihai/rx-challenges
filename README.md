# Solutions to Bnaya Eshet's RX Challenges

### RX Challenge #1
[Definition](http://blogs.microsoft.co.il/bnaya/2015/01/27/rx-challenge/): Create a buffer that should produce its bulk as a result of one of the following triggers: timeout, threshold reached or manual flush request (i.e. completion)

[Naive Solution](https://github.com/panteamihai/rx-challenges/blob/master/Challenges/CTFBuffer.cs): The CTFBuffer

[Provided Solution](https://github.com/panteamihai/rx-challenges/blob/master/Challenges/Program.cs#L82): Use the Buffer overload that uses an observable as a sequence closing selector)

### RX Challenge #2
[Definition](blogs.microsoft.co.il/bnaya/2015/02/05/rx-challenge-2/): Avoid overlaps of multiple emitting streams.

[Solution](https://github.com/panteamihai/rx-challenges/blob/master/Challenges/Program.cs#L163): Use the [Switch](http://www.introtorx.com/Content/v1.0.10621.0/12_CombiningSequences.html#Switch) operator

### RX Challenge #3
[Definition](http://blogs.microsoft.co.il/bnaya/2015/03/06/rx-challenge-3/): Add flood protection to an observable. A flood is defined as an event which starts when more than X elements are emitted by the source observable during an Y interval. Should this arise, the source's values should be ignored for a Z interval (suspend), including the value that triggered the suspension.

[Solution #1](https://github.com/panteamihai/rx-challenges/blob/master/Challenges/mixins/ObservableMixins.cs#L16): Use the [Buffer](http://reactivex.io/documentation/operators/buffer.html) operator to determine which value emitting events should introduce a suspend, then use the [Scan](http://reactivex.io/documentation/operators/scan.html) operator to run through the sequence with a possible suspend window in mind. **Suspensions introduced by the values of the source observable should be cumulated. [More details](https://github.com/panteamihai/rx-challenges/blob/master/Challenges/Program.cs#L319)**

[Solution #2](https://github.com/panteamihai/rx-challenges/blob/master/Challenges/mixins/ObservableMixins.cs#L69): Use the [Buffer](http://reactivex.io/documentation/operators/buffer.html) operator to determine which value emitting events should introduce a suspend, then use the [Scan](http://reactivex.io/documentation/operators/scan.html) operator to run through the sequence with a possible suspend window in mind. **Elements that were ignored during a suspension should not be considered as contributing to introducing a subsequent suspension. [More details](https://github.com/panteamihai/rx-challenges/blob/master/Challenges/Program.cs#L385)**