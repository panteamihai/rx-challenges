# Solutions to Bnaya Eshet's RX Challenges

### RX Challenge #1
[Definition](http://blogs.microsoft.co.il/bnaya/2015/01/27/rx-challenge/): Create a buffer that should produce its bulk as a result of one of the following triggers: timeout, threshold reached or manual flush request (i.e. completion)

[Naive Solution](https://github.com/panteamihai/rx-challenges/blob/master/Challenges/CTFBuffer.cs): The CTFBuffer

[Provided Solution](https://github.com/panteamihai/rx-challenges/blob/master/Challenges/Program.cs#L82): Use the Buffer overload that uses an observable as a sequence closing selector)

### RX Challenge #2
[Definition](blogs.microsoft.co.il/bnaya/2015/02/05/rx-challenge-2/): Avoid overlaps of multiple emitting streams.

[Solution](https://github.com/panteamihai/rx-challenges/blob/master/Challenges/Program.cs#L163): Use the [Switch](http://www.introtorx.com/Content/v1.0.10621.0/12_CombiningSequences.html#Switch) operator