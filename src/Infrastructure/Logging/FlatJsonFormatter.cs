using Serilog.Events;
using Serilog.Formatting;
using Serilog.Formatting.Json;

namespace Infrastructure.Logging;

public sealed class FlatJsonFormatter : ITextFormatter
{
    private static readonly JsonValueFormatter ValueFormatter = new();

    public void Format(LogEvent logEvent, TextWriter output)
    {
        ArgumentNullException.ThrowIfNull(logEvent);
        ArgumentNullException.ThrowIfNull(output);

        output.Write('{');

        WriteString(output, "message", logEvent.RenderMessage(), first: true);
        WriteString(output, "timestamp", logEvent.Timestamp.UtcDateTime.ToString("O"));
        WriteString(output, "level", logEvent.Level.ToString().ToLowerInvariant());

        if (logEvent.Exception is not null)
            WriteString(output, "exception", logEvent.Exception.ToString());

        foreach (var property in logEvent.Properties)
        {
            output.Write(',');
            JsonValueFormatter.WriteQuotedJsonString(ToCamelCase(property.Key), output);
            output.Write(':');
            WriteValue(property.Value, output);
        }

        output.WriteLine('}');
    }

    private static void WriteValue(LogEventPropertyValue value, TextWriter output)
    {
        if (value is StructureValue structure)
            WriteStructure(structure, output);
        else if (value is SequenceValue sequence)
            WriteSequence(sequence, output);
        else
            ValueFormatter.Format(value, output);
    }

    private static void WriteStructure(StructureValue structure, TextWriter output)
    {
        output.Write('{');
        var first = true;
        foreach (var prop in structure.Properties)
        {
            if (!first)
                output.Write(',');
            first = false;
            JsonValueFormatter.WriteQuotedJsonString(ToCamelCase(prop.Name), output);
            output.Write(':');
            WriteValue(prop.Value, output);
        }
        output.Write('}');
    }

    private static void WriteSequence(SequenceValue sequence, TextWriter output)
    {
        output.Write('[');
        var first = true;
        foreach (var element in sequence.Elements)
        {
            if (!first)
                output.Write(',');
            first = false;
            WriteValue(element, output);
        }
        output.Write(']');
    }

    private static void WriteString(TextWriter output, string name, string value, bool first = false)
    {
        if (!first)
            output.Write(',');

        JsonValueFormatter.WriteQuotedJsonString(name, output);
        output.Write(':');
        JsonValueFormatter.WriteQuotedJsonString(value, output);
    }

    private static string ToCamelCase(string name)
    {
        if (string.IsNullOrEmpty(name) || char.IsLower(name[0]))
            return name;

        return string.Create(name.Length, name, static (span, s) =>
        {
            span[0] = char.ToLowerInvariant(s[0]);
            s.AsSpan(1).CopyTo(span[1..]);
        });
    }
}
