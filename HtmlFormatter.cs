using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Web;

namespace SqlParseTree
{
    public static class HtmlFormatter
    {
        public static string Format(ParseData data, StringBuilder log)
        {
            var watch = Stopwatch.StartNew();
            var count = data.Count();
            s_unique = 0;
            var script = new StringBuilder();
            var body = new StringBuilder();

            body.AppendLine("<ol>");
            AddData(data, script, body);
            body.AppendLine("</ol>");

            log.AppendLine($"Render HTML: {watch.Elapsed}");
            return $@"
<!DOCTYPE html>
<html>
<HEAD>
    <TITLE>SQL</TITLE>
    <style>
        *.detailText
        {{
            width: calc(100% - 50px);
        }}
        *.hide
        {{
            display: none;
        }}
        *.highlight
        {{
            background: yellow;
        }}
        *.canHide 
        {{
        }}
    </style>
    <script>
    var types = Array({count});
    var texts = Array({count});

    function toggleChildren(unique) {{
        document.getElementById('chd_' + unique).classList.toggle('hide');
    }};
    function toggleDetail(unique) {{
        document.getElementById('dtl_' + unique).classList.toggle('hide');
    }};
    function collapseEpandAll(forCollapse) {{
        var elements = document.getElementsByClassName('canHide');
        for (var i = 0; i < elements.length; i++) {{
            if (forCollapse) {{
                elements[i].classList.add('hide');
            }} else {{
                elements[i].classList.remove('hide');
            }}
        }}
    }};
    function hightlight(prefix, unique) {{
        var id = prefix + '_' + unique;
        var element = document.getElementById(id);
        element.classList.add('highlight');

        while (element !== null) {{
            if (element.classList.contains('canHide')) {{
                element.classList.remove('hide');
            }}
            element = element.parentElement;
        }}
    }};
    function doSearch() {{
        var elements = document.getElementsByClassName('highlight');
        for (var i = 0; i < elements.length; i++) {{
            elements[i].classList.remove('highlight');
        }}
        var text = document.getElementById('txt_search').value;
        if (text) {{
            text = text.toLowerCase();
            for (var i = 0; i < {count}; i++) {{
                if (types[i].startsWith(text)) {{
                    hightlight('type', i);
                }}
                if (texts[i].toLowerCase().startsWith(text)) {{
                    hightlight('text', i);
                }}
            }}
        }}
    }};
{script}
    </script>
</HEAD>
<BODY>
<input type='button' value='Collapse All' onClick='collapseEpandAll(true);' />
<input type='button' value='Expand All' onClick='collapseEpandAll(false);' />
<br />
<input type='type' id='txt_search' onKeyUp='doSearch();' />
{body}
</BODY>
</html>";
        }

        private static void AddData(ParseData data, StringBuilder script, StringBuilder body)
        {
            var unique = s_unique;
            s_unique++;
            script.AppendLine($"texts[{unique}]={HttpUtility.JavaScriptStringEncode(data.Text, addDoubleQuotes: true)};");
            script.AppendLine($"types[{unique}]='{data.TypeName.ToLower()}';");

            body.AppendLine("<li>");

            if (data.Children != null)
            {
                body.AppendLine($"<span onClick = 'toggleChildren({unique});'> +/- </span>");
            }
            body.AppendLine($"<b id='type_{unique}'>{data.TypeName}</b>:");

            var lines = data.Text.Split(Environment.NewLine);
            var firstLine = lines.Where(x => !string.IsNullOrEmpty(x)).FirstOrDefault();
            body.AppendLine($"<span id='text_{unique}'>{HttpUtility.HtmlEncode(firstLine)}</span>");
            if ((firstLine?.Length ?? 0) < data.Text.Length)
            {
                body.AppendLine($" <span onClick = 'toggleDetail({unique});'>...</span>");
                body.AppendLine($"<div id='dtl_{unique}' class='detailText hide canHide'>");
                body.AppendLine($"<textarea class='detailText' id='txt_{unique}' rows='{Math.Min(lines.Length, 5)}' readonly></textarea>");
                body.AppendLine("<script type='text/javascript'>");
                body.AppendLine($"document.getElementById('txt_{unique}').value=texts[{unique}];");
                body.AppendLine("</script>");
                body.AppendLine("</div>");
            }

            AddProperties(data.Properties, body);

            if (data.Children != default)
            {
                body.AppendLine($"<ol id='chd_{unique}' class='hide canHide'>");
                foreach (var child in data.Children)
                {
                    AddData(child, script, body);
                }
                body.AppendLine("</ol>");
            }

            body.AppendLine("</li>");
        }

        private static void AddProperties(List<Property>? properties, StringBuilder body)
        {
            if (properties == null)
            {
                return;
            }

            body.AppendLine($"<ul>");
            foreach (var property in properties)
            {
                body.AppendLine("<li>");
                body.AppendLine($"{property.Name}:");
                if (property.Value != null)
                {
                    switch (property.Value)
                    {
                        case string s:
                            body.AppendLine(s);
                            break;
                        case List<Property> p:
                            AddProperties(p, body);
                            break;
                        default:
                            body.AppendLine(property.Value.GetType().Name);
                            break;
                    }
                }
                body.AppendLine("</li>");
            }
            body.AppendLine("</ul>");
        }

        private static int s_unique = 0;
    }
}