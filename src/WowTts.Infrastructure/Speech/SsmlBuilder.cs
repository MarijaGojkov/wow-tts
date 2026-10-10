using System.Globalization;
using System.Security;
using System.Text;
using WowTts.Domain.Speech;

namespace WowTts.Infrastructure.Speech;

// Builds the SSML that tells Azure Speech how to speak a line: which voice, which style (mstts:express-as)
// and how strongly, and speed and pitch (prosody). Paragraph breaks in quest text become short pauses.
// HD voices support less SSML: no prosody, no style strength and no <break>, so they get paragraphs instead.
public static class SsmlBuilder
{
    public static string Build(string text, VoicePerformance performance)
    {
        // Quest text can contain & or <, which would break the XML.
        var paragraphs = text.Split("\n\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(paragraph => SecurityElement.Escape(paragraph.Replace('\n', ' ')))
            .ToList();

        string spoken = VoiceInfo.IsHdName(performance.Voice) ? ForHdVoice(paragraphs, performance) : ForStandardVoice(paragraphs, performance);

        string locale = string.Join('-', performance.Voice.Split('-').Take(2));

        return $"<speak version=\"1.0\" xmlns=\"http://www.w3.org/2001/10/synthesis\" " +
               $"xmlns:mstts=\"https://www.w3.org/2001/mstts\" xml:lang=\"{locale}\">" +
               $"<voice name=\"{SecurityElement.Escape(performance.Voice)}\">{spoken}</voice></speak>";
    }

    private static string ForStandardVoice(List<string> paragraphs, VoicePerformance performance)
    {
        string spoken = string.Join("<break time=\"600ms\"/>", paragraphs);

        if (performance.RatePercent != 0 || performance.PitchPercent != 0)
        {
            spoken = $"<prosody rate=\"{Signed(performance.RatePercent)}%\" pitch=\"{Signed(performance.PitchPercent)}%\">{spoken}</prosody>";
        }

        if (performance.Style is not null)
        {
            string degree = performance.StyleDegree.ToString("0.##", CultureInfo.InvariantCulture);
            spoken = $"<mstts:express-as style=\"{SecurityElement.Escape(performance.Style)}\" styledegree=\"{degree}\">{spoken}</mstts:express-as>";
        }

        return spoken;
    }

    // A paragraph ends a style in HD voices, so each paragraph gets the style again.
    private static string ForHdVoice(List<string> paragraphs, VoicePerformance performance)
    {
        if (performance.Style is null)
            return string.Concat(paragraphs.Select(paragraph => $"<p>{paragraph}</p>"));

        string style = SecurityElement.Escape(performance.Style);
        return string.Join("\n", paragraphs.Select(paragraph => $"<mstts:express-as style=\"{style}\">{paragraph}</mstts:express-as>"));
    }

    private static string Signed(int value) => value >= 0 ? $"+{value}" : value.ToString(CultureInfo.InvariantCulture);
}
