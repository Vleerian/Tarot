using NSDotnet;
using NSDotnet.Models;

using Spectre.Console;


public partial class Tarot
{
    async Task GetPuppetInfo()
    {
        DBPuppet[] Puppets = await Database.Table<DBPuppet>().ToArrayAsync();
        await Database.CreateTableAsync<DeckDB>();
        await Database.CreateTableAsync<PuppetData>();
        await Database.DeleteAllAsync<DeckDB>();
        await Database.DeleteAllAsync<PuppetData>();
        await AnsiConsole.Progress()
            .StartAsync(async ctx => {
                var FetchTask = ctx.AddTask("Fetching data...", maxValue:Puppets.Length);
                foreach(var Puppet in Puppets)
                {
                    FetchTask.Description = $"Fetching data for [yellow]{Puppet.Puppet}[/]";
                    try{
                        NSAPI.Instance.Auth = new NSAuth(NSDotnet.Enums.AuthType.Autologin, Puppet.Password);
                        var Result = await NSAPI.Instance.GetAPI<CardsAPI>($"https://www.nationstates.net/cgi-bin/api.cgi?q=cards+deck+info+packs;nationname={Puppet.Puppet}");
                        var Deck = Result.Data.Deck.Select(D=>new DeckDB() {
                            Owner = Puppet.Puppet,
                            ID = D.ID,
                            Rarity = DBCard.RarityToInt(D.Rarity),
                            Season = D.Season
                        }).ToArray();
                        await Database.InsertAllAsync(Deck);
                        var DeckInfo = Result.Data.Deck_Info;
                        await Database.InsertAsync(new PuppetData() {
                            Name = Puppet.Puppet,
                            Bank = DeckInfo.Bank,
                            Deck_Value = DeckInfo.DeckValue,
                            Num_Cards = DeckInfo.Num_Cards
                        });
                        System.Threading.Thread.Sleep(800);
                    }
                    catch(Exception e)
                    {
                        AnsiConsole.MarkupLine($"Failed to get deck information for {Puppet.Puppet}");
                        AnsiConsole.WriteException(e);
                    }
                    FetchTask.Increment(1.0);
                }
            });
    }

    async Task FindLegendaries()
    {
        var owners = await Database.QueryAsync<DeckViewEntry>("SELECT * FROM DeckView WHERE RarityInt = 5");
        var table = new Table()
            .MinimalDoubleHeadBorder();
        table.AddColumn("Owner").AddColumn("Card").AddColumn("Rarity").AddColumn("Season");
        foreach(var owner in owners)
            table.AddRow(MarkWrapMany(owner.Name, owner.Season, owner.Rarity).Prepend(Linkify(owner.Owner)));
        AnsiConsole.Write(table);
    }

    async Task FindOwner()
    {
        bool Run = true;
        while(Run)
        {
            string search = AnsiConsole.Ask<string>("Card Name (exit to stop)");
            if(search.Trim().ToLower() == "exit")
                return;
            var card = Helpers.SanitizeName(search);
            var owners = await Database.QueryAsync<DeckViewEntry>("SELECT * FROM DeckView WHERE Name = ?", card);
            var table = new Table();
            table.AddColumn("Owner").AddColumn("Card").AddColumn("Season");
            foreach(var owner in owners)
                table.AddRow(MarkWrapMany(owner.Name, owner.Season.ToString()).Prepend(Linkify(owner.Owner)));
            AnsiConsole.Write(table);
        }
    }

    async Task<DBCard[]> FindCard(string cardName, int season = 0)
    {
        if(season < 0 || season > 3)
            season = 0;
        List<DBCard> cards;
        if(season != 0)
            cards = await Database.QueryAsync<DBCard>("SELECT * FROM Cards WHERE Name = ? AND Season = ?", cardName, season);
        else
            cards = await Database.QueryAsync<DBCard>("SELECT * FROM Cards WHERE Name = ?", cardName, season);
        
        return cards.ToArray();
    }

    async Task ListPuppets()
    {
        var SortMode = AnsiConsole.Prompt(new SelectionPrompt<string>()
        .Title("Select Sorting mode")
        .AddChoices(new[] {
            "1. DeckValue", "2. Junk Value",
            "3. Bank", "4. NumCards", "5. DV-JV",
            "6. JV + Bank"
        }));
        List<PuppetViewEntry> PuppetData;
        switch(SortMode[0])
        {
            case '1':
                PuppetData = await Database.QueryAsync<PuppetViewEntry>("SELECT * FROM PuppetStats ORDER BY DeckValue DESC");
                break;
            case '2':
                PuppetData = await Database.QueryAsync<PuppetViewEntry>("SELECT * FROM PuppetStats ORDER BY JunkValue DESC");
                break;
            case '3':
                PuppetData = await Database.QueryAsync<PuppetViewEntry>("SELECT * FROM PuppetStats ORDER BY Bank DESC");
                break;
            case '4':
                PuppetData = await Database.QueryAsync<PuppetViewEntry>("SELECT * FROM PuppetStats ORDER BY Num_Cards DESC");
                break;
            case '5':
                PuppetData = await Database.QueryAsync<PuppetViewEntry>("SELECT * FROM PuppetStats ORDER BY DeckValue - JunkValue DESC");
                break;
            case '6':
                PuppetData = await Database.QueryAsync<PuppetViewEntry>("SELECT * FROM PuppetStats ORDER BY JunkValue + Bank DESC");
                break;
            default:
                return;
        }

        List<DeckViewEntry> DeckData = await Database.QueryAsync<DeckViewEntry>("SELECT * FROM DeckView");

        var table = new Table()
            .MinimalDoubleHeadBorder();;
        table.AddColumn("Puppet").AddColumn("Bank").AddColumn("JV").AddColumn("BV").AddColumn("DV").AddColumn("∆V").AddColumn("Cards").AddColumn("Breakdown");
        foreach(var puppet in PuppetData)
        {
            var Items = MarkWrapMany(puppet.Bank, puppet.JunkValue, puppet.Bank + puppet.JunkValue,
                puppet.DeckValue, Math.Round(puppet.DeckValue - puppet.JunkValue, 2), puppet.Num_Cards)
                .Prepend(Linkify(Helpers.SanitizeName(puppet.Name)))
                .Append(await GenerateBreakdown(DeckData.Where(P=>P.Owner == puppet.Name).ToArray()));
            table.AddRow(Items);
        }
        table.AddRow(MarkWrapMany("Total", PuppetData.Sum(P=>P.Bank), PuppetData.Sum(P=>P.JunkValue), PuppetData.Sum(P=>P.DeckValue),
            "-", PuppetData.Sum(P=>P.Num_Cards), "-"));
        AnsiConsole.Write(table);
    }

    async Task GetCardInfo()
    {
        // Reset card values
        await Database.CreateTableAsync<DBCard>();
        await Database.ExecuteAsync("UPDATE Cards SET MarketValue = 0, TopBuy = 0, Owners = NULL");
        // Get unique cards
        var Unique_Cards = await Database.QueryAsync<DeckViewEntry>("SELECT DISTINCT * FROM DeckView");
        int CardCount = Unique_Cards.Count;
        AnsiConsole.MarkupLine($"[yellow]NOTICE[/] Fetching card info will take [cyan]{TimeSpan.FromSeconds(Unique_Cards.Count*0.6).ToString("c")}[/]");
        List<DBCard> Cards = new();
        await AnsiConsole.Progress()
            .StartAsync(async ctx => {
                var Task1 = ctx.AddTask("Fetch card data...", maxValue: Unique_Cards.Count);
                foreach(var Card in Unique_Cards)
                {
                    await Task.Delay(600);
                    Task1.Description = $"Fetch card data for [yellow]S{Card.Season} {Card.Name}[/]";
                    var Result = await NSAPI.Instance.GetAPI<CardAPI>($"https://www.nationstates.net/cgi-bin/api.cgi?q=card+info+owners+market;cardid={Card.ID};season={Card.Season}");
                    if(Result.Response.StatusCode != System.Net.HttpStatusCode.OK)
                    {
                        AnsiConsole.MarkupLine($"Failed fetching data for [red]S{Card.Season} {Card.Name}[/]");
                        continue;
                    }
                    CardAPI C = Result.Data;
                    var TopBuy = C.Markets?.Where(M => M.type == "bid").MaxBy(M=>M.price)?.price ?? 0.0f;
                    var Owners = string.Join(",", C.Owners);
                    await Database.ExecuteAsync(
                        "UPDATE Cards SET MarketValue = ?, TopBuy = ?, Owners = ? WHERE ID = ? AND Season = ?",
                        C.MarketValue ?? 0.0f, TopBuy, Owners, Card.ID, Card.Season
                    );
                    Task1.Increment(1.0);
                }
            });
    }
}