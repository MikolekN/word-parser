using System;

namespace ModelDto
{
    /// <summary>
    /// Poziom ważności wiadomości walidacji/diagnostyki (błędy, ostrzeżenia, informacje).
    /// Umożliwia kategoryzowanie dokumentów pod względem "jakości" parsowania.
    /// </summary>
    public enum ValidationLevel
    {
        /// <summary>
        /// Informacja diagnostyczna (bez wpływu na poprawność struktury).
        /// </summary>
        Info,

        /// <summary>
        /// Ostrzeżenie (potencjalny problem, ale struktura jest zrozumiała).
        /// </summary>
        Warning,

        /// <summary>
        /// Błąd (problem z parsowaniem lub strukturą, ale jednostka może być częściowo użyteczna).
        /// </summary>
        Error,

        /// <summary>
        /// Błąd krytyczny (struktura całkowicie zniekształcona, jednostka nie powinna być używana).
        /// </summary>
        Critical
    }

    /// <summary>
    /// Wiadomość diagnostyczna opisująca problem lub informację w trakcie parsowania dokumentu.
    /// </summary>
    public class ValidationMessage
    {
        /// <summary>
        /// Poziom ważności wiadomości.
        /// </summary>
        public ValidationLevel Level { get; set; }

        /// <summary>
        /// Tekst wiadomości opisujący problem lub informację.
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Opcjonalny timestamp wiadomości (dla śledzenia chronologii błędów).
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ValidationMessage() { }

        public ValidationMessage(ValidationLevel level, string message)
        {
            Level = level;
            Message = message;
        }

        public override string ToString() => $"[{Level}] {Message}";
    }
}
