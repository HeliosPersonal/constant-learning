namespace ConstantLearning.Enums;

public enum BotMessageKey
{
    // Реєстрація
    ChatAlreadyRegistered,
    ChatRegisteredSuccess,
    RegistrationError,
    
    // Зупинка навчання
    ChatNotRegistered,
    LearningStopped,
    StopLearningError,
    
    // Прогрес
    ProgressRestarted,
    RestartProgressError,
    
    // Налаштування часу
    InvalidTimeCommandFormat,
    InvalidTimeFormat,
    RepetitionTimeSet,
    NewWordsTimeSet,
    UpdateTimeError,
    
    // Налаштування кількості слів
    InvalidWordsCountFormat,
    WordsCountSet,
    UpdateWordsCountError,
    
    // Довідка
    Help,
    
    // Отримання всіх вивчених слів
    NoLearnedWords,
    LearnedWordsHeader,
    GetAllLearnedError,
    
    // Форматування повідомлень
    NoRepetitionWords,
    RepetitionHeader,
    AnswersHeader,
    NoNewWords,
    NewWordsHeader,
    AllWordsLearned
}
