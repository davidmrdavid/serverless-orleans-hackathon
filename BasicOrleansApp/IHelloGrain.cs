using Orleans;

namespace Silo
{
    public interface IHelloGrain : IGrainWithStringKey
    {
        Task<string> SayHello(string greeting);
    }
}