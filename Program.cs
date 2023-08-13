using System.Text;
using System.Text.RegularExpressions;

using System.Xml;
using System.Xml.Serialization;

using NSDotnet;
using NSDotnet.Models;

using SQLite;

using Spectre.Console;
using Spectre.Console.Rendering;
using Spectre.Console.Cli;

await new Tarot().Execute();