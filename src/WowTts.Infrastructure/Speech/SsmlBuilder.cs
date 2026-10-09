using System.Globalization;
using System.Security;
using System.Text;
using WowTts.Domain.Speech;

namespace WowTts.Infrastructure.Speech;

// Builds the SSML that tells Azure Speech how to speak a line: which voice, which style (mstts:express-as)
// and how strongly, and speed and pitch (prosody). Paragraph breaks in quest text become short pauses.
public static class SsmlBuilder
{
    public static string Build(string text, VoicePerformance performance)
    {
        var body = new StringBuilder();
        var paragraphs = text.Split("\n\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        for (int i = 0; i < paragraphs.Length; i++)
        {
            if (i > 0)
                body.Append("<break time=\"600ms\"/>");
            // Quest text can contain & or <, which would break the XML.
            body.Append(SecurityElement.Escape(paragraphs[i].Replace('\n', ' ')));
        }

        string spoken = body.ToString();

        if (performance.RatePercent != 0 || performance.PitchPercent != 0)
        {
            spoken = $"<prosody rate=\"{Signed(performance.RatePercent)}%\" pitch=\"{Signed(performance.PitchPercent)}%\">{spoken}</prosody>";
        }

        if (performance.Style is not null)
        {
            string degree = performance.StyleDegree.ToString("0.##", CultureInfo.InvariantCulture);
            spoken = $"<mstts:express-as style=\"{SecurityElement.Escape(performance.Style)}\" styledegree=\"{degree}\">{spoken}</mstts:express-as>";
        }

        string locale = string.Join('-', performance.Voice.Split('-').Take(2));

        return $"<speak version=\"1.0\" xmlns=\"http://www.w3.org/2001/10/synthesis\" " +
               $"xmlns:mstts=\"https://www.w3.org/2001/mstts\" xml:lang=\"{locale}\">" +
               $"<voice name=\"{SecurityElement.Escape(performance.Voice)}\">{spoken}</voice></speak>";
    }

    private static string Signed(int value) => value >= 0 ? $"+{value}" : value.ToString(CultureInfo.InvariantCulture);
}
