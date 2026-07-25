#nullable enable

using System.Collections.Generic;
using ModelDto.EditorialUnits;

namespace ModelDto
{
    /// <summary>
    /// Kontrakt dla jednostek, które mogą posiadać części wspólne (intro/wrapUp).
    /// </summary>
    public interface IHasCommonParts
    {
        /// <summary>
        /// Części wspólne występujące na tym samym poziomie co elementy wyliczeniowe.
        /// </summary>
        List<CommonPart> CommonParts { get; set; }
    }
}
