using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared
{
    public class ErrorCode
    {
        /// <summary>
        /// 默认错误
        /// </summary>
        public const int InvalidArgument = 1000;

        /// <summary>
        /// 路由格式不匹配
        /// </summary> <summary>
        public const int RouteNameDuplicate = 1001;
        public const int RouteNotFound = 1002;
        public const int RouteConflict = 1003;
        public const int InvalidRoutePathFormat = 1004;
        public const int InvalidHttpMethods = 1005;
        public const int InvalidDestinationFormat = 1006;
        public const int NoHealthyDestination = 1007;
        public const int RouteNameEmpty = 1008;
        public const int RoutePathEmpty = 1009;
        public const int RouteIdInvalid = 1010;
    }
}
