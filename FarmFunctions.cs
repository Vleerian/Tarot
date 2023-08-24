using System.Text.RegularExpressions;

using NSDotnet;
using NSDotnet.Models;

using Spectre.Console;

public partial class Tarot
{
    async Task<NationAPI[]> Fetch_Puppet_Data()
    {
        var Puppets = await Database.Table<DBPuppet>().ToArrayAsync();
        List<NationAPI> Data = new();
        await AnsiConsole.Progress()
            .StartAsync(async ctx => {
                var FetchTask = ctx.AddTask("Fetching data...", maxValue:Puppets.Length);
                foreach(var puppet in Puppets)
                {
                    FetchTask.Description = $"Fetching data for [yellow]{puppet.Puppet}[/]";
                    await Task.Delay(600);
                    NSAPI.Instance.Auth = new NSAuth(NSDotnet.Enums.AuthType.Autologin, puppet.Password);
                    var Request = await NSAPI.Instance.GetAPI<NationAPI>($"https://www.nationstates.net/cgi-bin/api.cgi?nation={puppet.Puppet}&q=name+packs+issues");
                    if(Request.Response.StatusCode != System.Net.HttpStatusCode.OK)
                    {
                        AnsiConsole.MarkupLine($"[red]Failed[/]");
                        continue;
                    }
                    Data.Add(Request.Data);
                    await Database.ExecuteAsync("UPDATE PuppetData SET Num_Packs = ? WHERE Name LIKE ?", Request.Data.Packs, puppet.Puppet);
                    FetchTask.Increment(1.0);
                }
        });
        return Data.ToArray();
    }

    async Task Issues_Links()
    {
        var Puppets = await Fetch_Puppet_Data();
        await TarotHTML.Generate_Issue_Links(Puppets);
    }

    async Task Pack_Links()
    {
        var Puppets = await Database.Table<PuppetData>().ToArrayAsync();
        if(AnsiConsole.Confirm("Request packs info?"))
            await Fetch_Puppet_Data();
        bool AutoClose = AnsiConsole.Confirm("AutoClose Pack Links?");
        await TarotHTML.Generate_Pack_Links(Puppets.ToArray(), AutoClose);
    }
}