using AutoMapper;
using Domain.Routes;
using Domain.Routes.Configuration;

namespace Infrastructure.Routes
{
 public class RouteConfigurationProfile : Profile
    {
        public RouteConfigurationProfile()
        {
              CreateMap<List<Domain.Routes.Route>, Domain.Routes.Configuration.RouteOptions>()
                .ForMember(dest => dest.Routes, opt => opt.MapFrom(src => src));
                
            CreateMap<Domain.Routes.Route, RouteConfigItem>();
        }
    }
}