using Domain.Entities;
using DTO.Auth;
using DTO.Catalogo;
using DTO.ContenidoCatalogo;
using DTO.FormField;
using DTO.League;
using DTO.Quiniela;
using DTO.User;
using Interface.Mapping;
using Riok.Mapperly.Abstractions;

namespace UseCases.Common.Mapping;

[Mapper]
public partial class AppMapper : IAppMapper
{
    // User
    public partial User ToEntity(UserDTO dto);
    public partial UserDTO ToDTO(User entity);
    public partial UserProfileDto ToProfileDTO(User entity);
    public partial List<UserDTO> ToDTOList(IEnumerable<User> entities);

    // Catalogo
    public partial Catalogo ToEntity(CatalogoDTO dto);
    public partial CatalogoDTO ToDTO(Catalogo entity);
    public partial List<CatalogoDTO> ToDTOList(IEnumerable<Catalogo> entities);

    // ContenidoCatalogo
    public partial ContenidoCatalogo ToEntity(ContenidoCatalogoDTO dto);
    public partial ContenidoCatalogoDTO ToDTO(ContenidoCatalogo entity);
    public partial List<ContenidoCatalogoDTO> ToDTOList(IEnumerable<ContenidoCatalogo> entities);

    // FormField
    public partial FormField ToEntity(FormFieldDTO dto);
    public partial FormFieldDTO ToDTO(FormField entity);
    public partial List<FormFieldDTO> ToDTOList(IEnumerable<FormField> entities);

    // QuinielaMember
    public QuinielaMemberDto ToMemberDTO(QuinielaMember entity)
    {
        if (entity == null) return null!;
        return new QuinielaMemberDto
        {
            Id = entity.Id,
            QuinielaId = entity.QuinielaId,
            UserId = entity.UserId,
            Alias = entity.Alias,
            Role = entity.Role,
            PaidFee = entity.PaidFee,
            TotalHits = entity.TotalHits,
            TotalUpsets = entity.TotalUpsets,
            TotalHumillaciones = entity.TotalHumillaciones,
            CurrentStreak = entity.CurrentStreak,
            BestStreak = entity.BestStreak,
            JoinedAt = entity.JoinedAt,
            DisplayName = entity.User != null ? entity.User.DisplayName : null,
            AvatarUrl = entity.User != null ? entity.User.AvatarUrl : null
        };
    }

    public List<QuinielaMemberDto> ToMemberDTOList(IEnumerable<QuinielaMember> entities)
    {
        if (entities == null) return new List<QuinielaMemberDto>();
        return entities.Select(ToMemberDTO).ToList();
    }

    // Quiniela custom mappings
    public QuinielaResponseDto ToResponseDTO(Quiniela entity)
    {
        if (entity == null) return null!;
        return new QuinielaResponseDto
        {
            Id = entity.Id,
            Name = entity.Name,
            Description = entity.Description,
            LeagueId = entity.LeagueId,
            LeagueCode = entity.League != null ? entity.League.Code : string.Empty,
            LeagueName = entity.League != null ? entity.League.Name : string.Empty,
            SportName = entity.League?.Sport != null ? entity.League.Sport.Name : string.Empty,
            InviteCode = entity.InviteCode,
            EntryFee = entity.EntryFee,
            FirstPlacePct = entity.FirstPlacePct,
            SecondPlacePct = entity.SecondPlacePct,
            ThirdPlacePct = entity.ThirdPlacePct,
            IsActive = entity.IsActive,
            OwnerId = entity.OwnerId,
            OwnerName = entity.Owner != null ? (entity.Owner.DisplayName ?? entity.Owner.Username) : string.Empty,
            MembersCount = entity.Members != null ? entity.Members.Count(m => m.Active) : 0,
            Created = entity.Created
        };
    }

    public List<QuinielaResponseDto> ToResponseDTOList(IEnumerable<Quiniela> entities)
    {
        if (entities == null) return new List<QuinielaResponseDto>();
        return entities.Select(ToResponseDTO).ToList();
    }

    public QuinielaDetailDto ToDetailDTO(Quiniela entity)
    {
        if (entity == null) return null!;
        var baseDto = ToResponseDTO(entity);
        return new QuinielaDetailDto
        {
            Id = baseDto.Id,
            Name = baseDto.Name,
            Description = baseDto.Description,
            LeagueId = baseDto.LeagueId,
            LeagueCode = baseDto.LeagueCode,
            LeagueName = baseDto.LeagueName,
            SportName = baseDto.SportName,
            InviteCode = baseDto.InviteCode,
            EntryFee = baseDto.EntryFee,
            FirstPlacePct = baseDto.FirstPlacePct,
            SecondPlacePct = baseDto.SecondPlacePct,
            ThirdPlacePct = baseDto.ThirdPlacePct,
            IsActive = baseDto.IsActive,
            OwnerId = baseDto.OwnerId,
            OwnerName = baseDto.OwnerName,
            MembersCount = baseDto.MembersCount,
            Created = baseDto.Created,
            Members = entity.Members != null
                ? entity.Members.Where(m => m.Active).Select(ToMemberDTO).ToList()
                : new List<QuinielaMemberDto>()
        };
    }

    // League custom mappings
    public LeagueDto ToLeagueDTO(League entity)
    {
        if (entity == null) return null!;
        return new LeagueDto
        {
            Id = entity.Id,
            SportId = entity.SportId,
            SportName = entity.Sport != null ? entity.Sport.Name : string.Empty,
            Code = entity.Code,
            Name = entity.Name,
            Country = entity.Country,
            LogoUrl = entity.LogoUrl,
            Active = entity.Active
        };
    }

    public List<LeagueDto> ToLeagueDTOList(IEnumerable<League> entities)
    {
        if (entities == null) return new List<LeagueDto>();
        return entities.Select(ToLeagueDTO).ToList();
    }

    // Typed convenience Map<TDestination> dispatch method
    public TDestination Map<TDestination>(object? source)
    {
        if (source == null) return default!;

        // User
        if (typeof(TDestination) == typeof(User) && source is UserDTO uDto) return (TDestination)(object)ToEntity(uDto);
        if (typeof(TDestination) == typeof(UserDTO) && source is User u) return (TDestination)(object)ToDTO(u);
        if (typeof(TDestination) == typeof(UserProfileDto) && source is User uProf) return (TDestination)(object)ToProfileDTO(uProf);
        if (typeof(TDestination) == typeof(IEnumerable<UserDTO>) && source is IEnumerable<User> uList) return (TDestination)(object)ToDTOList(uList);
        if (typeof(TDestination) == typeof(List<UserDTO>) && source is IEnumerable<User> uList2) return (TDestination)(object)ToDTOList(uList2);

        // Catalogo
        if (typeof(TDestination) == typeof(Catalogo) && source is CatalogoDTO cDto) return (TDestination)(object)ToEntity(cDto);
        if (typeof(TDestination) == typeof(CatalogoDTO) && source is Catalogo c) return (TDestination)(object)ToDTO(c);
        if (typeof(TDestination) == typeof(IEnumerable<CatalogoDTO>) && source is IEnumerable<Catalogo> cList) return (TDestination)(object)ToDTOList(cList);
        if (typeof(TDestination) == typeof(List<CatalogoDTO>) && source is IEnumerable<Catalogo> cList2) return (TDestination)(object)ToDTOList(cList2);

        // ContenidoCatalogo
        if (typeof(TDestination) == typeof(ContenidoCatalogo) && source is ContenidoCatalogoDTO ccDto) return (TDestination)(object)ToEntity(ccDto);
        if (typeof(TDestination) == typeof(ContenidoCatalogoDTO) && source is ContenidoCatalogo cc) return (TDestination)(object)ToDTO(cc);
        if (typeof(TDestination) == typeof(IEnumerable<ContenidoCatalogoDTO>) && source is IEnumerable<ContenidoCatalogo> ccList) return (TDestination)(object)ToDTOList(ccList);
        if (typeof(TDestination) == typeof(List<ContenidoCatalogoDTO>) && source is IEnumerable<ContenidoCatalogo> ccList2) return (TDestination)(object)ToDTOList(ccList2);

        // FormField
        if (typeof(TDestination) == typeof(FormField) && source is FormFieldDTO ffDto) return (TDestination)(object)ToEntity(ffDto);
        if (typeof(TDestination) == typeof(FormFieldDTO) && source is FormField ff) return (TDestination)(object)ToDTO(ff);
        if (typeof(TDestination) == typeof(IEnumerable<FormFieldDTO>) && source is IEnumerable<FormField> ffList) return (TDestination)(object)ToDTOList(ffList);
        if (typeof(TDestination) == typeof(List<FormFieldDTO>) && source is IEnumerable<FormField> ffList2) return (TDestination)(object)ToDTOList(ffList2);

        // Quiniela
        if (typeof(TDestination) == typeof(QuinielaResponseDto) && source is Quiniela q) return (TDestination)(object)ToResponseDTO(q);
        if (typeof(TDestination) == typeof(IEnumerable<QuinielaResponseDto>) && source is IEnumerable<Quiniela> qList) return (TDestination)(object)ToResponseDTOList(qList);
        if (typeof(TDestination) == typeof(List<QuinielaResponseDto>) && source is IEnumerable<Quiniela> qList2) return (TDestination)(object)ToResponseDTOList(qList2);
        if (typeof(TDestination) == typeof(QuinielaDetailDto) && source is Quiniela qd) return (TDestination)(object)ToDetailDTO(qd);

        // QuinielaMember
        if (typeof(TDestination) == typeof(QuinielaMemberDto) && source is QuinielaMember qm) return (TDestination)(object)ToMemberDTO(qm);
        if (typeof(TDestination) == typeof(IEnumerable<QuinielaMemberDto>) && source is IEnumerable<QuinielaMember> qmList) return (TDestination)(object)ToMemberDTOList(qmList);
        if (typeof(TDestination) == typeof(List<QuinielaMemberDto>) && source is IEnumerable<QuinielaMember> qmList2) return (TDestination)(object)ToMemberDTOList(qmList2);

        // League
        if (typeof(TDestination) == typeof(LeagueDto) && source is League lg) return (TDestination)(object)ToLeagueDTO(lg);
        if (typeof(TDestination) == typeof(IEnumerable<LeagueDto>) && source is IEnumerable<League> lgList) return (TDestination)(object)ToLeagueDTOList(lgList);
        if (typeof(TDestination) == typeof(List<LeagueDto>) && source is IEnumerable<League> lgList2) return (TDestination)(object)ToLeagueDTOList(lgList2);

        throw new NotSupportedException($"No mapping defined between {source.GetType().Name} and {typeof(TDestination).Name}");
    }
}
