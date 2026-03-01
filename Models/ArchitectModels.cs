using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Denkiishi_v2.Models
{
    // ==========================================
    // ENTIDADES DO BANCO DE DADOS
    // ==========================================
    [Table("mandala")]
    public class Mandala
    {
        [Key][Column("id")] public int Id { get; set; }
        [Column("text")] public string Text { get; set; }
        [Column("sequential")] public int Sequential { get; set; }
    }

    [Table("circle")]
    public class Circle
    {
        [Key][Column("id")] public int Id { get; set; }
        [Column("mandala_id")] public int MandalaId { get; set; }
        [Column("text")] public string Text { get; set; }
        [Column("sequential")] public int Sequential { get; set; }
    }

    [Table("circle_ue_item")]
    public class CircleUeItem
    {
        [Key][Column("id")] public int Id { get; set; }
        [Column("circle_id")] public int? CircleId { get; set; }
        [Column("kanji_id")] public int? KanjiId { get; set; }
        [Column("radical_id")] public int? RadicalId { get; set; }
        [Column("vocabulary_id")] public int? VocabularyId { get; set; }
        [Column("sequential")] public int? Sequential { get; set; }
    }

    // ==========================================
    // DTOs (OBJETOS PARA A TELA)
    // ==========================================
    public class ArchitectViewModel
    {
        public List<MandalaDto> Mandalas { get; set; } = new List<MandalaDto>();
        public List<SelectListItem> LinguasDisponiveis { get; set; } = new List<SelectListItem>();
        public int LinguaPadraoId { get; set; }
    }

    public class MandalaDto
    {
        public int Id { get; set; }
        public string Nome { get; set; }
        public int Ordem { get; set; }
        public List<CircleDto> Circulos { get; set; } = new List<CircleDto>();
    }

    public class CircleDto
    {
        public int Id { get; set; }
        public string Nome { get; set; }
        public int Ordem { get; set; }
        public List<ItemDto> Itens { get; set; } = new List<ItemDto>();
    }

    public class ItemDto
    {
        public int IdMapping { get; set; } // ID da tabela circle_ue_item
        public string Tipo { get; set; } // "kanji", "radical", "vocabulario"
        public int OriginalId { get; set; } // ID original do item
        public string Texto { get; set; } // O caractere (ex: 水)
        public int CircleId { get; set; }
        public string TooltipExtra { get; set; }
        public Dictionary<int, string> Meanings { get; set; } = new Dictionary<int, string>();
    }
} 