using System.Xml.Linq;
using ModelDto;
using Serilog;

namespace WordParserCore.Services.Converters
{
    internal static class SentenceProcessor
    {
        internal static SentenceContext? Process(List<TextSegment> textSegments, SentenceContext? previousSentenceContext, XElement textSectionElement)
        {
            SentenceContext? contextToPass = null;

            for (int i = 0; i < textSegments.Count; i++)
            {
                /// <summary>
                /// Processes text segments using boolean logic.
                /// Inputs:
                ///     A if there is a context from previous sentence (if we are inside an enumeration),
                ///     B if the sentence is first on it's level,
                ///     C if the sentence is last on it's level,
                ///     D if the sentence is opening an enumeration.
                /// Outputs:
                ///     E if we continue an enumeration,
                ///     F if we start a new enumeration (create a new context),
                ///     G if we pass previous context further.
                ///</summary>
                var textSegment = textSegments[i];
                var isContextExist = previousSentenceContext != null; // A
                var isFirst = i == 0; // B
                var isLast = i == textSegments.Count - 1; // C
                var isIntro = textSegment.Role == "ListIntro"; // D

                if (isContextExist && isFirst && isLast && isIntro) // E!FG
                {
                    previousSentenceContext!.CurrentFragment += 1;
                    textSectionElement.Add(SentenceContext.ToXml(context: previousSentenceContext, text: textSegment.Text));
                    contextToPass = previousSentenceContext;
                }
                else if (isContextExist && isFirst && !isIntro) // E!F!G
                {
                    previousSentenceContext!.CurrentFragment += 1;
                    textSectionElement.Add(SentenceContext.ToXml(context: previousSentenceContext, text: textSegment.Text));
                    contextToPass = null;
                }
                else if (!isIntro) // !E!F!G
                {
                    textSectionElement.Add(SentenceContext.ToXml(number: textSegment.Order, guid: Guid.NewGuid().ToString(), fragment: 1, text: textSegment.Text));
                    contextToPass = null;
                }
                else if (isLast) // !EF!G
                {
                    var nextSentenceContext = new SentenceContext(
                        currentFragment: 1,
                        referenceSentenceNumber: textSegment.Order,
                        referenceSentenceGUID: Guid.NewGuid().ToString());
                    textSectionElement.Add(SentenceContext.ToXml(context: nextSentenceContext, text: textSegment.Text));
                    contextToPass = nextSentenceContext;
                }
                else // a sentence that isn't last but is intro
                {
                    Log.Warning("Podczas przetwarzania fragmentów zdań wykryto fragment, który nie jest ostatni na danym poziomie, ale otwiera nowe wyliczenie.\nTextSegment(Type={type}, Text={text}, Order={order}, Role={role})", textSegment.Type.ToString(), textSegment.Text, textSegment.Order.ToString(), textSegment.Role);
                }
            }
            return contextToPass;
        }
    }
}
