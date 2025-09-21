using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Droplet.Data.Entities;

namespace Domain.Routes.Rules
{
public class EnsureValidDestinationFormat : IBusinessRule
{
    private readonly string _host;

    public EnsureValidDestinationFormat(string host)
    {
        _host = host;
    }

    public string ErrorMessage => $"Invalid destination host format: '{_host}'.";
    public int ErrorCode => Shared.ErrorCode.InvalidDestinationFormat;

    public bool IsBroken()
    {
        if (string.IsNullOrWhiteSpace(_host))
            return true;

        return !Uri.TryCreate(_host, UriKind.Absolute, out var uri) || 
               (uri.Scheme != "http" && uri.Scheme != "https");
    }
}
}