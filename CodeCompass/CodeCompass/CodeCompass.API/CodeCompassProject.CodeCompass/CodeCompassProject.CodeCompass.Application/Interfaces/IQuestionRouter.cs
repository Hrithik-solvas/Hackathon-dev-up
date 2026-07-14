namespace CodeCompassProject.CodeCompass.Application.Interfaces;

public interface IQuestionRouter
{
    QuestionClassification Classify(string question);
}

public enum QuestionClassification
{
    Product,
    TechStack,
    Both
}
