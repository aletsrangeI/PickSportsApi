namespace DTO.Quiniela;

public class QuinielaDetailDto : QuinielaResponseDto
{
    public int? CurrentUserMemberId { get; set; }
    public List<QuinielaMemberDto> Members { get; set; } = new();
}
