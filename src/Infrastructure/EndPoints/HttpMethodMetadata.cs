using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Infrastructure.Endpoints
{
    /// <summary>
    /// HTTP方法元数据
    /// 用于约束Endpoint接受的HTTP方法
    /// </summary>
    public class HttpMethodMetadata : IHttpMethodMetadata
    {
        public IReadOnlyList<string> HttpMethods { get; }

        public HttpMethodMetadata(IEnumerable<string> httpMethods)
        {
            HttpMethods = httpMethods.ToList().AsReadOnly();
        }

        public bool AcceptCorsPreflight => HttpMethods.Contains("OPTIONS");
    }
}