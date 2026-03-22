using System.Collections.Generic;

namespace Denkiishi_v2.Models
{
    public class QuizSessionViewModel
    {
        public int SessionId { get; set; }

        // Futuro: poderá indicar se é lição ou revisão SRS
        public string Mode { get; set; } = "lesson"; // "lesson" | "review"

        public List<QuizQuestionViewModel> Questions { get; set; } = new();
    }

    public class QuizQuestionViewModel
    {
        // "radical" | "kanji" | "vocab"
        public string ItemType { get; set; } = default!;

        public int ItemId { get; set; }

        // O caractere principal (radical, kanji ou vocab)
        public string Character { get; set; } = default!;

        // "meaning" ou "reading"
        public string PromptType { get; set; } = default!;

        // Texto da pergunta em PT-BR (ex: "Qual é o significado deste kanji?")
        public string PromptText { get; set; } = default!;

        // Texto auxiliar (ex: "Responda em Português" / "Digite a leitura em hiragana")
        public string? HelperText { get; set; }

        /// <summary>Resposta considerada correta (meaning ou reading principal). Usado em "Mostrar resposta" e validação.</summary>
        public string? CorrectAnswer { get; set; }

        /// <summary>Mensagem quando o usuário acerta um alternativo (ex: "Estamos considerando o significado principal.").</summary>
        public string? PrincipalHint { get; set; }
    }
}

