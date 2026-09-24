namespace DTO.Quiniela;

public class QuinielaDetailDto : QuinielaResponseDto
{
    public List<QuinielaMemberDto> Members { get; set; } = new();
}
