namespace ConstantLearning.Enums;

public enum BotMessageKey
{
    // Registration
    ChatAlreadyRegistered,
    ChatRegisteredSuccess,
    RegistrationError,
    
    // Stop Learning
    ChatNotRegistered,
    LearningStopped,
    StopLearningError,
    
    // Progress
    ProgressRestarted,
    RestartProgressError,
    
    // Time Configuration
    InvalidTimeCommandFormat,
    InvalidTimeFormat,
    RepetitionTimeSet,
    NewWordsTimeSet,
    UpdateTimeError,
    
    // Words Count Configuration
    InvalidWordsCountFormat,
    WordsCountSet,
    UpdateWordsCountError,
    
    // Help
    Help,
    
    // Get All Learned
    NoLearnedWords,
    LearnedWordsHeader,
    GetAllLearnedError,
    
    // Message Formatter
    NoRepetitionWords,
    RepetitionHeader,
    AnswersHeader,
    NoNewWords,
    NewWordsHeader,
    AllWordsLearned
}
