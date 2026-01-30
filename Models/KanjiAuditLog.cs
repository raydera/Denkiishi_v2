using System.ComponentModel.DataAnnotations.Schema;

namespace Denkiishi_v2.Models;

[Table("kanji_audit_log")]
public class KanjiAuditLog
{
    [Column("id")]
    public int Id { get; set; }

    [Column("kanji_id")]
    public int KanjiId { get; set; }

    [Column("action_type")]
    public string ActionType { get; set; } = null!; // INSERT, UPDATE

    [Column("source")]
    public string Source { get; set; } = null!; // "Tanos Import"

    // Mapeando JSONB como string (o Npgsql lida bem com isso)
    [Column("changed_fields", TypeName = "jsonb")]
    public string? ChangedFields { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey("KanjiId")]
    public virtual Kanji Kanji { get; set; } = null!;
}