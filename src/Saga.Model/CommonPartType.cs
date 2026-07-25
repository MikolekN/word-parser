namespace ModelDto
{
    /// <summary>
    /// Typ części wspólnej (wirtualnego bytu reprezentującego intro lub wrapUp jednostki wyliczeniowej).
    /// 
    /// CommonPart Intro: tekst przed wyliczeniem (np. "Następujące przepisy uchyla się:" przed listą punktów)
    /// CommonPart WrapUp: tekst po wyliczeniu (np. tekst wspólny dla wszystkich wariantów po liście tiretów)
    /// 
    /// Służy do adresowania fragmentów tekstu przed i po liście w modelu redakcyjnym.
    /// Może się pojawić tylko w jednostkach wyliczeniowych (Point, Letter).
    /// </summary>
    public enum CommonPartType
    {
        /// <summary>
        /// Wprowadzenie do wyliczenia - tekst przed listą (litery, tirety, itp.)
        /// </summary>
        Intro,

        /// <summary>
        /// Część wspólna po wyliczeniu - tekst wspólny dla wszystkich wariantów
        /// </summary>
        WrapUp
    }

    public static class CommonPartTypeExtensions
    {
        public static string ToFriendlyString(this CommonPartType type)
        {
            return type switch
            {
                CommonPartType.Intro => "intro",
                CommonPartType.WrapUp => "wrapUp",
                _ => "unknown"
            };
        }

        public static string ToEIdSegment(this CommonPartType type)
        {
            return type switch
            {
                CommonPartType.Intro => "intro",
                CommonPartType.WrapUp => "wrapUp",
                _ => ""
            };
        }
    }
}
