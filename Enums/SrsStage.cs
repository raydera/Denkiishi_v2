namespace Denkiishi_v2.Enums
{
    public enum SrsStage
    {
        Initiate = 0,     // Trancado / Não aprendido
        Apprentice1 = 1,  // Aprendiz 1 (Próxima revisão em 4 horas)
        Apprentice2 = 2,  // Aprendiz 2 (Próxima revisão em 8 horas)
        Apprentice3 = 3,  // Aprendiz 3 (Próxima revisão em 23 horas)
        Apprentice4 = 4,  // Aprendiz 4 (Próxima revisão em 47 horas)
        Guru1 = 5,        // Guru 1 (Próxima revisão em 1 semana)
        Guru2 = 6,        // Guru 2 (Próxima revisão em 2 semanas)
        Master = 7,       // Mestre (Próxima revisão em 1 mês)
        Enlightened = 8,  // Iluminado (Próxima revisão em 4 meses)
        Burned = 9        // Queimado / Finalizado (Nunca mais revisa)
    }
}