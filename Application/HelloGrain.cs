using Orleans;

namespace Application
{
    public class HelloGrain : Grain, IHelloGrain
    {
        public Task<string> SayHello(string greeting) => Task.FromResult($"Hello, {greeting}!");
    }
}
