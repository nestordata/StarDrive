using System;
using Ship_Game.Platform;

namespace Ship_Game
{
    public sealed class SingleGlobalInstance : IDisposable
    {
        readonly ISingleInstanceLock Lock;
        public readonly bool UniqueInstance;

        public SingleGlobalInstance()
        {
            Lock = PlatformServices.CreateSingleInstanceLock();
            UniqueInstance = Lock.UniqueInstance;
        }

        public void Dispose()
        {
            Lock.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
