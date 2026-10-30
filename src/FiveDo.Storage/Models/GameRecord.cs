
namespace FiveDo.Storage;


/// <summary>
/// One entry from the 4DO game database.
/// </summary>
/// <param name="Id">The 4DO game identifier.</param>
/// <param name="CheckSum">MD5 of the first two disc sectors, uppercase hex.</param>
/// <param name="Name">Game title.</param>
/// <param name="ReleaseYear">Release year.</param>
/// <param name="Publisher">Publisher.</param>
/// <param name="Regions">Region codes.</param>
public sealed record GameRecord(
    string Id,
    string CheckSum,
    string Name,
    string ReleaseYear,
    string Publisher,
    string Regions);
