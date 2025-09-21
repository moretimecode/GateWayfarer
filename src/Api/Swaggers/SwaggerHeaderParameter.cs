using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Api.Swaggers
{
    public class SwaggerHeaderParameter : IOperationFilter
    {
        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            if (context.ApiDescription.GroupName == "crm")
            {
                operation.Parameters.Add(new OpenApiParameter
                {
                    Name = "managerId",
                    Description = "操作人Id(用于调试)",
                    In = ParameterLocation.Header,
                    Required = false
                });
                operation.Parameters.Add(new OpenApiParameter
                {
                    Name = "tenant",
                    Description = "租户id(用于调试)",
                    In = ParameterLocation.Header,
                    Required = false
                });
            }

            if (context.ApiDescription.GroupName == "user")
            {
                operation.Parameters.Add(new OpenApiParameter
                {
                    Name = "uid",
                    Description = "用户Id（用于调试）",
                    In = ParameterLocation.Header,
                    Required = false
                });
                operation.Parameters.Add(new OpenApiParameter
                {
                    Name = "userInfo",
                    Description = "用户信息 格式：Email|Nation|Mobile|UserRole 如：123@qq.com|86|18152365425|1（用于调试）",
                    In = ParameterLocation.Header,
                    Required = false
                });
            }
        }
    }
}
