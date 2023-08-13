using System.Collections.ObjectModel;

///<summary>
/// The purpose of this class is to provide equivalent functionality
/// to the generatehtml scripts from RCES. I do not believe in light
/// themes, so there is only a dark theme option.
///</summary>
public class TarotHTML
{
    readonly static string Style = @"<style>@media (prefers-color-scheme: dark) {
    body {
        background-color: #111;
        color: #FFF;
    }
    a {
        color: #FFF;
    }
    tr:hover {
        background-color: #444;
    }
}

td.createcol p {
    padding-left: 10em;
}

a {
    text-decoration: none;
}

a:visited {
    color: grey;
}

table {
    border-collapse: collapse;
    max-width: 100%;
    border: 1px solid #F06;
}

tr, td {
    border-bottom: 1px solid #F06;
}

td:first-child {
    border: 1px solid #F06;
}

td p, td:first-child {
    padding: 0.5em;
}</style>";

    readonly static string template_start = @$"<html>
<head>{Style}</head>
<body>
<table>";

    readonly static string template_end = @$"</table>
<script>
document.querySelectorAll(""a"").forEach(function(el) {{
		el.addEventListener(""click"", function(ev) {{
			if (!ev.repeat) {{
				let myidx = 0;
				const row = el.closest(""tr"");
				let child = el.closest(""td"");
				while((child = child.previousElementSibling) != null) {{
					myidx++;
				}}
				try {{
					row.nextElementSibling.children[myidx].querySelector(""p > a"").focus();
				}} finally {{
					row.parentNode.removeChild(row);
				}}
			}}
		}});
	}});
</script>
</body>
</html>";

    readonly static ReadOnlyDictionary<string, string> Links = new Dictionary<string, string>(){
        {"Issues", "page=dilemmas/template-overall=none"},
        {"Deck", "page=deck"},
        {"Value Deck", "page=deck/value_deck=1"},
        {"Value Deck (No CSS)", "page=deck/value_deck=1/template-overall=none"},
        {"Telegrams", "page=telegrams"},
        {"Settings", "page=settings"},
        {"TG Settings", "page=tgsettings"},
    }.AsReadOnly();

    public static async Task GenerateHTML(string[] puppets)
    {
        string output = template_start;
        string Containerize_Container = "";
        string Containerize_Nation = "";
        foreach(var puppet in puppets)
        {
            string canon = NSDotnet.Helpers.SanitizeName(puppet);
            Containerize_Container += $"@^.*\\.nationstates\\.net/(.*/)?container={canon}(/.*)?$ , {canon}\n";
            Containerize_Nation += $"@^.*\\.nationstates\\.net/(.*/)?nation={canon}(/.*)?$ , {canon}\n";
            string uribase = $"https://www.nationstates.net/container={canon}/nation={canon}";
            output += $"<tr>\n\t<td><p><a target=\"_blank\" href=\"{uribase}\">{canon}</a></p></td>\n";
            foreach(KeyValuePair<string, string> lnk in Links)
                output += $"<td><p><a target=\"_blank\" href=\"{uribase}/{lnk.Value}\">{lnk.Key}</a></p></td>\n";
            output += $"\t<td class=\"createcol\"><p><a target=\"_blank\" href=\"{uribase}/page=blank/template-overall=none/x-rces-cp?x-rces-cp-nation={canon}\">Create {canon}</a></p></td>\n";
            output += "</tr>\n";
        }
        output += template_end;
        await File.WriteAllTextAsync("puppet_links.html", output);
        await File.WriteAllTextAsync("containerise (container).txt", Containerize_Container);
        await File.WriteAllTextAsync("containerise (nation).txt", Containerize_Nation);
    }
}