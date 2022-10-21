using Orleans;

namespace Application
{
    public class HelloGrain : Grain, IHelloGrain
    {
        int count;

        public Task<string> SayHello(string greeting) => Task.FromResult($"Hello, {greeting} {++count}!");
    }
}
