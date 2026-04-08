using Denkiishi_v2.Enums;
using System;

namespace Denkiishi_v2.Services
{
    public interface ISrsService
    {
        // Atualizado para receber e devolver o Ease Factor
        (SrsStage NewStage, DateTime NextReview, decimal NewEaseFactor) CalculateNextReview(
            SrsStage currentStage,
            int incorrectMeaningCount,
            int incorrectReadingCount,
            decimal currentEaseFactor);
    }

    public class SrsService : ISrsService
    {
        // Intervalos base (WaniKani style)
        private readonly TimeSpan[] _intervals = new TimeSpan[]
        {
            TimeSpan.Zero,            // 0: Initiate
            TimeSpan.FromHours(4),    // 1: Apprentice 1
            TimeSpan.FromHours(8),    // 2: Apprentice 2
            TimeSpan.FromHours(23),   // 3: Apprentice 3
            TimeSpan.FromHours(47),   // 4: Apprentice 4
            TimeSpan.FromDays(7),     // 5: Guru 1
            TimeSpan.FromDays(14),    // 6: Guru 2
            TimeSpan.FromDays(30),    // 7: Master
            TimeSpan.FromDays(120),   // 8: Enlightened
            TimeSpan.Zero             // 9: Burned
        };

        public (SrsStage NewStage, DateTime NextReview, decimal NewEaseFactor) CalculateNextReview(
            SrsStage currentStage,
            int incorrectMeaningCount,
            int incorrectReadingCount,
            decimal currentEaseFactor)
        {
            int totalErrors = incorrectMeaningCount + incorrectReadingCount;
            int currentStageValue = (int)currentStage;
            int newStageValue;
            decimal newEaseFactor = currentEaseFactor;

            // Constantes do Algoritmo (Baseado em Anki/WaniKani)
            const decimal EASE_BONUS = 0.15m;    // Aumenta facilidade no acerto limpo
            const decimal EASE_PENALTY = 0.20m;  // Diminui facilidade por erro cometido
            const decimal MIN_EASE = 1.30m;      // Trava para o item não sumir do radar

            if (totalErrors == 0)
            {
                // ACERTO LIMPO: Sobe 1 nível e o item torna-se "mais fácil"
                newStageValue = currentStageValue + 1;
                newEaseFactor += EASE_BONUS;
            }
            else
            {
                // PENALIZAÇÃO DE ESTÁGIO (Leveled Down):
                // Cálculo: (Erros / 2) arredondado para cima
                int penalty = (int)Math.Ceiling(totalErrors / 2.0);

                // Regra de Ouro: Se já era Guru+, o tombo é em dobro (perda de consistência)
                if (currentStageValue >= (int)SrsStage.Guru1)
                {
                    penalty *= 2;
                }

                newStageValue = currentStageValue - penalty;

                // PENALIZAÇÃO DE EASE FACTOR:
                // Quanto mais erros, mais difícil o item é considerado.
                newEaseFactor -= (totalErrors * EASE_PENALTY);

                // Garantir que o item nunca volte ao estado "Não Aprendido" (0)
                if (newStageValue < 1) newStageValue = 1;
            }

            // Travas de Segurança
            if (newStageValue > 9) newStageValue = 9;
            if (newEaseFactor < MIN_EASE) newEaseFactor = MIN_EASE;

            // Cálculo da Próxima Revisão
            // Nota: Em sistemas avançados, multiplicamos o intervalo pelo EaseFactor. 
            // Para manter compatibilidade WaniKani, usamos o intervalo fixo do estágio.
            DateTime nextReview = DateTime.UtcNow.Add(_intervals[newStageValue]);

            // Item "Queimado" (Burned)
            if (newStageValue == 9)
            {
                nextReview = DateTime.UtcNow.AddYears(100);
            }

            return ((SrsStage)newStageValue, nextReview, newEaseFactor);
        }
    }
}