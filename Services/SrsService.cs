using Denkiishi_v2.Enums;
using System;

namespace Denkiishi_v2.Services
{
    public interface ISrsService
    {
        (SrsStage NewStage, DateTime NextReview) CalculateNextReview(SrsStage currentStage, int incorrectMeaningCount, int incorrectReadingCount);
    }

    public class SrsService : ISrsService
    {
        // Os intervalos exatos de tempo (Indexados diretamente pelo valor do SrsStage)
        // Nota: O índice 0 (Initiate) e o índice 9 (Burned) têm tempo Zero porque não entram na fila normal.
        private readonly TimeSpan[] _intervals = new TimeSpan[]
        {
            TimeSpan.Zero,            // 0: Initiate
            TimeSpan.FromHours(4),    // 1: Apprentice 1 -> Vai para Apprentice 2 em 4h
            TimeSpan.FromHours(8),    // 2: Apprentice 2 -> Vai para Apprentice 3 em 8h
            TimeSpan.FromHours(23),   // 3: Apprentice 3 -> Vai para Apprentice 4 em 23h (23h ajusta melhor ao sono do aluno do que 24h)
            TimeSpan.FromHours(47),   // 4: Apprentice 4 -> Vai para Guru 1 em 47h
            TimeSpan.FromDays(7),     // 5: Guru 1 -> Vai para Guru 2 em 1 semana
            TimeSpan.FromDays(14),    // 6: Guru 2 -> Vai para Master em 2 semanas
            TimeSpan.FromDays(30),    // 7: Master -> Vai para Enlightened em 1 mês (aprox)
            TimeSpan.FromDays(120),   // 8: Enlightened -> Vai para Burned em 4 meses
            TimeSpan.Zero             // 9: Burned (Finalizado)
        };

        public (SrsStage NewStage, DateTime NextReview) CalculateNextReview(SrsStage currentStage, int incorrectMeaningCount, int incorrectReadingCount)
        {
            int totalErrors = incorrectMeaningCount + incorrectReadingCount;
            int currentStageValue = (int)currentStage;
            int newStageValue;

            if (totalErrors == 0)
            {
                // Respondeu tudo certo! Sobe 1 nível.
                newStageValue = currentStageValue + 1;

                // Trava de segurança para não passar do nível máximo (Burned = 9)
                if (newStageValue > 9) newStageValue = 9;
            }
            else
            {
                // MÁGICA DA PENALIZAÇÃO:
                // Se o aluno errar, ele cai de nível. A fórmula base é (Erros / 2) arredondado para cima.
                int penalty = (int)Math.Ceiling(totalErrors / 2.0);

                // Regra Cruel, mas justa: Se estava no nível Guru (5) ou superior, 
                // o cérebro deveria ter consolidado a memória. O tombo é multiplicado por 2!
                if (currentStageValue >= (int)SrsStage.Guru1)
                {
                    penalty *= 2;
                }

                newStageValue = currentStageValue - penalty;

                // Um item que já foi aprendido nunca volta a ser "Trancado" (0). O fundo do poço é o Apprentice 1.
                if (newStageValue < 1) newStageValue = 1;
            }

            // Calcula a próxima data de revisão baseada na hora atual UTC (sempre use UTC em servidores!) + Intervalo
            DateTime nextReview = DateTime.UtcNow.Add(_intervals[newStageValue]);

            // Se o item for "Queimado" (Burned), marcamos a revisão para o ano 2100 (na prática, ele some do SRS)
            if (newStageValue == 9)
            {
                nextReview = DateTime.UtcNow.AddYears(100);
            }

            return ((SrsStage)newStageValue, nextReview);
        }
    }
}