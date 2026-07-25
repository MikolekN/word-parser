using ModelDto;
using WordParserCore.Services.Parsing;
using Xunit;

namespace WordParserCore.Tests
{
    /// <summary>
    /// Testy zaawansowanego podziału tekstu na zdania
    /// z obsługą wyjątków ("Dz. U.", "RRRR r.").
    /// </summary>
    public class SentenceSplittingTests
    {
        [Fact]
        public void SplitIntoSentences_TwoNormalSentences_SplitsCorrectly()
        {
            var text = "To jest pierwsze zdanie. To jest drugie zdanie.";
            
            var segments = ParsingFactories.SplitIntoSentences(text);
            
            Assert.Equal(2, segments.Count);
            Assert.Equal("To jest pierwsze zdanie.", segments[0].Text);
            Assert.Equal("To jest drugie zdanie.", segments[1].Text);
        }

        [Fact]
        public void SplitIntoSentences_DzUException_DoesNotSplit()
        {
            // "Dz. U." nie powinno być punktem podziału, nawet jeśli następuje wielka litera
            var text = "W 2024 r. przychód mogą stanowić: (Dz. U. z 2023 r. poz. 227), mogą także stanowić następujące rzeczy.";
            
            var segments = ParsingFactories.SplitIntoSentences(text);
            
            // Powinno być jedno zdanie, które zawiera "Dz. U."
            Assert.Single(segments);
            Assert.Contains("Dz. U. z 2023 r.", segments[0].Text);
        }

        [Fact]
        public void SplitIntoSentences_YearException_DoesNotSplit()
        {
            // "RRRR r." nie powinno być punktem podziału
            var text = "W 2024 r. Bank Gospodarstwa Krajowego dokumentuje zasoby.";
            
            var segments = ParsingFactories.SplitIntoSentences(text);
            
            // Powinno być jedno zdanie, które zawiera rok
            Assert.Single(segments);
            Assert.Contains("2024 r.", segments[0].Text);
        }

        [Fact]
        public void SplitIntoSentences_MultipleYears_DoesNotSplitAfterEachYear()
        {
            var text = "W 2020 r. i 2021 r. Bank Gospodarstwa Krajowego działał. Działalność była intensywna.";
            
            var segments = ParsingFactories.SplitIntoSentences(text);
            
            // Pierwsze zdanie zawiera dwa roki, drugie to osobne zdanie
            Assert.Equal(2, segments.Count);
            Assert.Contains("2020 r. i 2021 r.", segments[0].Text);
        }

        [Fact]
        public void SplitIntoSentences_DzUAndYearException_HandlesMultipleExceptions()
        {
            var text = "Przychód mogą stanowić: (Dz. U. z 2023 r. poz. 227), mogą także stanowić. W 2024 r. Fundusz prowadzi działalność.";
            
            var segments = ParsingFactories.SplitIntoSentences(text);
            
            // Powinny być dokładnie 2 zdania
            Assert.Equal(2, segments.Count);
            Assert.Contains("Dz. U.", segments[0].Text);
            Assert.Contains("2024 r.", segments[1].Text);
        }

        [Fact]
        public void SplitIntoSentences_EmptyString_ReturnsEmpty()
        {
            var segments = ParsingFactories.SplitIntoSentences(string.Empty);
            
            Assert.Empty(segments);
        }

        [Fact]
        public void SplitIntoSentences_SingleSentenceNoEnd_ReturnsSingle()
        {
            var text = "To jest tekst bez kropki na końcu";
            
            var segments = ParsingFactories.SplitIntoSentences(text);
            
            Assert.Single(segments);
            Assert.Equal(text, segments[0].Text);
        }

        [Fact]
        public void SplitIntoSentences_PolishLetters_IdentifiesCapitalPolishLetters()
        {
            var text = "Pierwsza część. Ąćęłńóśźż druga część.";
            
            var segments = ParsingFactories.SplitIntoSentences(text);
            
            // Powinny być 2 zdania, bo każde zaczyna się wielką literą (w tym polską)
            Assert.Equal(2, segments.Count);
        }

        [Fact]
        public void SplitIntoSentences_DoctorTitle_SeparatesAfterTitle()
        {
            // "Dr." po którym następuje wielka litera jest traktowany jako koniec zdania
            // Tak naprawdę jest to problem z prostą heurystyką - moglibyśmy dodać 
            // wyjątek dla tytułów, ale na razie testujemy rzeczywiste zachowanie
            var text = "Dr. Jan Kowalski jest lekarzem.";
            
            var segments = ParsingFactories.SplitIntoSentences(text);
            
            // Rzeczywistość: jest 2 segmenty (Dr. | Jan Kowalski...)
            // To jest znane ograniczenie - mogłoby być naprawione dodając listę tytułów
            Assert.Equal(2, segments.Count);
            Assert.Equal("Dr.", segments[0].Text);
            Assert.Equal("Jan Kowalski jest lekarzem.", segments[1].Text);
        }

        [Fact]
        public void SplitIntoSentences_DzUWithoutSpaceAfterDz_StillDetects()
        {
            // Przypadek "Dz.U." bez spacji między Dz. i U.
            var text = "W ustawie z dnia (Dz.U. z 2023 r. poz. 227) wprowadza się zmiany.";
            
            var segments = ParsingFactories.SplitIntoSentences(text);
            
            // Powinno być jedno zdanie
            Assert.Single(segments);
            Assert.Contains("Dz.U.", segments[0].Text);
        }
    }
}
