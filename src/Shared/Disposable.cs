using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Shared
{
  public class Disposable : IDisposable
{
    Action _onDispose;
    Disposable(Action dispose)
    {
        _onDispose = dispose;
    }
    public static Disposable Create(Action dispose)
    {
        return new Disposable(dispose);
    }

    public void Dispose()
    {
        _onDispose!.Invoke();
        _onDispose = null;
    }
}
}