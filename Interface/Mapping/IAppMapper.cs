using Domain.Entities;
using DTO.Auth;
using DTO.Catalogo;
using DTO.ContenidoCatalogo;
using DTO.FormField;
using DTO.League;
using DTO.Quiniela;
using DTO.User;

namespace Interface.Mapping;

public interface IAppMapper
{
    // User
    User ToEntity(UserDTO dto);
    UserDTO ToDTO(User entity);
    UserProfileDto ToProfileDTO(User entity);
    List<UserDTO> ToDTOList(IEnumerable<User> entities);

    // Catalogo
    Catalogo ToEntity(CatalogoDTO dto);
    CatalogoDTO ToDTO(Catalogo entity);
    List<CatalogoDTO> ToDTOList(IEnumerable<Catalogo> entities);

    // ContenidoCatalogo
    ContenidoCatalogo ToEntity(ContenidoCatalogoDTO dto);
    ContenidoCatalogoDTO ToDTO(ContenidoCatalogo entity);
    List<ContenidoCatalogoDTO> ToDTOList(IEnumerable<ContenidoCatalogo> entities);

    // FormField
    FormField ToEntity(FormFieldDTO dto);
    FormFieldDTO ToDTO(FormField entity);
    List<FormFieldDTO> ToDTOList(IEnumerable<FormField> entities);

    // Quiniela
    QuinielaResponseDto ToResponseDTO(Quiniela entity);
    List<QuinielaResponseDto> ToResponseDTOList(IEnumerable<Quiniela> entities);
    QuinielaDetailDto ToDetailDTO(Quiniela entity);

    // QuinielaMember
    QuinielaMemberDto ToMemberDTO(QuinielaMember entity);
    List<QuinielaMemberDto> ToMemberDTOList(IEnumerable<QuinielaMember> entities);

    // League
    LeagueDto ToLeagueDTO(League entity);
    List<LeagueDto> ToLeagueDTOList(IEnumerable<League> entities);

    // Typed convenience Map<TDestination> for seamless compatibility
    TDestination Map<TDestination>(object? source);
}
