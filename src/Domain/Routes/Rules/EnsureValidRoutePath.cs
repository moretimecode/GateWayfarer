using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Droplet.Data.Entities;

namespace Domain.Routes.Rules
{
    public class EnsureValidRoutePath : IBusinessRule
    {
        private readonly string _path;
        private readonly RouteMatchType _matchType;

        public EnsureValidRoutePath(string path, RouteMatchType matchType)
        {
            _path = path;
            _matchType = matchType;
        }

        public string ErrorMessage => $"Invalid route path format: '{_path}' for match type '{_matchType}'.";
        public int ErrorCode => Shared.ErrorCode.InvalidRoutePathFormat;

        public bool IsBroken()
        {
            return _matchType switch
            {
                RouteMatchType.Exact => !IsValidExactPath(_path),
                RouteMatchType.Prefix => !IsValidPrefixPath(_path),
                RouteMatchType.Regex => !IsValidRegexPath(_path),
                RouteMatchType.Wildcard => !IsValidWildcardPath(_path),
                _ => true
            };
        }

        private bool IsValidExactPath(string path) =>
            !string.IsNullOrWhiteSpace(path) && path.StartsWith("/");

        private bool IsValidPrefixPath(string path) =>
            !string.IsNullOrWhiteSpace(path) && path.StartsWith("/");

        private bool IsValidRegexPath(string path)
        {
            try { _ = new Regex(path); return true; }
            catch { return false; }
        }

        private bool IsValidWildcardPath(string path) =>
            !string.IsNullOrWhiteSpace(path) && path.StartsWith("/") &&
            path.All(c => char.IsLetterOrDigit(c) || "/-_*?".Contains(c));
    }
}