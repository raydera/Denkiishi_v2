namespace Denkiishi_v2.Models;

public class QuizItemState
{
    public bool MeaningCorrect { get; set; }
    public bool ReadingCorrect { get; set; }
    public int MeaningErrors { get; set; }
    public int ReadingErrors { get; set; }
}