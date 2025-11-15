using System;

namespace Zakupki.Fetcher.Services;

public sealed class NoticeAnalysisException : Exception
{
    public NoticeAnalysisException(string message, bool isValidation)
        : base(message)
    {
        IsValidation = isValidation;
    }

    public NoticeAnalysisException(string message, bool isValidation, Exception? innerException)
        : base(message, innerException)
    {
        IsValidation = isValidation;
    }

    public bool IsValidation { get; }
}
