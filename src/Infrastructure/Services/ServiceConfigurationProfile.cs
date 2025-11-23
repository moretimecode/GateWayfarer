using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Domain.Services;
using Domain.Services.Configuration;

namespace Infrastructure.Services
{
    public class ServiceConfigurationProfile : Profile
    {
        public ServiceConfigurationProfile()
        {
            CreateMap<List<Service>, ServiceOptions>()
              .ForMember(dest => dest.Services, opt => opt.MapFrom(src => src));
            CreateMap<Service, ServiceConfigItem>();
            CreateMap<HealthCheckConfig, HealthCheckConfigItem>();
            CreateMap<Destination, DestinationConfigItem>();
            CreateMap<ServiceProtocolConfig, ProtocolConfigItem>()
            .ForMember(dest => dest.TimeoutSeconds, opt => opt.MapFrom(src => src.Timeout));
        }
    }
}