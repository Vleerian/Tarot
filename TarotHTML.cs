using System.Collections.ObjectModel;

using NSDotnet;
using NSDotnet.Models;

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

    public static string MakeURI(string puppet, string target, bool AutoCloseNoTemplate = true) =>
        $"https://www.nationstates.net/container={puppet}/nation={puppet}/" + target + $"/User_Agent={NSAPI.Instance.UserAgent}/Script=Tarot/Author_Email=vleerian@hotmail.com/Author_Discord=Vleerian/Author_Main_Nation=Vleerian" + (AutoCloseNoTemplate ? "/template-overall=none/autoclose=1" : "");

    public static string MakeURI(string puppet, bool AutoCloseNoTemplate = true) =>
        $"https://www.nationstates.net/container={puppet}/nation={puppet}/User_Agent={NSAPI.Instance.UserAgent}/Script=Tarot/Author_Email=vleerian@hotmail.com/Author_Discord=Vleerian/Author_Main_Nation=Vleerian" + (AutoCloseNoTemplate ? "/template-overall=none/autoclose=1" : "");

    public static string JunkLink(string puppet, DeckViewEntry card, bool AutoCloseNoTemplate = true) =>
        MakeURI(puppet, $"page=ajax3/a=junkcard/card={card.ID}/season={card.Season}", AutoCloseNoTemplate);

    public static async Task Generate_Issue_Links(NationAPI[] Issues)
    {
        string output = template_start;
        int Number = 1;
        int Count = Issues.Sum(I => I.Issues.Length);
        foreach(var Puppet in Issues)
        {
            string canon = NSDotnet.Helpers.SanitizeName(Puppet.name);
            foreach(var Issue in Puppet.Issues)
            {
                output += $"<tr>\n\t<td><p>{Number++}/{Count}</p></td>\n";
                if(Issue.IssueID == 407)
                    output += $"<td><p><a target=\"_blank\" href=\"{MakeURI(canon, "page=show_dilemma/dilemma=407/template-overall=none", false)}\">{canon} Issue {Issue.IssueID}</a></p></td>\n";
                else
                {
                    output += $"<td><p>{Puppet.name} Issue {Issue.IssueID}</p>";
                    foreach(var opt in Issue.Options.OrderBy(I=>I.OptionID))
                        output += $"<td><p><a target=\"_blank\" href=\"{MakeURI(canon, $"page=enact_dilemma/choice-{opt.OptionID}=1/dilemma={Issue.IssueID}")}\">Option {opt.OptionID}</a></p></td>";
                    output += "</td>\n";
                    int option = Issue.Options.MinBy(I=>I.OptionID).OptionID;
                    
                }
                output += $"</tr>\n";
            }
        }
        output += template_end;
        await File.WriteAllTextAsync("issue_links.html", output);
    }

    public static async Task Generate_Pack_Links(PuppetData[] Puppets, bool AutoClose)
    {
        string output = template_start;
        int Number = 1;
        int Count = Puppets.Sum(P=>P.Num_Packs);
        foreach(var Puppet in Puppets)
        {
            string canon = NSDotnet.Helpers.SanitizeName(Puppet.Name);
            for(int i = 0; i < Puppet.Num_Packs; i++)
            {
                output += $"<tr>\n\t<td><p>{Number++}/{Count}</p></td>\n";
                output += $"<td><p><a target=\"_blank\" href=\"{MakeURI(canon, "/page=deck?open_loot_box=1", AutoClose)}\">{canon} Pack {i}</a></p></td>\n";
                output += $"</tr>\n";
            }
        }
        output += template_end;
        await File.WriteAllTextAsync("pack_links.html", output);
    }

    public static async Task Generate_Junk_Links((string Puppet, DeckViewEntry[] Cards)[] Junk)
    {
        string output = template_start;
        int Number = 1;
        int Count = Junk.Select(J=>J.Cards.Count()).Sum();
        foreach(var Puppet in Junk)
        {
            foreach(var card in Puppet.Cards)
            {
                output += $"<tr>\n\t<td><p>{Number++}/{Count}</p></td>\n";
                output += $"<td><p><a target=\"_blank\" href=\"{JunkLink(Puppet.Puppet, card)}\">({Puppet.Puppet}) Season {card.Season} {card.Name}</a></p></td>\n";
                output += $"<td><p><a target=\"_blank\" href=\"{MakeURI(Puppet.Puppet, $"page=deck/card={card.ID}/season={card.Season}/gift=1", false)}\">Gift</a></p></td></tr>\n";
            }
        }
        output += template_end;
        await File.WriteAllTextAsync("junk_links.html", output);
    }

    public static async Task Generate_Puppet_Links(string[] puppets)
    {
        string output = template_start;
        string Containerize_Container = "";
        string Containerize_Nation = "";
        int Number = 1;
        int Count = puppets.Count();
        foreach(var puppet in puppets)
        {
            string canon = NSDotnet.Helpers.SanitizeName(puppet);
            Containerize_Container += $"@^.*\\.nationstates\\.net/(.*/)?container={canon}(/.*)?$ , {canon}\n";
            Containerize_Nation += $"@^.*\\.nationstates\\.net/(.*/)?nation={canon}(/.*)?$ , {canon}\n";
            output += $"<tr>\n\t<td><p>{Number++}/{Count}</p></td>\n";
            output += $"<td><p><a target=\"_blank\" href=\"{MakeURI(canon, false)}\">{canon}</a></p></td>\n";
            foreach(KeyValuePair<string, string> lnk in Links)
                output += $"<td><p><a target=\"_blank\" href=\"{MakeURI(canon, lnk.Value)}\">{lnk.Key}</a></p></td>\n";
            output += $"\t<td class=\"createcol\"><p><a target=\"_blank\" href=\"{MakeURI(canon, $"page=blank/template-overall=none/x-rces-cp?x-rces-cp-nation={canon}", false)}\">Create {canon}</a></p></td>\n";
            output += "</tr>\n";
        }
        output += template_end;
        await File.WriteAllTextAsync("puppet_links.html", output);
        await File.WriteAllTextAsync("containerise (container).txt", Containerize_Container);
        await File.WriteAllTextAsync("containerise (nation).txt", Containerize_Nation);
    }
}