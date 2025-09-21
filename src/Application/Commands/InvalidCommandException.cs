using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Commands
{
    public class InvalidCommandException(string details, string paraName) : Exception(details)
    {
        public int ErrorCode => Shared.ErrorCode.InvalidArgument;
        public string Details { get; } = details;
        public string ParaName { get; } = paraName;
    }

}
