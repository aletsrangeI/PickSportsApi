using AutoMapper;
using Domain.Entities;
using DTO.Catalogo;
using DTO.ContenidoCatalogo;
using DTO.EquipoLiga;
using DTO.FormField;
using DTO.User;

namespace UseCases.Common.Mapping;

public class MappingsProfile : Profile
{
    public MappingsProfile()
    {
        CreateMap<User, UserDTO>().ReverseMap();
        CreateMap<Catalogo, CatalogoDTO>().ReverseMap();
        CreateMap<ContenidoCatalogo, ContenidoCatalogoDTO>().ReverseMap();
        CreateMap<EquipoLiga, EquipoLigaDTO>().ReverseMap();
        CreateMap<FormField, FormFieldDTO>().ReverseMap();
    }
}