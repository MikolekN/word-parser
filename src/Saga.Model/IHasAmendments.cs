#nullable enable

namespace ModelDto
{
    /// <summary>
    /// Kontrakt dla jednostek, ktore zawieraja nowelizacje.
    /// </summary>
    public interface IHasAmendments
    {
        /// <summary>
        /// Nowelizacja przypisana do jednostki (0 lub 1).
        /// Jedna jednostka redakcyjna może być właścicielem co najwyżej jednej nowelizacji.
        /// </summary>
        Amendment? Amendment { get; set; }
    }
}