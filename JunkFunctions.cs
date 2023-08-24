using Spectre.Console;

public partial class Tarot
{
    async Task Junker()
    {
        var SortMode = AnsiConsole.Prompt(new SelectionPrompt<string>()
        .Title("Select Juking mode")
        .AddChoices(new[] {
            "1. ∆V Mode", "2. Slow Mode (Requires Card Info)",
        }));
        switch(SortMode[0])
        {
            case '1':
                await Fast_Junker();
                break;
            case '2':
                await Slow_Junker();
                break;
        }
    }

    async Task Fast_Junker()
    {
        float Threshhold = (float)Math.Round(AnsiConsole.Ask<float>("∆V Threshhold: "), 2);
        // Select puppets with a ∆V < Threshhold
        var Puppets = (await Database.Table<PuppetViewEntry>().ToListAsync())
            .Where( R => (R.DeckValue - R.JunkValue) > Threshhold);
        // Filter out legendaries and group the cards by the puppet that owns them
        var Cards = (await Database.Table<DeckViewEntry>().ToListAsync())
            .Where( C => C.RarityInt < 5 )
            .GroupBy( C => C.Owner)
            .Where( G => Puppets.Any(P=>P.Name == G.First().Owner))
            .Select( C => (C.First().Owner, C.ToArray()))
            .ToArray();
        await TarotHTML.Generate_Junk_Links(Cards);
    }

    async Task Slow_Junker()
    {
        AnsiConsole.MarkupLine("[yellow]NOTICE:[/] You must have run Get Card Info first.");
        int OwnerThresh = AnsiConsole.Ask<int>("Owner Threshhold: ");
        float DeltaVThresh = AnsiConsole.Ask<float>("∆V Threshhold: ");
        // Select all cards
        var Deck = await Database.Table<DeckViewEntry>().ToListAsync();
        // Cards which have a DeltaV > Threshhold are filtered out for selling
        var Sell_List = Deck
            .Where(C => (C.MarketValue - C.JunkValue) >= DeltaVThresh || C.Owners.Length <= OwnerThresh);
        
        var table = new Table()
            .MinimalDoubleHeadBorder();
        table.AddColumn("Owner").AddColumn("Season").AddColumn("Card").AddColumn("Rarity").AddColumn("MV").AddColumn("∆V").AddColumn("Owners");
        foreach(var Card in Sell_List)
            table.AddRow(MarkWrapMany(Card.Season, Card.Name, Card.Rarity, Card.MarketValue, Card.MarketValue - Card.JunkValue, Card.Owners.Length).Prepend(Linkify(Card.Owner)));
        AnsiConsole.Write(table);

        // Create the junk list and junk html file
        var Junk = Deck.Where(C => {
            return C.MarketValue < DeltaVThresh && // If it doesn't make the DeltaV threshold
                C.Owners.Length < OwnerThresh && // If it has too many owners
                C.RarityInt < 5; // If it's not a legendary
        }).GroupBy(C => C.Owner).Select(C=>(C.First().Owner, C.ToArray())).ToArray();
        await TarotHTML.Generate_Junk_Links(Junk);
    }
}