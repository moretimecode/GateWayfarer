using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Droplet.Data.Entities;

namespace Domain.Routes.Rules
{
public class EnsureValidHttpMethods : IBusinessRule
{
    private readonly HttpMethod _httpMethods;

    public EnsureValidHttpMethods(HttpMethod httpMethods)
    {
        _httpMethods = httpMethods;
    }

    public string ErrorMessage => "Invalid HTTP methods specified.";
    public int ErrorCode =>Shared.ErrorCode.InvalidHttpMethods;

    public bool IsBroken()
    {
        return _httpMethods == HttpMethod.None;
    }
}
}