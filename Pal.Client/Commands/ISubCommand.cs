using System;
using System.Collections.Generic;

namespace Pal.Client.Commands
{
    public interface ISubCommand
    {
        IReadOnlyDictionary<string, Action<string>> GetHandlers();
    }
}
