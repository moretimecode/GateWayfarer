using AutoMapper;
using Domain.Routes;
using Domain.Routes.Configuration;

namespace Infrastructure.Routes
{
 public class RouteConfigurationProfile : Profile
    {
        public RouteConfigurationProfile()
        {
              CreateMap<List<Route>, RouteOptions>()
                .ForMember(dest => dest.Routes, opt => opt.MapFrom(src => src));
                
            CreateMap<Route, RouteConfigItem>();
        }
    }
}