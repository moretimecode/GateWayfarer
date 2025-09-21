namespace Domain.Routes;

[Flags]
public enum HttpMethod
{
    None = 0,
    GET = 1,
    POST = 2,
    PUT = 4,
    DELETE = 8,
    PATCH = 16,
    HEAD = 32,
    OPTIONS = 64,
    ALL = GET | POST | PUT | DELETE | PATCH | HEAD | OPTIONS
}