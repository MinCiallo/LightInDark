using LightInDark.Configuration;
using LightInDark.Roles;

namespace Light.Roles.Impostors;

public class EvilGuesser : GuesserRole
{
    public const string Code = "EvilGuesser";

    public static readonly EvilGuesser MyRole = new();

    public override string CodeName => Code;
    public override LightInDark.Color Color => LightInDark.Color.ImpostorColor;
    public override RoleCategory RoleCategory => RoleCategory.Impostor;
    public override AllocationParameters Allocation => new() { MaxCount = 1, Chance = 30 };
}
