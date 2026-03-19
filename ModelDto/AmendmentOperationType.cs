namespace ModelDto
{
    /// <summary>
    /// Rodzaj operacji nowelizacyjnej.
    /// </summary>
    public enum AmendmentOperationType
    {
        /// <summary>Uchylenie jednostki redakcyjnej</summary>
        Repeal,
        /// <summary>Dodanie nowej jednostki redakcyjnej</summary>
        Insertion,
        /// <summary>Zmiana brzmienia jednostki redakcyjnej</summary>
        Modification,
        /// <summary>Błąd podczas przetwarzania</summary>
        Error
    }
}