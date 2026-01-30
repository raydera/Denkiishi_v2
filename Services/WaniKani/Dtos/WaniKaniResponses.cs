using System.Text.Json.Serialization;

namespace Denkiishi_v2.Services.WaniKani.Dtos
{
    // ... (WaniKaniCollectionResponse e PagesInfo permanecem iguais)
    public class WaniKaniCollectionResponse
    {
        [JsonPropertyName("data")] public List<WaniKaniResource> Data { get; set; } = new();
        [JsonPropertyName("pages")] public PagesInfo? Pages { get; set; }
    }
    public class PagesInfo { [JsonPropertyName("next_url")] public string? NextUrl { get; set; } }

    public class WaniKaniResource
    {
        [JsonPropertyName("id")] public int Id { get; set; }
        [JsonPropertyName("object")] public string? ObjectType { get; set; }
        [JsonPropertyName("data")] public WaniKaniSubjectData? Data { get; set; }
    }

    public class WaniKaniSubjectData
    {
        [JsonPropertyName("characters")] public string? Characters { get; set; }
        [JsonPropertyName("level")] public int Level { get; set; }
        [JsonPropertyName("slug")] public string? Slug { get; set; } // Usado para mnemonics se quiser

        // IDs dos componentes (Serve para Kanji->Radical E Vocab->Kanji)
        [JsonPropertyName("component_subject_ids")]
        public List<int> ComponentSubjectIds { get; set; } = new();

        [JsonPropertyName("meanings")] public List<WaniKaniMeaning> Meanings { get; set; } = new();
        [JsonPropertyName("readings")] public List<WaniKaniReading> Readings { get; set; } = new();

        // NOVO: Frases de Exemplo
        [JsonPropertyName("context_sentences")]
        public List<WaniKaniContextSentence> ContextSentences { get; set; } = new();
    }

    public class WaniKaniContextSentence
    {
        [JsonPropertyName("en")] public string En { get; set; } = string.Empty;
        [JsonPropertyName("ja")] public string Ja { get; set; } = string.Empty;
    }

    // ... (Classes Meaning e Reading iguais ao anterior)
    public class WaniKaniMeaning { [JsonPropertyName("meaning")] public string Meaning { get; set; } = string.Empty; [JsonPropertyName("primary")] public bool Primary { get; set; } }
    public class WaniKaniReading { [JsonPropertyName("type")] public string Type { get; set; } = string.Empty; [JsonPropertyName("reading")] public string Reading { get; set; } = string.Empty; }
}