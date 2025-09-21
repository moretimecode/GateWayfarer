using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Domain.Services;

namespace Domain.Routes.Contexts
{
    public class RouteMatchResult
    {
        public Route Route { get; set; }
        public Destination? SelectedDestination { get; set; }
        public RouteParameters Parameters { get; set; }

        /// <summary>
        /// 匹配的服务代码
        /// </summary>
        public string ServiceCode => Route.ServiceCode;
    }
}