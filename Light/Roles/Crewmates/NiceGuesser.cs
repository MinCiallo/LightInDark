using LightInDark.Configuration;
using LightInDark.Roles;

namespace Light.Roles.Crewmates;

public class NiceGuesser : GuesserRole
{
    public const string Code = "NiceGuesser";

    public static readonly NiceGuesser MyRole = new();

    public override string CodeName => Code;
    public override LightInDark.Color Color => LightInDark.Color.Yellow;
    public override RoleCategory RoleCategory => RoleCategory.Crewmate;
    public override AllocationParameters Allocation => new() { MaxCount = 1, Chance = 30 };
}
