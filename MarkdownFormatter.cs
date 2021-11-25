using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace SqlParseTree
{
    public static class MarkdownFormatter
    {
        public static string Format(ParseData data, StringBuilder log)
        {
            var watch = Stopwatch.StartNew();
            var body = new StringBuilder();
            AddData(data, body);
            log.AppendLine($"Render MD: {watch.Elapsed}");
            return body.ToString();
        }

        private static void AddData(ParseData data, StringBuilder body)
        {
            body.Append(' ', m_pad);
            body.Append($"1. **{data.TypeName}**: ");

            var lines = data.Text.Split(Environment.NewLine);
            var firstLine = lines.Where(x => !string.IsNullOrEmpty(x)).FirstOrDefault()?.Trim();
            if (!string.IsNullOrEmpty(firstLine))
            {
                const int trunkLength = 20;
                bool truncated = false;
                if (firstLine.Length > trunkLength)
                {
                    firstLine = firstLine[..trunkLength];
                    truncated = true;
                }
                body.Append($"`{firstLine.Trim()}`");
                if (truncated)
                {
                    body.Append("...");
                }
            }
            body.AppendLine();

            m_pad += 3;

            AddProperties(data.Properties, body);

            if (data.Children != default)
            {
                foreach (var child in data.Children)
                {
                    AddData(child, body);
                }
            }

            m_pad -= 3;
        }

        private static void AddProperties(List<Property>? properties, StringBuilder body)
        {
            if (properties == null)
            {
                return;
            }

            foreach (var property in properties)
            {
                body.Append(' ', m_pad);
                body.Append($"- {property.Name}: ");
                if (property.Value != null)
                {
                    switch (property.Value)
                    {
                        case string s:
                            body.AppendLine($"_{s}_");
                            break;
                        case List<Property> p:
                            body.AppendLine();
                            m_pad += 2;
                            AddProperties(p, body);
                            m_pad -= 2;
                            break;
                        default:
                            body.AppendLine(property.Value.GetType().Name);
                            break;
                    }
                }
            }
        }
        private static int m_pad = 0;
    }
}