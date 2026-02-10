namespace ConstantLearning.Configuration;

public class LearningOptions
{
    public const string SectionName = "Learning";
    
    public int RepetitionWordsCount { get; set; }
    public int NewWordsCount { get; set; }
    public int AnswerDelaySeconds { get; set; }
    public int LearnedWordsChunkSize { get; set; }
}
