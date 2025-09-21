using Microsoft.AspNetCore.Mvc;
using Domain.Routes.Contexts;
using Domain.Routes;

namespace Api.Controllers.BackGrounds
{
    [ApiController]
    [Route("background/[controller]")]
    [ApiExplorerSettings(GroupName = "background")]
    public class BaseController : ControllerBase
    {
        /// <summary>
        /// 获取客户端IP地址
        /// </summary>
        protected string GetClientIpAddress()
        {
            var forwardedFor = Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (!string.IsNullOrEmpty(forwardedFor))
            {
                var ips = forwardedFor.Split(',', StringSplitOptions.RemoveEmptyEntries);
                if (ips.Length > 0)
                {
                    return ips[0].Trim();
                }
            }

            var realIp = Request.Headers["X-Real-IP"].FirstOrDefault();
            if (!string.IsNullOrEmpty(realIp))
            {
                return realIp;
            }

            return HttpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty;
        }

        /// <summary>
        /// 创建网关请求上下文
        /// </summary>
        protected GatewayRequest CreateGatewayRequest(string path, Domain.Routes.HttpMethod httpMethod)
        {
            var clientIp = GetClientIpAddress();
            var userAgent = Request.Headers["User-Agent"].FirstOrDefault();
            var requestId = HttpContext.TraceIdentifier;

            var request = GatewayRequest.FromHttp(path, httpMethod, clientIp, userAgent)
                .WithRequestId(requestId);

            var importantHeaders = new[] { "Authorization", "Content-Type", "Accept", "Accept-Language" };
            foreach (var headerName in importantHeaders)
            {
                if (Request.Headers.TryGetValue(headerName, out var headerValue))
                {
                    request.WithHeader(headerName, headerValue.ToString());
                }
            }

            foreach (var queryParam in Request.Query)
            {
                request.WithQueryParameter(queryParam.Key, queryParam.Value.ToString());
            }

            request.WithMetadata("ControllerName", ControllerContext.ActionDescriptor.ControllerName)
                   .WithMetadata("ActionName", ControllerContext.ActionDescriptor.ActionName)
                   .WithMetadata("RemoteIpAddress", HttpContext.Connection.RemoteIpAddress?.ToString() ?? "")
                   .WithMetadata("RemotePort", HttpContext.Connection.RemotePort);

            return request;
        }

        protected static Domain.Routes.HttpMethod ConvertHttpMethod(string method)
        {
            return method.ToUpperInvariant() switch
            {
                "GET" => Domain.Routes.HttpMethod.GET,
                "POST" => Domain.Routes.HttpMethod.POST,
                "PUT" => Domain.Routes.HttpMethod.PUT,
                "DELETE" => Domain.Routes.HttpMethod.DELETE,
                "PATCH" => Domain.Routes.HttpMethod.PATCH,
                "HEAD" => Domain.Routes.HttpMethod.HEAD,
                "OPTIONS" => Domain.Routes.HttpMethod.OPTIONS,
                _ => Domain.Routes.HttpMethod.GET
            };
        }
    }
}